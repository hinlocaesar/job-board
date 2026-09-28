using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using JobBoard.IntegrationTests.Infrastructure;

namespace JobBoard.IntegrationTests;

/// <summary>FluentValidation failures must surface as RFC 7807 bodies with per-field errors.</summary>
public sealed class ValidationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ValidationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Posting_A_Job_With_An_Invalid_Payload_Returns_400_Validation_Failed()
    {
        var employer = await _factory.CreateEmployerAsync();
        var categoryId = await _client.FirstCategoryIdAsync();

        var payload = new
        {
            title = "No",                                  // too short (< 5)
            description = "too short",                     // too short (< 20)
            categoryId,
            jobType = "Bogus",                             // unknown enum
            region = "Worldwide",
            payType = "Hourly",
            payMin = 500,
            payMax = 100,                                  // max < min
            currency = "US",                               // not 3 chars
            experienceLevel = "Mid",
            hoursPerWeek = (short?)40,
            closesAt = (DateTime?)null,
            skills = Array.Empty<string>(),
        };

        var response = await employer.Client.PostAsJsonAsync("/api/jobs", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.ReadProblemAsync();
        problem["code"]!.GetValue<string>().Should().Be("validation_failed");

        var errors = problem["errors"];
        errors.ShouldContainField("Title");
        errors.ShouldContainField("Description");
        errors.ShouldContainField("JobType");
        errors.ShouldContainField("PayMax");
        errors.ShouldContainField("Currency");
    }

    [Fact]
    public async Task Creating_A_Worker_Profile_With_An_Invalid_Payload_Returns_400_Validation_Failed()
    {
        var worker = await _factory.CreateVerifiedAccountAsync("worker");

        var payload = new
        {
            headline = "abc",            // too short (< 4)
            summary = (string?)null,
            country = "PHL",             // longer than 2
            city = "Cebu",
            timeZone = "Asia/Manila",
            yearsOfExperience = (short)70, // > 60
            rateMin = 50,
            rateMax = 10,                // max < min
            currency = "USD",
            ratePeriod = "Fortnight",    // unknown enum
            availability = "Freelance",
            isPublic = true,
            resumeFileId = (Guid?)null,
            skills = Array.Empty<object>(),
            experiences = Array.Empty<object>(),
        };

        var response = await worker.Client.PostAsJsonAsync("/api/profiles/worker", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.ReadProblemAsync();
        problem["code"]!.GetValue<string>().Should().Be("validation_failed");

        var errors = problem["errors"];
        errors.ShouldContainField("Headline");
        errors.ShouldContainField("Country");
        errors.ShouldContainField("YearsOfExperience");
        errors.ShouldContainField("RateMax");
        errors.ShouldContainField("RatePeriod");
    }

    [Fact]
    public async Task Error_Responses_Are_Served_With_The_Application_Problem_Json_Media_Type()
    {
        // PRODUCT-BUG: ExceptionHandlingMiddleware sets
        //     context.Response.ContentType = "application/problem+json";
        // immediately before Response.WriteAsJsonAsync(...), but ASP.NET Core's
        // WriteAsJsonAsync unconditionally overwrites ContentType with
        // "application/json; charset=utf-8"
        // (HttpResponseJsonExtensions: response.ContentType = contentType ?? JsonContentTypeWithCharset).
        // The RFC 7807 body (status/title/detail/code/errors) is correct, so clients that
        // parse JSON still work — but the advertised media type is plain application/json,
        // which breaks clients that content-negotiate on application/problem+json.
        // Fix: pass the media type explicitly, e.g.
        //     await context.Response.WriteAsJsonAsync(payload, options, "application/problem+json");
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = "nobody-" + Guid.NewGuid().ToString("N") + "@example.com", password = HttpFlows.Password });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.ProblemCodeAsync()).Should().Be("invalid_credentials");
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }
}
