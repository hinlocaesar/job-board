using System.Net;
using FluentAssertions;
using JobBoard.Infrastructure.Data;
using JobBoard.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace JobBoard.IntegrationTests;

/// <summary>Smoke tests: the host boots against the test database and the basics respond.</summary>
public sealed class SmokeTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SmokeTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_Endpoint_Returns_Ok()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadNodeAsync();
        body["status"]!.GetValue<string>().Should().Be("ok");
    }

    [Fact]
    public void Factory_Runs_In_Development_Against_The_Test_Database()
    {
        using var scope = _factory.Server.Services.CreateScope();

        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        environment.IsDevelopment().Should().BeTrue("demo seeding and the dev outbox require Development");

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var connectionString = db.Database.GetConnectionString();
        connectionString.Should().NotBeNullOrWhiteSpace();
        connectionString!.Should().Contain("jobboard_test",
            "integration tests must never touch the dev 'jobboard' database");
    }

    [Fact]
    public async Task Categories_Return_A_Non_Empty_List()
    {
        var response = await _client.GetAsync("/api/categories");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var categories = (await response.ReadNodeAsync()).AsArray();

        categories.Should().NotBeEmpty("DbSeeder must seed the marketplace categories on startup");
        categories.Should().OnlyContain(category =>
            !string.IsNullOrEmpty(category!["name"]!.GetValue<string>()) &&
            !string.IsNullOrEmpty(category!["slug"]!.GetValue<string>()));
    }

    [Fact]
    public async Task Dev_Outbox_Endpoint_Is_Available_In_Development()
    {
        var response = await _client.GetAsync("/api/auth/outbox");

        response.StatusCode.Should().Be(HttpStatusCode.OK, "the outbox is Development-only and the factory pins Development");
        (await response.ReadNodeAsync()).AsArray().Should().NotBeNull();
    }
}
