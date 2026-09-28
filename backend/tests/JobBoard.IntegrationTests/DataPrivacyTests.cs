using System.Net;
using FluentAssertions;
using JobBoard.IntegrationTests.Infrastructure;

namespace JobBoard.IntegrationTests;

/// <summary>Data-privacy rule: contact details of a public worker profile are only
/// exposed to authenticated callers, never to anonymous visitors.</summary>
public sealed class DataPrivacyTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public DataPrivacyTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Anonymous_Visitor_Get_Public_Worker_Profile_Has_No_Contact_Email()
    {
        var worker = await _factory.CreateWorkerAsync(isPublic: true);
        worker.ProfileSlug.Should().NotBeNullOrWhiteSpace();

        var response = await _client.GetAsync($"/api/profiles/worker/{worker.ProfileSlug}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await response.ReadNodeAsync();
        profile["isPublic"]!.GetValue<bool>().Should().BeTrue();
        profile.GetStringOrNull("contactEmail").Should().BeNull("anonymous visitors must not see contact details");
        profile.GetStringOrNull("contactPhone").Should().BeNull();
    }

    [Fact]
    public async Task Authenticated_Caller_Get_Public_Worker_Profile_Sees_The_Contact_Email()
    {
        var worker = await _factory.CreateWorkerAsync(isPublic: true);

        var adminSession = await _factory.LoginAsync(SeedUsers.AdminEmail, SeedUsers.AdminPassword);
        var adminClient = _factory.CreateAuthedClient(adminSession);

        var response = await adminClient.GetAsync($"/api/profiles/worker/{worker.ProfileSlug}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await response.ReadNodeAsync();
        profile.GetStringOrNull("contactEmail").Should().Be(worker.Email,
            "an authenticated caller may see the contact email of a public profile");
    }
}
