using System.Net;
using FluentAssertions;
using JobBoard.IntegrationTests.Infrastructure;

namespace JobBoard.IntegrationTests;

/// <summary>The public job board: response shape, full-text search, category filter, pagination.</summary>
public sealed class PublicJobsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public PublicJobsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Job_List_Returns_The_Paginated_Search_Shape()
    {
        var response = await _client.GetAsync("/api/jobs");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadNodeAsync();

        foreach (var property in new[] { "items", "totalCount", "page", "pageSize", "totalPages" })
            body[property].Should().NotBeNull($"the response must expose '{property}'");

        body["page"]!.GetValue<int>().Should().Be(1);
        body["pageSize"]!.GetValue<int>().Should().Be(10);

        var total = body["totalCount"]!.GetValue<int>();
        var pageSize = body["pageSize"]!.GetValue<int>();
        body["totalPages"]!.GetValue<int>().Should().Be((int)Math.Ceiling(total / (double)pageSize));
        body["items"]!.AsArray().Count.Should().BeLessThanOrEqualTo(pageSize);

        total.Should().BeGreaterThanOrEqualTo(1, "the startup seed publishes sample jobs");
        var firstItem = body["items"]!.AsArray()[0]!;
        firstItem["slug"].Should().NotBeNull();
        firstItem["title"].Should().NotBeNull();
        firstItem["companyName"].Should().NotBeNull();
        firstItem["categorySlug"].Should().NotBeNull();
    }

    [Fact]
    public async Task Full_Text_Search_Filters_By_The_Q_Parameter()
    {
        var categoryId = await _client.FirstCategoryIdAsync();
        var token = HttpFlows.UniqueToken("srch");

        // Nothing matches before the job exists.
        var before = await _client.GetAsync($"/api/jobs?q={token}");
        (await before.ReadNodeAsync())["totalCount"]!.GetValue<int>().Should().Be(0);

        var job = await _factory.PublishJobAsync(categoryId, token);

        var after = await _client.GetAsync($"/api/jobs?q={token}");
        var result = await after.ReadNodeAsync();

        result["totalCount"]!.GetValue<int>().Should().Be(1, $"only our own '{token}' job may match");
        var items = result["items"]!.AsArray();
        items.Should().ContainSingle();
        items[0]!["slug"]!.GetValue<string>().Should().Be(job.Slug);
        items[0]!["title"]!.GetValue<string>().Should().Contain(token);
    }

    [Fact]
    public async Task Category_Filter_Returns_Only_That_Category()
    {
        const string categorySlug = "web-development";
        var categoryId = await _client.FirstCategoryIdAsync(categorySlug);
        var job = await _factory.PublishJobAsync(categoryId);

        var response = await _client.GetAsync($"/api/jobs?category={categorySlug}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.ReadNodeAsync();
        var items = result["items"]!.AsArray();

        items.Should().NotBeEmpty();
        items.Should().OnlyContain(item => item!["categorySlug"]!.GetValue<string>() == categorySlug);
        items[0]!["slug"]!.GetValue<string>().Should().Be(job.Slug,
            "the most recently approved job sorts first");
    }

    [Fact]
    public async Task Unknown_Category_Slug_Returns_404()
    {
        var response = await _client.GetAsync("/api/jobs?category=does-not-exist-xyz");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.ProblemCodeAsync()).Should().Be("not_found");
    }

    [Fact]
    public async Task Pagination_Page_2_With_PageSize_1_Behaves()
    {
        var first = await _client.GetAsync("/api/jobs?page=1&pageSize=1");
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var firstBody = await first.ReadNodeAsync();
        var total = firstBody["totalCount"]!.GetValue<int>();
        firstBody["page"]!.GetValue<int>().Should().Be(1);
        firstBody["pageSize"]!.GetValue<int>().Should().Be(1);
        firstBody["totalPages"]!.GetValue<int>().Should().Be(total, "page size 1 => one page per job");
        var firstItems = firstBody["items"]!.AsArray();
        firstItems.Should().HaveCount(1);

        var second = await _client.GetAsync("/api/jobs?page=2&pageSize=1");
        second.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondBody = await second.ReadNodeAsync();
        secondBody["page"]!.GetValue<int>().Should().Be(2);
        secondBody["pageSize"]!.GetValue<int>().Should().Be(1);
        secondBody["totalCount"]!.GetValue<int>().Should().Be(total);
        var secondItems = secondBody["items"]!.AsArray();

        if (total >= 2)
        {
            secondItems.Should().HaveCount(1);
            secondItems[0]!["slug"]!.GetValue<string>()
                .Should().NotBe(firstItems[0]!["slug"]!.GetValue<string>(), "pages must not repeat jobs");
        }
        else
        {
            secondItems.Should().BeEmpty();
        }
    }
}
