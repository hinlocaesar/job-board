using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using JobBoard.IntegrationTests.Infrastructure;

namespace JobBoard.IntegrationTests;

/// <summary>Authentication and role-based authorization on POST /api/jobs.</summary>
public sealed class AuthorizationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthorizationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Posting_A_Job_Without_A_Token_Returns_401()
    {
        var categoryId = await _client.FirstCategoryIdAsync();

        var response = await _client.PostAsJsonAsync("/api/jobs",
            MarketplaceFlows.NewJobPayload(categoryId, "Anonymous Job Title", "A perfectly valid description body."));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Posting_A_Job_As_A_Worker_Returns_403()
    {
        var worker = await _factory.CreateVerifiedAccountAsync("worker");
        var categoryId = await _client.FirstCategoryIdAsync();

        var response = await worker.Client.PostAsJsonAsync("/api/jobs",
            MarketplaceFlows.NewJobPayload(categoryId, "Worker Should Not Post", "A perfectly valid description body."));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Posting_A_Job_Without_An_Employer_Profile_Returns_400_Employer_Profile_Required()
    {
        // Verified employer account that has NOT created its employer profile yet.
        var employer = await _factory.CreateVerifiedAccountAsync("employer");
        var categoryId = await _client.FirstCategoryIdAsync();

        var response = await employer.Client.PostAsJsonAsync("/api/jobs",
            MarketplaceFlows.NewJobPayload(categoryId, "No Profile Yet Job", "A perfectly valid description body."));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ProblemCodeAsync()).Should().Be("employer_profile_required");
    }
}
