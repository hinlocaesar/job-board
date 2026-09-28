using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using JobBoard.IntegrationTests.Infrastructure;

namespace JobBoard.IntegrationTests;

/// <summary>Worker end-to-end flow: profile → apply → employer reviews → shortlist → status visible.</summary>
public sealed class WorkerFlowTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public WorkerFlowTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Apply_Duplicate_Apply_Applicants_Shortlist_And_Mine_Flow()
    {
        // Published job owned by a dedicated employer.
        var categoryId = await _client.FirstCategoryIdAsync();
        var employer = await _factory.CreateEmployerAsync();
        var job = await employer.CreateJobAsync(categoryId);
        await _factory.ApproveJobAsync(job.Id);

        var worker = await _factory.CreateWorkerAsync();

        // 1. Apply.
        var apply = await worker.Client.PostAsJsonAsync($"/api/jobs/{job.Id}/apply",
            new { coverLetter = "I would love to work on this posting." });

        apply.StatusCode.Should().Be(HttpStatusCode.Created,
            $"applying to a published job should succeed: {await apply.Content.ReadAsStringAsync()}");
        var applicationId = Guid.Parse((await apply.ReadNodeAsync())["id"]!.GetValue<string>());

        // 2. Applying twice is a conflict.
        var duplicate = await worker.Client.PostAsJsonAsync($"/api/jobs/{job.Id}/apply",
            new { coverLetter = "One more time." });

        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await duplicate.ProblemCodeAsync()).Should().Be("already_applied");

        // 3. The employer lists applicants — contact details included.
        var applicants = await employer.Client.GetAsync($"/api/jobs/{job.Id}/applications");
        applicants.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = (await applicants.ReadNodeAsync()).AsArray();
        list.Should().ContainSingle();
        var applicant = list.Single()!;
        Guid.Parse(applicant["id"]!.GetValue<string>()).Should().Be(applicationId);
        applicant["email"]!.GetValue<string>().Should().Be(worker.Email,
            "the authenticated employer must see the applicant's contact email");
        applicant["status"]!.GetValue<string>().Should().Be("Submitted");
        applicant["coverLetter"]!.GetValue<string>().Should().Be("I would love to work on this posting.");

        // 4. Shortlist.
        var decide = await employer.Client.PatchAsJsonAsync($"/api/applications/{applicationId}",
            new { status = "Shortlisted", rejectionReason = (string?)null });

        decide.StatusCode.Should().Be(HttpStatusCode.OK);
        (await decide.ReadNodeAsync())["status"]!.GetValue<string>().Should().Be("Shortlisted");

        // 5. The worker sees the new status in /api/applications/mine.
        var mine = await worker.Client.GetAsync("/api/applications/mine");
        mine.StatusCode.Should().Be(HttpStatusCode.OK);

        var mineList = (await mine.ReadNodeAsync()).AsArray();
        var entry = mineList.FirstOrDefault(item => item?["id"]?.GetValue<string>() == applicationId.ToString());

        entry.Should().NotBeNull();
        entry!["status"]!.GetValue<string>().Should().Be("Shortlisted");
        entry["jobTitle"]!.GetValue<string>().Should().Be(job.Title);
        entry["jobStatus"]!.GetValue<string>().Should().Be("Published");
    }

    [Fact]
    public async Task Applying_Without_A_Worker_Profile_Returns_400()
    {
        var categoryId = await _client.FirstCategoryIdAsync();
        var job = await _factory.PublishJobAsync(categoryId);

        // Verified worker account that never created its profile.
        var worker = await _factory.CreateVerifiedAccountAsync("worker");

        var response = await worker.Client.PostAsJsonAsync($"/api/jobs/{job.Id}/apply", new { coverLetter = "Hello." });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ProblemCodeAsync()).Should().Be("worker_profile_required");
    }
}
