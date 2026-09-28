using FluentAssertions;
using JobBoard.Api.Features.Auth;

namespace JobBoard.UnitTests;

public sealed class RegisterRequestValidatorTests
{
    private readonly RegisterRequestValidator _validator = new();

    private static RegisterRequest Valid() => new(
        Email: "new.user@example.com",
        Password: "Str0ngPass!",
        FullName: "New User",
        Role: "employer",
        AcceptPrivacy: true,
        PrivacyPolicyVersion: "2026-09-01",
        MarketingConsent: false);

    [Fact]
    public void Valid_Request_Passes()
    {
        var result = _validator.Validate(Valid());

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Worker_Role_Is_Accepted_Case_Insensitively()
        => _validator.Validate(Valid() with { Role = "WORKER" }).IsValid.Should().BeTrue();

    [Theory]
    [InlineData("admin")]
    [InlineData("superadmin")]
    [InlineData("moderator")]
    public void Only_Signup_Roles_Are_Allowed(string role)
    {
        var result = _validator.Validate(Valid() with { Role = role });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.StartsWith("Role must be one of"));
    }

    [Fact]
    public void Privacy_Consent_Is_Mandatory()
    {
        var result = _validator.Validate(Valid() with { AcceptPrivacy = false });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "You must accept the privacy policy to register.");
    }

    [Theory]
    [InlineData("weak1A")]       // too short (< 8)
    [InlineData("alllowercase1")] // no uppercase
    [InlineData("ALLUPPERCASE1")] // no lowercase
    [InlineData("NoDigitsAtAll")] // no digit
    [InlineData("Sh0")]           // way too short
    public void Weak_Passwords_Are_Rejected(string password)
    {
        var result = _validator.Validate(Valid() with { Password = password });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterRequest.Password));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("@example.com")]
    [InlineData("")]
    public void Invalid_Emails_Are_Rejected(string email)
    {
        var result = _validator.Validate(Valid() with { Email = email });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterRequest.Email));
    }

    [Fact]
    public void Empty_FullName_Is_Rejected()
        => _validator.Validate(Valid() with { FullName = "" }).IsValid.Should().BeFalse();

    [Fact]
    public void Missing_Role_Is_Rejected()
        => _validator.Validate(Valid() with { Role = "" }).IsValid.Should().BeFalse();
}
