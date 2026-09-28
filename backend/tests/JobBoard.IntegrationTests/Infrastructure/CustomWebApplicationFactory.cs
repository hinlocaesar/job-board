using JobBoard.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace JobBoard.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the real <c>Program</c> in an in-memory TestServer (no fixed port) against the
/// <c>jobboard_test</c> database:
/// <list type="bullet">
/// <item>Environment is pinned to Development so DbSeeder seeds roles/categories/demo users
/// and the dev-only <c>/api/auth/outbox</c> endpoint exists.</item>
/// <item>Configuration is overridden: test connection string, seeded demo users, high rate
/// limits (otherwise parallel tests trip the per-IP 10/min auth policy) and a temp storage root.</item>
/// <item>Process environment variables are pinned before the first host starts, because the app
/// loads the repo-root .env itself and .env values only apply when the variable is not set —
/// this guarantees tests can never reach the dev database or a real SMTP server.</item>
/// </list>
/// EF migrations are applied by the app's own DbSeeder (<c>Database.MigrateAsync()</c>) on startup.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    static CustomWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__Marketplace", TestDatabase.ConnectionString);
        Environment.SetEnvironmentVariable("Email__Provider", "Log");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            // Added after the default sources (appsettings/env vars), so these values win.
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Marketplace"] = TestDatabase.ConnectionString,
                ["Seed:DemoUsers"] = "true",
                // Dev defaults (10/120/300 per minute) would throttle a test run with 429s.
                ["RateLimit:AuthPermit"] = "10000",
                ["RateLimit:PublicPermit"] = "10000",
                ["RateLimit:GlobalPermit"] = "10000",
                ["App:FrontendUrl"] = "http://localhost:5173",
                ["Email:Provider"] = "Log",
                // Rooted path: Path.Combine(contentRoot, rooted) == rooted, so uploads land in
                // the temp directory instead of src/JobBoard.Api/storage.
                ["Storage:RootPath"] = Path.Combine(Path.GetTempPath(), "jobboard-integration-tests", "storage"),
            });
        });
    }
}
