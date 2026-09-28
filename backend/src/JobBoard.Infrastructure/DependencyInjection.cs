using JobBoard.Domain.Abstractions;
using JobBoard.Infrastructure.Data;
using JobBoard.Infrastructure.Email;
using JobBoard.Infrastructure.Search;
using JobBoard.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JobBoard.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, string contentRootPath)
    {
        var connectionString = configuration.GetConnectionString("Marketplace")
            ?? throw new InvalidOperationException("Connection string 'Marketplace' is missing.");

        services.AddDbContext<AppDbContext>(options => options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention());

        // Full-text search behind an interface so it can be swapped later (Prompt: NON-FUNCTIONAL).
        services.AddScoped<IJobSearchService, PostgresJobSearchService>();

        // Scoped, not singleton: it depends on the scoped AppDbContext.
        services.AddScoped<IFileStorage>(sp => new LocalFileStorage(
            sp.GetRequiredService<AppDbContext>(),
            sp.GetRequiredService<ILogger<LocalFileStorage>>(),
            Path.Combine(contentRootPath, configuration["Storage:RootPath"] ?? "storage")));

        services.AddSingleton<IEmailSender>(sp => new EmailSender(
            configuration,
            sp.GetRequiredService<ILogger<EmailSender>>(),
            Path.Combine(contentRootPath, "logs", "outbox.log")));

        return services;
    }
}
