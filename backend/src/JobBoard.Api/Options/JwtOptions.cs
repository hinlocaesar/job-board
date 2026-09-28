namespace JobBoard.Api.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "JobBoard";
    public string Audience { get; set; } = "JobBoard";
    /// <summary>Symmetric signing key. Must be >= 32 bytes for HMAC-SHA256.</summary>
    public string AccessTokenSecret { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 14;
}
