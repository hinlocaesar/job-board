using JobBoard.Api.Common;
using JobBoard.Api.Features.Auth;
using JobBoard.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Hosting;

namespace JobBoard.Api.Controllers;

/// <summary>All endpoints here are unauthenticated and rate-limited per IP (10/min default).</summary>
[Route("api/auth")]
[EnableRateLimiting("auth")]
public sealed class AuthController : ApiControllerBase
{
    private readonly IAuthService _auth;
    private readonly IConfiguration _configuration;

    public AuthController(IAuthService auth, IConfiguration configuration)
    {
        _auth = auth;
        _configuration = configuration;
    }

    /// <summary>Register as an employer or a worker (consent required, verification email sent).</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<UserDto>> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        await ValidateAsync(request);
        return Ok(await _auth.RegisterAsync(request, cancellationToken));
    }

    [HttpPost("verify-email")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request, CancellationToken cancellationToken)
    {
        await ValidateAsync(request);
        await _auth.VerifyEmailAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpPost("resend-verification")]
    [AllowAnonymous]
    public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationRequest request, CancellationToken cancellationToken)
    {
        await ValidateAsync(request);
        await _auth.ResendVerificationAsync(request.Email, cancellationToken);
        return Accepted(new AuthMessage(true, "If that address needs verification, a new email has been sent."));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        await ValidateAsync(request);
        return Ok(await _auth.LoginAsync(request, ClientIp, Request.Headers.UserAgent, cancellationToken));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Refresh([FromBody] RefreshRequest request, CancellationToken cancellationToken)
    {
        await ValidateAsync(request);
        return Ok(await _auth.RefreshAsync(request.RefreshToken, ClientIp, Request.Headers.UserAgent, cancellationToken));
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request, CancellationToken cancellationToken)
    {
        await ValidateAsync(request);
        await _auth.RevokeAsync(request.RefreshToken);
        return NoContent();
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await ValidateAsync(request);
        await _auth.ForgotPasswordAsync(request.Email, cancellationToken);
        return Accepted(new AuthMessage(true, "If that address exists, a reset link has been sent."));
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        await ValidateAsync(request);
        await _auth.ResetPasswordAsync(request, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Dev-only: lists the most recent messages written by the log email provider,
    /// so verification/reset links are clickable without a real SMTP server.
    /// </summary>
    [HttpGet("outbox")]
    [AllowAnonymous]
    public IActionResult Outbox([FromServices] IHostEnvironment environment)
    {
        if (!environment.IsDevelopment())
            return NotFound();

        var path = Path.Combine(environment.ContentRootPath, "logs", "outbox.log");
        if (!System.IO.File.Exists(path))
            return Ok(Array.Empty<object>());

        var text = System.IO.File.ReadAllText(path);
        var entries = text.Split("----- ", StringSplitOptions.RemoveEmptyEntries)
            .Where(e => e.Trim().Length > 0)
            .TakeLast(10)
            .Select(e => e.Trim())
            .ToArray();

        return Ok(entries);
    }
}
