using FluentAssertions;
using JobBoard.Api.Features.Jobs;

namespace JobBoard.UnitTests;

public sealed class JobInputValidatorTests
{
    private readonly JobInputValidator _validator = new();

    private static JobInput Valid() => new(
        Title: "Senior Platform Engineer",
        Description: "A description that is comfortably longer than the twenty character minimum.",
        CategoryId: Guid.NewGuid(),
        JobType: "FullTime",
        Region: "Worldwide",
        PayType: "Hourly",
        PayMin: 20,
        PayMax: 40,
        Currency: "USD",
        ExperienceLevel: "Mid",
        HoursPerWeek: 40,
        ClosesAt: null,
        Skills: ["TypeScript", "Vue"]);

    [Fact]
    public void Valid_Input_Passes()
    {
        var result = _validator.Validate(Valid());

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Enum_Fields_Accept_Api_Synonyms()
    {
        var input = Valid() with { JobType = "full_time", Region = "philippines_only", PayType = "fixed_price", ExperienceLevel = "SENIOR" };

        _validator.Validate(input).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("Bogus")]
    [InlineData("")]
    [InlineData("42")]
    public void Unknown_JobType_Is_Rejected(string jobType)
    {
        var result = _validator.Validate(Valid() with { JobType = jobType });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(JobInput.JobType));
    }

    [Fact]
    public void Unknown_Region_PayType_And_ExperienceLevel_Are_Rejected()
    {
        var result = _validator.Validate(Valid() with { Region = "Atlantis", PayType = "Crypto", ExperienceLevel = "Intern" });

        result.IsValid.Should().BeFalse();
        result.Errors.Select(e => e.PropertyName)
            .Should().Contain(new[] { nameof(JobInput.Region), nameof(JobInput.PayType), nameof(JobInput.ExperienceLevel) });
    }

    [Theory]
    [InlineData("No")]                              // too short
    [InlineData("")]                                // empty
    public void Bad_Titles_Are_Rejected(string title)
        => _validator.Validate(Valid() with { Title = title }).IsValid.Should().BeFalse();

    [Fact]
    public void Too_Long_Title_Is_Rejected()
        => _validator.Validate(Valid() with { Title = new string('x', 201) }).IsValid.Should().BeFalse();

    [Fact]
    public void Too_Short_Description_Is_Rejected()
        => _validator.Validate(Valid() with { Description = "too short" }).IsValid.Should().BeFalse();

    [Fact]
    public void Pay_Range_Where_Max_Below_Min_Is_Rejected()
    {
        var result = _validator.Validate(Valid() with { PayMin = 500, PayMax = 100 });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "PayMax must be greater than or equal to PayMin.");
    }

    [Fact]
    public void Negative_Pay_Is_Rejected()
    {
        var result = _validator.Validate(Valid() with { PayMin = -1, PayMax = 10 });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(JobInput.PayMin));
    }

    [Fact]
    public void Pay_Above_The_Million_Cap_Is_Rejected()
    {
        var result = _validator.Validate(Valid() with { PayMin = 0, PayMax = 2_000_000 });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(JobInput.PayMax));
    }

    [Fact]
    public void Currency_Must_Be_Exactly_Three_Characters()
        => _validator.Validate(Valid() with { Currency = "US" }).IsValid.Should().BeFalse();

    [Theory]
    [InlineData((short)0)]
    [InlineData((short)81)]
    public void HoursPerWeek_Outside_1_To_80_Is_Rejected(short hours)
        => _validator.Validate(Valid() with { HoursPerWeek = hours }).IsValid.Should().BeFalse();

    [Fact]
    public void Null_HoursPerWeek_Is_Allowed()
        => _validator.Validate(Valid() with { HoursPerWeek = null }).IsValid.Should().BeTrue();

    [Fact]
    public void Past_ClosesAt_Is_Rejected()
        => _validator.Validate(Valid() with { ClosesAt = DateTime.UtcNow.AddDays(-1) }).IsValid.Should().BeFalse();

    [Fact]
    public void More_Than_20_Skills_Is_Rejected()
    {
        var skills = Enumerable.Range(1, 21).Select(i => $"skill-{i}").ToList();

        var result = _validator.Validate(Valid() with { Skills = skills });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "At most 20 skills.");
    }

    [Fact]
    public void Empty_Or_Overlong_Skill_Names_Are_Rejected()
    {
        var emptySkill = _validator.Validate(Valid() with { Skills = [""] });
        var longSkill = _validator.Validate(Valid() with { Skills = [new string('x', 101)] });

        emptySkill.IsValid.Should().BeFalse();
        longSkill.IsValid.Should().BeFalse();
    }
}
