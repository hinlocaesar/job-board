namespace JobBoard.Api.Features.Auth;

public sealed record RegisterRequest(
    string Email,
    string Password,
    string FullName,
    string Role,
    bool AcceptPrivacy,
    string? PrivacyPolicyVersion,
    bool MarketingConsent = false);

public sealed record VerifyEmailRequest(string Email, string Code);
public sealed record ResendVerificationRequest(string Email);
public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshRequest(string RefreshToken);
public sealed record ForgotPasswordRequest(string Email);
public sealed record ResetPasswordRequest(string Email, string Code, string NewPassword);

public sealed record UserDto(
    Guid Id,
    string Email,
    string? FullName,
    IReadOnlyList<string> Roles,
    bool EmailVerified,
    string AccountStatus);

public sealed record AuthResponse(string AccessToken, string RefreshToken, UserDto User);

public sealed record AuthMessage(bool Accepted, string Message);
