using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace JobBoard.IntegrationTests.Infrastructure;

/// <summary>Response of POST /api/auth/login (camel-cased JSON).</summary>
internal sealed record AuthSessionDto(string AccessToken, string RefreshToken, UserInfoDto User);

/// <summary>User fragment embedded in <see cref="AuthSessionDto"/>.</summary>
internal sealed record UserInfoDto(Guid Id, string Email, string? FullName, IReadOnlyList<string> Roles, bool EmailVerified, string AccountStatus);

/// <summary>A signed-in test account plus an HttpClient that already carries its bearer token.</summary>
internal sealed record TestAccount(string Email, AuthSessionDto Session, HttpClient Client, string? ProfileSlug = null)
{
    public string AccessToken => Session.AccessToken;
}

/// <summary>Small HTTP/JSON helpers shared by the integration tests.</summary>
internal static class HttpFlows
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Password used by every generated test account (satisfies Identity + FluentValidation).</summary>
    public const string Password = "Test1234!";

    public static HttpClient WithBearer(this HttpClient client, string accessToken)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    public static string NewEmail() => $"it-{Guid.NewGuid():N}@example.com";

    /// <summary>Unique single-word token used in job titles/titles for full-text search.</summary>
    public static string UniqueToken(string prefix) => prefix + Guid.NewGuid().ToString("N");

    // ---------- response parsing ----------

    public static async Task<JsonNode> ReadNodeAsync(this HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return JsonNode.Parse(text)
            ?? throw new InvalidOperationException($"Expected a JSON body but got none (HTTP {(int)response.StatusCode}).");
    }

    public static async Task<T> ReadAsAsync<T>(this HttpResponseMessage response) where T : class
    {
        var text = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(text, Json)
            ?? throw new InvalidOperationException($"Could not deserialize {typeof(T).Name} (HTTP {(int)response.StatusCode}).");
    }

    /// <summary>Parses an RFC 7807-style error body and asserts it carries the "code" extension.</summary>
    public static async Task<JsonNode> ReadProblemAsync(this HttpResponseMessage response)
    {
        var node = await response.ReadNodeAsync();
        node["code"].Should().NotBeNull("every error response must carry an RFC 7807 'code' extension");
        return node;
    }

    public static async Task<string> ProblemCodeAsync(this HttpResponseMessage response)
        => (await response.ReadProblemAsync())["code"]!.GetValue<string>();

    /// <summary>JSON property lookup that treats both "missing" and explicit null as null.</summary>
    public static string? GetStringOrNull(this JsonNode node, string property)
    {
        var value = node[property];
        if (value is null)
            return null;

        return value.GetValueKind() == JsonValueKind.Null ? null : value.GetValue<string>();
    }

    /// <summary>Asserts the problem-details "errors" object reports a given field (case-insensitive).</summary>
    public static void ShouldContainField(this JsonNode? errors, string field)
    {
        errors.Should().NotBeNull("problem details must include an 'errors' extension for validation failures");
        errors!.AsObject().Select(pair => pair.Key)
            .Should().Contain(name => string.Equals(name, field, StringComparison.OrdinalIgnoreCase),
                $"the validation errors must report the '{field}' field");
    }

    // ---------- auth flows ----------

    public static Task<HttpResponseMessage> RegisterAsync(
        this HttpClient client,
        string email,
        string password = Password,
        string role = "worker",
        string fullName = "Integration Tester")
        => client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password,
            fullName,
            role,
            acceptPrivacy = true,
            privacyPolicyVersion = "2026-09-01",
            marketingConsent = false,
        });

    /// <summary>Reads the dev outbox (logs/outbox.log) and extracts the verification code sent to <paramref name="email"/>.</summary>
    public static async Task<string> GetVerificationCodeAsync(this HttpClient client, string email)
    {
        var response = await client.GetAsync("/api/auth/outbox");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        var entries = (await response.ReadNodeAsync()).AsArray()
            .Select(entry => entry?.GetValue<string>() ?? string.Empty)
            .Where(entry => entry.Contains(email, StringComparison.OrdinalIgnoreCase))
            .ToList();

        entries.Should().NotBeEmpty($"registering {email} must write a verification message to the dev outbox");

        var codeMatch = Regex.Match(entries[^1], @"[?&]code=([0-9a-f]+)");
        codeMatch.Success.Should().BeTrue("the outbox message must contain a clickable code link");
        return codeMatch.Groups[1].Value;
    }

    public static async Task VerifyEmailAsync(this HttpClient client, string email, string code)
    {
        var response = await client.PostAsJsonAsync("/api/auth/verify-email", new { email, code });
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NoContent);
    }

    public static async Task<AuthSessionDto> LoginAsync(this HttpClient client, string email, string password = Password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        return await response.ReadAsAsync<AuthSessionDto>();
    }

    /// <summary>Register → verify via the dev outbox → login. The returned account has an authenticated client.</summary>
    public static async Task<TestAccount> CreateVerifiedAccountAsync(this CustomWebApplicationFactory factory, string role)
    {
        var anonymous = factory.CreateClient();
        var email = NewEmail();

        var register = await anonymous.RegisterAsync(email, role: role);
        register.StatusCode.Should().Be(System.Net.HttpStatusCode.OK,
            $"registering a fresh {role} account should succeed: {await register.Content.ReadAsStringAsync()}");

        var code = await anonymous.GetVerificationCodeAsync(email);
        await anonymous.VerifyEmailAsync(email, code);

        var session = await anonymous.LoginAsync(email);
        session.User.Email.Should().Be(email);
        session.User.EmailVerified.Should().BeTrue();
        session.User.Roles.Should().Contain(role);

        return new TestAccount(email, session, factory.CreateClient().WithBearer(session.AccessToken));
    }

    // ---------- public helpers ----------

    /// <summary>Returns the categories array; the seeded marketplace must always have some.</summary>
    public static async Task<JsonArray> GetCategoriesAsync(this HttpClient client)
    {
        var response = await client.GetAsync("/api/categories");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        return (await response.ReadNodeAsync()).AsArray();
    }

    public static async Task<Guid> FirstCategoryIdAsync(this HttpClient client, string? slug = null)
    {
        var categories = await client.GetCategoriesAsync();
        var match = slug is null
            ? categories.FirstOrDefault()
            : categories.FirstOrDefault(category => category?["slug"]?.GetValue<string>() == slug);

        match.Should().NotBeNull(slug is null
            ? "the startup seed must create at least one category"
            : $"the category '{slug}' must exist");

        return Guid.Parse(match!["id"]!.GetValue<string>());
    }
}
