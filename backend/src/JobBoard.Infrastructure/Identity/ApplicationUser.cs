using JobBoard.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace JobBoard.Identity;

/// <summary>
/// Identity user with the moderation + privacy fields required by the MVP.
/// Key type is <see cref="Guid"/> so ids line up with the rest of the domain.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public string? FullName { get; set; }
    public AccountStatus AccountStatus { get; set; } = AccountStatus.Active;

    // PH Data Privacy Act (in spirit): explicit, versioned consent on signup.
    public DateTime? PrivacyConsentAt { get; set; }
    public string? PrivacyPolicyVersion { get; set; }
    public bool MarketingConsent { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
