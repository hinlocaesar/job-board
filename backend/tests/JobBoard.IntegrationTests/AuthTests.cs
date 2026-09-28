using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using JobBoard.IntegrationTests.Infrastructure;

namespace JobBoard.IntegrationTests;

/// <summary>Registration, verification, login and refresh-token flows.</summary>
public sealed class AuthTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_With_A_Valid_Payload_Returns_200()
    {
        var email = HttpFlows.NewEmail();

        var response = await _client.RegisterAsync(email, role: "worker");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var user = await response.ReadNodeAsync();
        user["email"]!.GetValue<string>().Should().Be(email);
        user["emailVerified"]!.GetValue<bool>().Should().BeFalse();
        user["roles"]!.AsArray().Should().ContainSingle();
        user["roles"]!.AsArray()[0]!.GetValue<string>().Should().Be("worker");
    }

    [Fact]
    public async Task Registering_The_Same_Twice_Returns_409()
    {
        var email = HttpFlows.NewEmail();
        (await _client.RegisterAsync(email, role: "employer")).StatusCode.Should().Be(HttpStatusCode.OK);

        var duplicate = await _client.RegisterAsync(email, role: "employer");

        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await duplicate.ProblemCodeAsync()).Should().Be("email_taken");
    }

    [Fact]
    public async Task Register_With_A_Weak_Password_Returns_400_Validation_Failed()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = HttpFlows.NewEmail(),
            password = "weak",
            fullName = "Weak Password",
            role = "worker",
            acceptPrivacy = true,
            privacyPolicyVersion = "2026-09-01",
            marketingConsent = false,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.ReadProblemAsync();
        problem["code"]!.GetValue<string>().Should().Be("validation_failed");
        problem["errors"].ShouldContainField("Password");
    }

    [Fact]
    public async Task Registering_Without_Privacy_Consent_Returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = HttpFlows.NewEmail(),
            password = HttpFlows.Password,
            fullName = "No Consent",
            role = "worker",
            acceptPrivacy = false,
            privacyPolicyVersion = "2026-09-01",
            marketingConsent = false,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ProblemCodeAsync()).Should().Be("validation_failed");
    }

    [Fact]
    public async Task Login_Before_Verifying_Email_Returns_401_Email_Not_Verified()
    {
        var email = HttpFlows.NewEmail();
        (await _client.RegisterAsync(email)).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = HttpFlows.Password });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.ProblemCodeAsync()).Should().Be("email_not_verified");
    }

    [Fact]
    public async Task Outbox_Contains_The_Verification_Code_Link()
    {
        var email = HttpFlows.NewEmail();
        (await _client.RegisterAsync(email)).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await _client.GetAsync("/api/auth/outbox");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var entry = (await response.ReadNodeAsync()).AsArray()
            .Select(message => message?.GetValue<string>() ?? string.Empty)
            .LastOrDefault(message => message.Contains(email, StringComparison.OrdinalIgnoreCase));

        entry.Should().NotBeNull("registering must write a message for the registered address");
        entry!.Should().Contain("code=");
        entry.Should().Contain("verify-email");
    }

    [Fact]
    public async Task VerifyEmail_Then_Login_Returns_Access_And_Refresh_Tokens()
    {
        var session = await RegisterVerifyAndLoginAsync();

        session.AccessToken.Should().NotBeNullOrWhiteSpace();
        session.RefreshToken.Should().NotBeNullOrWhiteSpace();
        session.User.EmailVerified.Should().BeTrue();
        session.User.Roles.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Refresh_With_A_Valid_Token_Issues_New_Tokens()
    {
        var session = await RegisterVerifyAndLoginAsync();

        var response = await _client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = session.RefreshToken });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rotated = await response.ReadAsAsync<AuthSessionDto>();
        rotated.AccessToken.Should().NotBe(session.AccessToken, "refresh rotates the access token");
        rotated.RefreshToken.Should().NotBe(session.RefreshToken, "refresh rotates the refresh token");
    }

    [Fact]
    public async Task Refresh_With_A_Garbage_Token_Returns_401()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = "not-a-real-refresh-token" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.ProblemCodeAsync()).Should().Be("invalid_refresh_token");
    }

    [Fact]
    public async Task Login_With_A_Wrong_Password_Returns_401_Invalid_Credentials()
    {
        var account = await _factory.CreateVerifiedAccountAsync("worker");

        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email = account.Email, password = "WrongPass123!" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.ProblemCodeAsync()).Should().Be("invalid_credentials");
    }

    private async Task<AuthSessionDto> RegisterVerifyAndLoginAsync()
    {
        var email = HttpFlows.NewEmail();
        (await _client.RegisterAsync(email)).StatusCode.Should().Be(HttpStatusCode.OK);

        var code = await _client.GetVerificationCodeAsync(email);
        await _client.VerifyEmailAsync(email, code);

        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = HttpFlows.Password });
        login.StatusCode.Should().Be(HttpStatusCode.OK,
            $"login after verification should succeed: {await login.Content.ReadAsStringAsync()}");
        return await login.ReadAsAsync<AuthSessionDto>();
    }
}
