using FluentAssertions;
using JobBoard.Api.Common;
using JobBoard.Domain.Enums;

namespace JobBoard.UnitTests;

public sealed class EnumValueTests
{
    [Theory]
    [InlineData("FullTime", JobType.FullTime)]
    [InlineData("fulltime", JobType.FullTime)]
    [InlineData("full_time", JobType.FullTime)]
    [InlineData("FULL-TIME", JobType.FullTime)]
    [InlineData("full time", JobType.FullTime)]
    [InlineData("PART_TIME", JobType.PartTime)]
    [InlineData("Contract", JobType.Contract)]
    public void TryParse_Is_Case_Underscore_And_Separator_Insensitive_For_JobType(string value, JobType expected)
    {
        EnumValue<JobType>.TryParse(value, out var result).Should().BeTrue();
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("philippines_only", Region.PhilippinesOnly)]
    [InlineData("Worldwide", Region.Worldwide)]
    [InlineData("fixed_price", PayType.FixedPrice)]
    [InlineData("HOURLY", PayType.Hourly)]
    [InlineData("project_based", Availability.ProjectBased)]
    public void TryParse_Works_For_Other_Enums_Too(string value, Enum expected)
    {
        // The method is generic; exercise it through one representative enum per value.
        switch (expected)
        {
            case Region region:
                EnumValue<Region>.TryParse(value, out var regionResult).Should().BeTrue();
                regionResult.Should().Be(region);
                break;
            case PayType payType:
                EnumValue<PayType>.TryParse(value, out var payTypeResult).Should().BeTrue();
                payTypeResult.Should().Be(payType);
                break;
            case Availability availability:
                EnumValue<Availability>.TryParse(value, out var availabilityResult).Should().BeTrue();
                availabilityResult.Should().Be(availability);
                break;
            default:
                throw new InvalidOperationException($"Unhandled enum {expected.GetType().Name}.");
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("banana")]
    [InlineData("fulltimee")]
    public void TryParse_Returns_False_For_Null_Empty_And_Unknown_Values(string? value)
        => EnumValue<JobType>.TryParse(value, out _).Should().BeFalse();

    [Fact]
    public void TryParse_Returns_False_For_Undefined_Numeric_Values()
        => EnumValue<JobType>.TryParse("99", out _).Should().BeFalse();

    [Fact]
    public void IsValid_Mirrors_TryParse()
    {
        EnumValue<JobType>.IsValid("full_time").Should().BeTrue();
        EnumValue<JobType>.IsValid("nope").Should().BeFalse();
    }

    [Fact]
    public void Parse_Returns_The_Value_For_A_Valid_String()
        => EnumValue<PayType>.Parse("fixed_price", "payType").Should().Be(PayType.FixedPrice);

    [Fact]
    public void Parse_Throws_ApiException_With_Field_Errors_For_An_Invalid_Value()
    {
        var act = () => EnumValue<JobType>.Parse("banana", "jobType");

        var exception = act.Should().Throw<ApiException>().Which;
        exception.Code.Should().Be("invalid_enum");
        exception.Errors.Should().ContainKey("jobType");
        exception.Errors!["jobType"][0].Should().Contain("FullTime");
    }
}
