namespace JobBoard.Domain.Enums;

public enum JobType
{
    FullTime = 0,
    PartTime = 1,
    Contract = 2,
    Freelance = 3,
    Internship = 4,
}

/// <summary>Who the job is open to (remote-first marketplace).</summary>
public enum Region
{
    Worldwide = 0,
    PhilippinesOnly = 1,
    Custom = 2,
}

public enum PayType
{
    Hourly = 0,
    Monthly = 1,
    FixedPrice = 2,
}

public enum ExperienceLevel
{
    Junior = 0,
    Mid = 1,
    Senior = 2,
    Lead = 3,
}

/// <summary>Moderation lifecycle for a job posting.</summary>
public enum JobStatus
{
    Draft = 0,
    /// <summary>Submitted by the employer, waiting for admin approval.</summary>
    Pending = 1,
    Published = 2,
    /// <summary>Flagged by an admin; hidden from the public board.</summary>
    Flagged = 3,
    /// <summary>Rejected by an admin; hidden from the public board.</summary>
    Rejected = 4,
    /// <summary>Closed by the employer.</summary>
    Closed = 5,
}

public enum ApplicationStatus
{
    Submitted = 0,
    Shortlisted = 1,
    Rejected = 2,
    Withdrawn = 3,
}
