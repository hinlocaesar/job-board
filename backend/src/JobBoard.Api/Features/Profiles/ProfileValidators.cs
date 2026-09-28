using FluentValidation;
using JobBoard.Api.Common;
using JobBoard.Api.Features.Profiles;

namespace JobBoard.Api.Features.Profiles;

public sealed class WorkerProfileInputValidator : AbstractValidator<WorkerProfileInput>
{
    public WorkerProfileInputValidator()
    {
        RuleFor(x => x.Headline).NotEmpty().MinimumLength(4).MaximumLength(200);
        RuleFor(x => x.Summary).MaximumLength(5000);
        RuleFor(x => x.Country).NotEmpty().MaximumLength(2);
        RuleFor(x => x.City).MaximumLength(120);
        RuleFor(x => x.TimeZone).MaximumLength(80);
        RuleFor(x => x.YearsOfExperience).Must(v => v is >= 0 and <= 60);
        RuleFor(x => x.RateMin).InclusiveBetween(0, 100_000);
        RuleFor(x => x.RateMax).InclusiveBetween(0, 100_000);
        RuleFor(x => x.RateMax).GreaterThanOrEqualTo(x => x.RateMin)
            .When(x => x.RateMin > 0)
            .WithMessage("RateMax must be greater than or equal to RateMin.");
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.RatePeriod).Must(v => EnumValue<JobBoard.Domain.Enums.RatePeriod>.IsValid(v))
            .WithMessage("RatePeriod must be one of: Hour, Day, Month.");
        RuleFor(x => x.Availability).Must(v => EnumValue<JobBoard.Domain.Enums.Availability>.IsValid(v))
            .WithMessage("Availability must be one of: FullTime, PartTime, Freelance, ProjectBased.");
        RuleFor(x => x.Skills).Must(list => list.Count <= 50).WithMessage("At most 50 skills.");
        RuleForEach(x => x.Skills).ChildRules(skill =>
        {
            skill.RuleFor(s => s.Name).NotEmpty().MaximumLength(100);
            skill.RuleFor(s => s.Level).Must(v => v is >= 1 and <= 5).WithMessage("Level must be between 1 and 5.");
            skill.RuleFor(s => s.YearsExperience).Must(v => v is >= 0 and <= 60);
        });
        RuleForEach(x => x.Experiences).ChildRules(exp =>
        {
            exp.RuleFor(e => e.Title).NotEmpty().MaximumLength(200);
            exp.RuleFor(e => e.CompanyName).NotEmpty().MaximumLength(200);
            exp.RuleFor(e => e.Description).MaximumLength(4000);
            exp.RuleFor(e => e.EndDate).GreaterThanOrEqualTo(e => e.StartDate)
                .When(e => e.EndDate is not null)
                .WithMessage("EndDate must be after StartDate.");
        });
    }
}

public sealed class EmployerProfileInputValidator : AbstractValidator<EmployerProfileInput>
{
    public EmployerProfileInputValidator()
    {
        RuleFor(x => x.CompanyName).NotEmpty().MinimumLength(2).MaximumLength(200);
        RuleFor(x => x.Website).Must(BeAValidUrl).When(x => !string.IsNullOrWhiteSpace(x.Website))
            .WithMessage("Website must be a valid http(s) URL.");
        RuleFor(x => x.Description).MaximumLength(5000);
        RuleFor(x => x.Country).MaximumLength(2);
    }

    private static bool BeAValidUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}


