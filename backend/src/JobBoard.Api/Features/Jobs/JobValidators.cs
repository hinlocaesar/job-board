using FluentValidation;
using JobBoard.Api.Common;
using JobBoard.Api.Features.Jobs;
using JobBoard.Domain.Enums;

namespace JobBoard.Api.Features.Jobs;

public sealed class JobInputValidator : AbstractValidator<JobInput>
{
    public JobInputValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MinimumLength(5).MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MinimumLength(20).MaximumLength(20000);
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.JobType).Must(v => EnumValue<JobType>.IsValid(v))
            .WithMessage($"JobType must be one of: {string.Join(", ", Enum.GetNames<JobType>())}.");
        RuleFor(x => x.Region).Must(v => EnumValue<Region>.IsValid(v))
            .WithMessage($"Region must be one of: {string.Join(", ", Enum.GetNames<Region>())}.");
        RuleFor(x => x.PayType).Must(v => EnumValue<PayType>.IsValid(v))
            .WithMessage($"PayType must be one of: {string.Join(", ", Enum.GetNames<PayType>())}.");
        RuleFor(x => x.ExperienceLevel).Must(v => EnumValue<ExperienceLevel>.IsValid(v))
            .WithMessage($"ExperienceLevel must be one of: {string.Join(", ", Enum.GetNames<ExperienceLevel>())}.");
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.PayMin).InclusiveBetween(0, 1_000_000);
        RuleFor(x => x.PayMax).InclusiveBetween(0, 1_000_000);
        RuleFor(x => x.PayMax).GreaterThanOrEqualTo(x => x.PayMin).WithMessage("PayMax must be greater than or equal to PayMin.");
        RuleFor(x => x.HoursPerWeek).Must(v => v is null || v is >= 1 and <= 80).When(x => x.HoursPerWeek is not null)
            .WithMessage("HoursPerWeek must be between 1 and 80.");
        RuleFor(x => x.ClosesAt).GreaterThan(DateTime.UtcNow).When(x => x.ClosesAt is not null)
            .WithMessage("ClosesAt must be in the future.");
        RuleFor(x => x.Skills).Must(list => list.Count <= 20).WithMessage("At most 20 skills.");
        RuleForEach(x => x.Skills).NotEmpty().MaximumLength(100);
    }
}
