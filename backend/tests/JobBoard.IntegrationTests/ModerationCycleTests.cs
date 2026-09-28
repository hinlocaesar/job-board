using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using JobBoard.IntegrationTests.Infrastructure;

namespace JobBoard.IntegrationTests;

/// <summary>The most valuable end-to-end path: employer posts → moderation → public board.</summary>
public sealed class ModerationCycleTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ModerationCycleTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Job_Is_Invisible_Until_An_Admin_Approves_It_Then_Appears_Publicly()
    {
        var token = HttpFlows.UniqueToken("cycle");
        var categoryId = await _client.FirstCategoryIdAsync();

        // 1. Employer creates a profile and posts a job.
        var employer = await _factory.CreateEmployerAsync();
        var job = await employer.CreateJobAsync(categoryId, token);

        // 2. While Pending it is invisible to anonymous visitors...
        var detailBefore = await _client.GetAsync($"/api/jobs/{job.Slug}");
        detailBefore.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await detailBefore.ProblemCodeAsync()).Should().Be("not_found");

        var searchBefore = await _client.GetAsync($"/api/jobs?q={token}");
        (await searchBefore.ReadNodeAsync())["totalCount"]!.GetValue<int>()
            .Should().Be(0, "a pending job must not show up in the public search");

        // 3. ...but it sits in the admin moderation queue.
        var adminSession = await _factory.LoginAsync(SeedUsers.AdminEmail, SeedUsers.AdminPassword);
        adminSession.User.Roles.Should().Contain("admin");
        var adminClient = _factory.CreateAuthedClient(adminSession);

        var queue = await adminClient.GetAsync("/api/admin/jobs");
        queue.StatusCode.Should().Be(HttpStatusCode.OK);
        var queued = (await queue.ReadNodeAsync()).AsArray()
            .FirstOrDefault(entry => entry?["id"]?.GetValue<string>() == job.Id.ToString());

        queued.Should().NotBeNull("the pending job must appear in GET /api/admin/jobs");
        queued!["status"]!.GetValue<string>().Should().Be("Pending");
        queued["slug"]!.GetValue<string>().Should().Be(job.Slug);

        // 4. Admin approves it.
        var approve = await adminClient.PostAsync($"/api/admin/jobs/{job.Id}/approve", null);
        approve.StatusCode.Should().Be(HttpStatusCode.OK);
        (await approve.ReadNodeAsync())["status"]!.GetValue<string>().Should().Be("Published");

        // 5. Now anyone can read it by slug...
        var detailAfter = await _client.GetAsync($"/api/jobs/{job.Slug}");
        detailAfter.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await detailAfter.ReadNodeAsync();
        detail["status"]!.GetValue<string>().Should().Be("Published");
        detail["slug"]!.GetValue<string>().Should().Be(job.Slug);
        detail["canEdit"]!.GetValue<bool>().Should().BeFalse("an anonymous visitor cannot edit");

        // ...and it appears in the public list.
        var searchAfter = await _client.GetAsync($"/api/jobs?q={token}");
        var result = await searchAfter.ReadNodeAsync();
        result["totalCount"]!.GetValue<int>().Should().Be(1);
        var items = result["items"]!.AsArray();
        items.Should().ContainSingle();
        items[0]!["slug"]!.GetValue<string>().Should().Be(job.Slug);
    }

    [Fact]
    public async Task Employer_Sees_Their_Job_As_Pending_In_My_Jobs()
    {
        var categoryId = await _client.FirstCategoryIdAsync();
        var employer = await _factory.CreateEmployerAsync();
        var job = await employer.CreateJobAsync(categoryId);

        var response = await employer.Client.GetAsync("/api/jobs/mine");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var mine = (await response.ReadNodeAsync()).AsArray();
        var entry = mine.FirstOrDefault(item => item?["id"]?.GetValue<string>() == job.Id.ToString());

        entry.Should().NotBeNull();
        entry!["status"]!.GetValue<string>().Should().Be("Pending");
        entry["title"]!.GetValue<string>().Should().Be(job.Title);
    }
}
