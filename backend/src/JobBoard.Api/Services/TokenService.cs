using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using JobBoard.Api.Options;
using JobBoard.Domain.Entities;
using JobBoard.Identity;
using JobBoard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace JobBoard.Api.Services;

/// <param name="AccessToken">Short-lived JWT handed to the client.</param>
/// <param name="RefreshToken">Raw rotating refresh token handed to the client (only its hash is persisted).</param>
public sealed record SessionTokens(string AccessToken, string RefreshToken, RefreshToken Entity);

public interface ITokenService
{
    SessionTokens GenerateTokens(ApplicationUser user, IList<string> roles, string? ip, string? userAgent);
    Task<RefreshToken?> GetActiveRefreshTokenAsync(string refreshToken);
    void Revoke(RefreshToken token);
}

public sealed class TokenService : ITokenService
{
    private readonly AppDbContext _db;
    private readonly JwtOptions _options;

    public TokenService(AppDbContext db, IOptions<JwtOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public SessionTokens GenerateTokens(ApplicationUser user, IList<string> roles, string? ip, string? userAgent)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.AccessTokenSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Name, user.FullName ?? user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("email_verified", user.EmailConfirmed.ToString().ToLowerInvariant()),
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes),
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        var rawRefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = Hash(rawRefreshToken),
            ExpiresAt = DateTime.UtcNow.AddDays(_options.RefreshTokenDays),
            Ip = ip,
            UserAgent = userAgent is { Length: > 256 } ? userAgent[..256] : userAgent,
        };

        _db.RefreshTokens.Add(refreshToken);
        return new SessionTokens(accessToken, rawRefreshToken, refreshToken);
    }

    public Task<RefreshToken?> GetActiveRefreshTokenAsync(string refreshToken)
    {
        var hash = Hash(refreshToken);
        return _db.RefreshTokens
            .Where(t => t.TokenHash == hash && t.RevokedAt == null && t.ExpiresAt > DateTime.UtcNow)
            .FirstOrDefaultAsync();
    }

    public void Revoke(RefreshToken token) => token.RevokedAt = DateTime.UtcNow;

    public static string Hash(string value)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }
}
