using System.Security.Cryptography;
using System.Text;
using JobBoard.Api.Common;
using JobBoard.Api.Features.Auth;
using JobBoard.Api.Services;
using JobBoard.Domain.Entities;
using JobBoard.Identity;
using JobBoard.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using JobBoard.Api.Options;
// Identity also defines an IEmailSender<TUser>; ours is the domain abstraction.
using IEmailSender = JobBoard.Domain.Abstractions.IEmailSender;

namespace JobBoard.Api.Services;

public interface IAuthService
{
    Task<UserDto> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task VerifyEmailAsync(VerifyEmailRequest request, CancellationToken cancellationToken = default);
    Task ResendVerificationAsync(string email, CancellationToken cancellationToken = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, string? ip, string? userAgent, CancellationToken cancellationToken = default);
    Task<AuthResponse> RefreshAsync(string refreshToken, string? ip, string? userAgent, CancellationToken cancellationToken = default);
    Task RevokeAsync(string refreshToken);
    Task ForgotPasswordAsync(string email, CancellationToken cancellationToken = default);
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);
    Task<UserDto> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed class AuthService : IAuthService
{
    private static readonly TimeSpan EmailCodeLifetime = TimeSpan.FromHours(24);
    private static readonly TimeSpan ResetCodeLifetime = TimeSpan.FromHours(1);

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _db;
    private readonly ITokenService _tokens;
    private readonly IEmailSender _email;
    private readonly IConfiguration _configuration;
    private readonly JwtOptions _jwt;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        AppDbContext db,
        ITokenService tokens,
        IEmailSender email,
        IConfiguration configuration,
        IOptions<JwtOptions> jwt)
    {
        _userManager = userManager;
        _db = db;
        _tokens = tokens;
        _email = email;
        _configuration = configuration;
        _jwt = jwt.Value;
    }

    private string FrontendUrl => (_configuration["App:FrontendUrl"] ?? "http://localhost:5173").TrimEnd('/');
    private string PrivacyVersion => _configuration["App:PrivacyPolicyVersion"] ?? "2026-09-01";

    public async Task<UserDto> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await _userManager.FindByEmailAsync(email) is not null)
            throw ApiException.Conflict("email_taken", "An account with this email already exists.");

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            FullName = request.FullName.Trim(),
            AccountStatus = Domain.Enums.AccountStatus.Active,
            // PH Data Privacy Act (in spirit): explicit, versioned consent on signup.
            PrivacyConsentAt = DateTime.UtcNow,
            PrivacyPolicyVersion = string.IsNullOrWhiteSpace(request.PrivacyPolicyVersion)
                ? PrivacyVersion
                : request.PrivacyPolicyVersion.Trim(),
            MarketingConsent = request.MarketingConsent,
            CreatedAt = DateTime.UtcNow,
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            throw FromIdentityErrors(result);

        var role = request.Role.Trim().ToLowerInvariant();
        await _userManager.AddToRoleAsync(user, role);

        await IssueAuthCodeAsync(user, Domain.Enums.AuthCodePurpose.EmailConfirmation, EmailCodeLifetime, "Verify your JobBoard email address", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return await GetCurrentUserAsync(user.Id, cancellationToken);
    }

    public async Task VerifyEmailAsync(VerifyEmailRequest request, CancellationToken cancellationToken = default)
    {
        var code = await FindValidCodeAsync(request.Email, Domain.Enums.AuthCodePurpose.EmailConfirmation, request.Code, cancellationToken);
        var user = await _userManager.FindByIdAsync(code.UserId.ToString())
            ?? throw ApiException.NotFound("Account");

        user.EmailConfirmed = true;
        code.UsedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ResendVerificationAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email.Trim().ToLowerInvariant());
        if (user is null || user.EmailConfirmed)
            return; // silent no-op: never reveal whether an address exists

        await IssueAuthCodeAsync(user, Domain.Enums.AuthCodePurpose.EmailConfirmation, EmailCodeLifetime, "Verify your JobBoard email address", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, string? ip, string? userAgent, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email.Trim().ToLowerInvariant());
        if (user is null)
            throw ApiException.Unauthorized("invalid_credentials", "Email or password is incorrect.");

        if (user.AccountStatus is Domain.Enums.AccountStatus.Banned or Domain.Enums.AccountStatus.Deleted)
            throw ApiException.Forbidden("This account has been suspended. Contact support if you believe this is a mistake.");
        if (user.AccountStatus is Domain.Enums.AccountStatus.Suspended)
            throw ApiException.Forbidden("This account is currently suspended.");

        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            await _userManager.AccessFailedAsync(user);
            if (await _userManager.IsLockedOutAsync(user))
                throw ApiException.TooManyRequests("Too many failed attempts. Try again in a few minutes.");
            throw ApiException.Unauthorized("invalid_credentials", "Email or password is incorrect.");
        }

        if (!user.EmailConfirmed)
            throw ApiException.Unauthorized("email_not_verified", "Please verify your email address before signing in.");

        return await IssueSessionAsync(user, ip, userAgent, cancellationToken);
    }

    public async Task<AuthResponse> RefreshAsync(string refreshToken, string? ip, string? userAgent, CancellationToken cancellationToken = default)
    {
        var token = await _tokens.GetActiveRefreshTokenAsync(refreshToken)
            ?? throw ApiException.Unauthorized("invalid_refresh_token", "The refresh token is no longer valid.");

        var user = await _userManager.FindByIdAsync(token.UserId.ToString())
            ?? throw ApiException.Unauthorized("invalid_refresh_token", "The refresh token is no longer valid.");

        if (user.AccountStatus is not Domain.Enums.AccountStatus.Active)
            throw ApiException.Forbidden("This account is not active.");

        // Rotation: the presented token is revoked and replaced.
        _tokens.Revoke(token);
        return await IssueSessionAsync(user, ip, userAgent, cancellationToken);
    }

    public async Task RevokeAsync(string refreshToken)
    {
        var token = await _tokens.GetActiveRefreshTokenAsync(refreshToken);
        if (token is not null)
        {
            _tokens.Revoke(token);
            await _db.SaveChangesAsync();
        }
    }

    public async Task ForgotPasswordAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email.Trim().ToLowerInvariant());
        if (user is null || user.AccountStatus is Domain.Enums.AccountStatus.Deleted)
            return; // always the same response: no account enumeration

        await IssueAuthCodeAsync(user, Domain.Enums.AuthCodePurpose.PasswordReset, ResetCodeLifetime, "Reset your JobBoard password", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var code = await FindValidCodeAsync(request.Email, Domain.Enums.AuthCodePurpose.PasswordReset, request.Code, cancellationToken);
        var user = await _userManager.FindByIdAsync(code.UserId.ToString())
            ?? throw ApiException.NotFound("Account");

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, request.NewPassword);
        if (!result.Succeeded)
            throw FromIdentityErrors(result);

        code.UsedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        // Logging out everywhere after a password change is the safe default.
        var activeTokens = await _db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAt == null)
            .ToListAsync(cancellationToken);
        activeTokens.ForEach(t => t.RevokedAt = DateTime.UtcNow);

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<UserDto> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw ApiException.NotFound("Account");
        return await ToDtoAsync(user);
    }

    private async Task<AuthResponse> IssueSessionAsync(ApplicationUser user, string? ip, string? userAgent, CancellationToken cancellationToken)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var session = _tokens.GenerateTokens(user, roles, ip, userAgent);
        await _db.SaveChangesAsync(cancellationToken);
        return new AuthResponse(session.AccessToken, session.RefreshToken, await ToDtoAsync(user));
    }

    private async Task<UserDto> ToDtoAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        return new UserDto(
            user.Id,
            user.Email ?? string.Empty,
            user.FullName,
            roles.ToList(),
            user.EmailConfirmed,
            user.AccountStatus.ToString());
    }

    private async Task IssueAuthCodeAsync(ApplicationUser user, Domain.Enums.AuthCodePurpose purpose, TimeSpan lifetime, string subject, CancellationToken cancellationToken)
    {
        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var hash = TokenService.Hash(code);

        // Replace any outstanding code for the same purpose.
        var existing = await _db.AuthCodes
            .Where(c => c.UserId == user.Id && c.Purpose == purpose && c.UsedAt == null)
            .ToListAsync(cancellationToken);
        _db.AuthCodes.RemoveRange(existing);

        _db.AuthCodes.Add(new AuthCode
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Purpose = purpose,
            CodeHash = hash,
            ExpiresAt = DateTime.UtcNow.Add(lifetime),
        });

        var path = purpose == Domain.Enums.AuthCodePurpose.EmailConfirmation ? "verify-email" : "reset-password";
        var link = $"{FrontendUrl}/{path}?email={Uri.EscapeDataString(user.Email ?? string.Empty)}&code={Uri.EscapeDataString(code)}";
        var body = $"""
            <p>Hi {System.Net.WebUtility.HtmlEncode(user.FullName ?? user.Email ?? string.Empty)},</p>
            <p>{System.Net.WebUtility.HtmlEncode(subject)}.</p>
            <p><a href="{link}">Click here to continue</a></p>
            <p>This link expires at {lifetime.TotalHours:0.#} hours. If you didn't request it, you can ignore this email.</p>
            """;

        await _email.SendAsync(user.Email ?? string.Empty, subject, body, cancellationToken);
    }

    private async Task<AuthCode> FindValidCodeAsync(string email, Domain.Enums.AuthCodePurpose purpose, string code, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(email.Trim().ToLowerInvariant())
            ?? throw ApiException.Unauthorized("invalid_code", "The code is invalid or has expired.");

        var hash = TokenService.Hash(code);
        var authCode = await _db.AuthCodes
            .FirstOrDefaultAsync(c => c.UserId == user.Id && c.Purpose == purpose && c.CodeHash == hash, cancellationToken)
            ?? throw ApiException.Unauthorized("invalid_code", "The code is invalid or has expired.");

        if (authCode.UsedAt is not null || authCode.ExpiresAt < DateTime.UtcNow)
            throw ApiException.Unauthorized("invalid_code", "The code is invalid or has expired.");

        return authCode;
    }

    private static ApiException FromIdentityErrors(IdentityResult result)
    {
        var errors = result.Errors
            .GroupBy(e => e.Code.StartsWith("Password", StringComparison.OrdinalIgnoreCase) ? "password" : "account")
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
        return ApiException.BadRequest("identity_failed", "The request could not be processed.", errors);
    }
}
