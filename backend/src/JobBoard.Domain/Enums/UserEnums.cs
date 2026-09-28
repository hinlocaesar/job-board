namespace JobBoard.Domain.Enums;

/// <summary>Lifecycle of a user account (moderation / PH Data Privacy Act deletion).</summary>
public enum AccountStatus
{
    Active = 0,
    Suspended = 1,
    Banned = 2,
    /// <summary>Soft-deleted; PII is anonymized by the export/delete flow.</summary>
    Deleted = 3,
}

/// <summary>How often a worker expects to be paid.</summary>
public enum RatePeriod
{
    Hour = 0,
    Day = 1,
    Month = 2,
}

/// <summary>Worker availability.</summary>
public enum Availability
{
    FullTime = 0,
    PartTime = 1,
    Freelance = 2,
    ProjectBased = 3,
}

/// <summary>Purpose of a stored blob (resume upload, logo, attachment).</summary>
public enum FilePurpose
{
    Resume = 0,
    Logo = 1,
    Attachment = 2,
}

/// <summary>Single-use codes for email verification and password reset.</summary>
public enum AuthCodePurpose
{
    EmailConfirmation = 0,
    PasswordReset = 1,
}
