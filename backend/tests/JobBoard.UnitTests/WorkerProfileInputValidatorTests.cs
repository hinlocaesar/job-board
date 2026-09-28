using FluentAssertions;
using JobBoard.Api.Features.Profiles;

namespace JobBoard.UnitTests;

public sealed class WorkerProfileInputValidatorTests
{
    private readonly WorkerProfileInputValidator _validator = new();

    private static WorkerProfileInput Valid() => new(
        Headline: "Senior Frontend Developer",
        Summary: "Six years of dashboards and design systems.",
        Country: "PH",
        City: "Cebu",
        TimeZone: "Asia/Manila",
        YearsOfExperience: 5,
        RateMin: 20,
        RateMax: 40,
        Currency: "USD",
        RatePeriod: "Hour",
        Availability: "Freelance",
        IsPublic: true,
        ResumeFileId: null,
        Skills: [new SkillInput("Vue.js", 5, 4)],
        Experiences: []);

    [Fact]
    public void Valid_Input_Passes()
    {
        var result = _validator.Validate(Valid());

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Headline_Is_Required_And_Minimum_Four_Characters()
    {
        _validator.Validate(Valid() with { Headline = "" }).IsValid.Should().BeFalse();
        _validator.Validate(Valid() with { Headline = "abc" }).IsValid.Should().BeFalse();
        _validator.Validate(Valid() with { Headline = new string('x', 201) }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Country_Must_Not_Exceed_Two_Characters()
        => _validator.Validate(Valid() with { Country = "PHL" }).IsValid.Should().BeFalse();

    [Fact]
    public void Rate_Max_Below_Rate_Min_Is_Rejected_When_A_Rate_Is_Set()
    {
        var result = _validator.Validate(Valid() with { RateMin = 50, RateMax = 10 });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "RateMax must be greater than or equal to RateMin.");
    }

    [Fact]
    public void Zero_Rates_Are_Allowed_Even_When_Equal()
        => _validator.Validate(Valid() with { RateMin = 0, RateMax = 0 }).IsValid.Should().BeTrue();

    [Fact]
    public void Negative_Rate_Is_Rejected()
        => _validator.Validate(Valid() with { RateMin = -5, RateMax = 40 }).IsValid.Should().BeFalse();

    [Fact]
    public void Currency_Must_Be_Exactly_Three_Characters()
        => _validator.Validate(Valid() with { Currency = "PHP" + "1" }).IsValid.Should().BeFalse();

    [Theory]
    [InlineData((short)61)]
    [InlineData((short)-1)]
    public void YearsOfExperience_Must_Be_Between_0_And_60(short years)
        => _validator.Validate(Valid() with { YearsOfExperience = years }).IsValid.Should().BeFalse();

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)6)]
    public void Skill_Level_Must_Be_Between_1_And_5(byte level)
    {
        var result = _validator.Validate(Valid() with { Skills = [new SkillInput("Vue.js", level, 3)] });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Level must be between 1 and 5.");
    }

    [Fact]
    public void More_Than_50_Skills_Is_Rejected()
    {
        var skills = Enumerable.Range(1, 51).Select(i => new SkillInput($"skill-{i}")).ToList();

        var result = _validator.Validate(Valid() with { Skills = skills });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "At most 50 skills.");
    }

    [Fact]
    public void Skill_Without_A_Name_Is_Rejected()
        => _validator.Validate(Valid() with { Skills = [new SkillInput("")] }).IsValid.Should().BeFalse();

    [Theory]
    [InlineData("Fortnightly")]
    [InlineData("")]
    public void Unknown_RatePeriod_Is_Rejected(string ratePeriod)
    {
        var result = _validator.Validate(Valid() with { RatePeriod = ratePeriod });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "RatePeriod must be one of: Hour, Day, Month.");
    }

    [Fact]
    public void Unknown_Availability_Is_Rejected()
    {
        var result = _validator.Validate(Valid() with { Availability = "Whenever" });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.StartsWith("Availability must be one of"));
    }

    [Fact]
    public void Experience_EndDate_Before_StartDate_Is_Rejected()
    {
        var experience = new ExperienceInput(
            Title: "Developer",
            CompanyName: "ACME",
            Location: null,
            StartDate: new DateOnly(2022, 1, 1),
            EndDate: new DateOnly(2021, 1, 1),
            IsCurrent: false,
            Description: null);

        var result = _validator.Validate(Valid() with { Experiences = [experience] });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "EndDate must be after StartDate.");
    }
}
