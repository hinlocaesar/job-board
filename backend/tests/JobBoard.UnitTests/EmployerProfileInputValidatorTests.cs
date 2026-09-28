using FluentAssertions;
using JobBoard.Api.Features.Profiles;

namespace JobBoard.UnitTests;

public sealed class EmployerProfileInputValidatorTests
{
    private readonly EmployerProfileInputValidator _validator = new();

    private static EmployerProfileInput Valid() => new(
        CompanyName: "Acme Remote HQ",
        Website: "https://example.com",
        Description: "A remote-first company.",
        Country: "PH",
        LogoFileId: null);

    [Fact]
    public void Valid_Input_Passes()
    {
        var result = _validator.Validate(Valid());

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void CompanyName_Is_Required()
        => _validator.Validate(Valid() with { CompanyName = "" }).IsValid.Should().BeFalse();

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://example.com")]
    [InlineData("example.com")]
    public void Website_Must_Be_A_Http_Or_Https_Url(string website)
    {
        var result = _validator.Validate(Valid() with { Website = website });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Website must be a valid http(s) URL.");
    }

    [Fact]
    public void Empty_Website_Is_Allowed()
        => _validator.Validate(Valid() with { Website = null }).IsValid.Should().BeTrue();
}
