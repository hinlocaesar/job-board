using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace JobBoard.IntegrationTests.Infrastructure;

/// <summary>Higher-level marketplace flows: profiles, job creation, moderation.</summary>
internal static class MarketplaceFlows
{
    public sealed record TestJob(Guid Id, string Slug, string Title);

    // ---------- profiles ----------

    /// <summary>Registers a verified employer account and creates its employer profile.</summary>
    public static async Task<TestAccount> CreateEmployerAsync(this CustomWebApplicationFactory factory)
    {
        var account = await factory.CreateVerifiedAccountAsync("employer");

        var response = await account.Client.PostAsJsonAsync("/api/profiles/employer", new
        {
            companyName = $"Test Co {Guid.NewGuid():N}",
            website = "https://example.com",
            description = "Integration test company profile.",
            country = "PH",
            logoFileId = (Guid?)null,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            $"creating an employer profile should succeed: {await response.Content.ReadAsStringAsync()}");
        var slug = (await response.ReadNodeAsync())["slug"]!.GetValue<string>();

        return account with { ProfileSlug = slug };
    }

    /// <summary>Registers a verified worker account and creates its (optionally public) worker profile.</summary>
    public static async Task<TestAccount> CreateWorkerAsync(this CustomWebApplicationFactory factory, bool isPublic = true)
    {
        var account = await factory.CreateVerifiedAccountAsync("worker");

        var response = await account.Client.PostAsJsonAsync("/api/profiles/worker", new
        {
            headline = "Integration Test Worker",
            summary = "Available for marketplace integration test scenarios.",
            country = "PH",
            city = "Cebu",
            timeZone = "Asia/Manila",
            yearsOfExperience = 3,
            rateMin = 10,
            rateMax = 25,
            currency = "USD",
            ratePeriod = "Hour",
            availability = "Freelance",
            isPublic,
            resumeFileId = (Guid?)null,
            skills = new[] { new { name = "Testing", level = (byte)4, yearsExperience = (short)3 } },
            experiences = Array.Empty<object>(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            $"creating a worker profile should succeed: {await response.Content.ReadAsStringAsync()}");
        var slug = (await response.ReadNodeAsync())["slug"]!.GetValue<string>();

        return account with { ProfileSlug = slug };
    }

    // ---------- jobs ----------

    /// <summary>A valid POST /api/jobs payload with the given identity fields.</summary>
    public static object NewJobPayload(Guid categoryId, string title, string description) => new
    {
        title,
        description,
        categoryId,
        jobType = "FullTime",
        region = "Worldwide",
        payType = "Hourly",
        payMin = 20,
        payMax = 40,
        currency = "USD",
        experienceLevel = "Mid",
        hoursPerWeek = (short?)40,
        closesAt = (DateTime?)null,
        skills = new[] { "Testing", "Automation" },
    };

    /// <summary>Employer posts a job; it must come back as Pending (not yet publicly visible).</summary>
    public static async Task<TestJob> CreateJobAsync(this TestAccount employer, Guid categoryId, string? token = null)
    {
        token ??= HttpFlows.UniqueToken("job");
        var title = $"Senior Platform Engineer {token}";
        var description =
            $"We are hiring a platform engineer. Quote reference {token} when you apply. " +
            "This description intentionally exceeds the twenty character minimum.";

        var response = await employer.Client.PostAsJsonAsync(
            "/api/jobs", NewJobPayload(categoryId, title, description));

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            $"posting a job should succeed: {await response.Content.ReadAsStringAsync()}");

        var body = await response.ReadNodeAsync();
        var slug = body["slug"]!.GetValue<string>();
        body["published"]!.GetValue<bool>().Should().BeFalse("new postings must enter the moderation queue");
        body["status"]!.GetValue<string>().Should().Be("Pending");

        var id = await FindOwnJobIdAsync(employer.Client, slug);
        return new TestJob(id, slug, title);
    }

    /// <summary>Resolves the job id through the employer's own list (visible while Pending).</summary>
    private static async Task<Guid> FindOwnJobIdAsync(HttpClient employerClient, string slug)
    {
        var response = await employerClient.GetAsync("/api/jobs/mine");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var match = (await response.ReadNodeAsync()).AsArray()
            .FirstOrDefault(job => job?["slug"]?.GetValue<string>() == slug);

        match.Should().NotBeNull($"GET /api/jobs/mine must contain {slug}");
        return Guid.Parse(match!["id"]!.GetValue<string>());
    }

    // ---------- moderation ----------

    public static async Task<AuthSessionDto> LoginAsync(this CustomWebApplicationFactory factory, string email, string password)
    {
        var anonymous = factory.CreateClient();
        return await anonymous.LoginAsync(email, password);
    }

    /// <summary>A new HttpClient for an existing session (bearer token pre-set).</summary>
    public static HttpClient CreateAuthedClient(this CustomWebApplicationFactory factory, AuthSessionDto session)
        => factory.CreateClient().WithBearer(session.AccessToken);

    public static async Task ApproveJobAsync(this CustomWebApplicationFactory factory, Guid jobId)
    {
        var adminSession = await factory.LoginAsync(SeedUsers.AdminEmail, SeedUsers.AdminPassword);
        adminSession.User.Roles.Should().Contain("admin");
        var adminClient = factory.CreateAuthedClient(adminSession);

        var response = await adminClient.PostAsync($"/api/admin/jobs/{jobId}/approve", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"approving job {jobId} should succeed: {await response.Content.ReadAsStringAsync()}");
        (await response.ReadNodeAsync())["status"]!.GetValue<string>().Should().Be("Published");
    }

    /// <summary>Full employer→admin flow: employer + profile + pending job, then admin approval.</summary>
    public static async Task<TestJob> PublishJobAsync(this CustomWebApplicationFactory factory, Guid categoryId, string? token = null)
    {
        var employer = await factory.CreateEmployerAsync();
        var job = await employer.CreateJobAsync(categoryId, token);
        await factory.ApproveJobAsync(job.Id);
        return job;
    }
}
