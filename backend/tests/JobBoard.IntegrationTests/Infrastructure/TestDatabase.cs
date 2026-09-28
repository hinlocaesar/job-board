namespace JobBoard.IntegrationTests.Infrastructure;

/// <summary>
/// Resolves the connection string for the PostgreSQL TEST database (<c>jobboard_test</c>).
/// Reads the <c>ConnectionStrings__MarketplaceTest</c> environment variable first and falls
/// back to the repo-root <c>.env</c> file (the same one the API loads at startup).
/// Deliberately never falls back to the dev <c>jobboard</c> database.
/// </summary>
internal static class TestDatabase
{
    public const string TestKey = "ConnectionStrings__MarketplaceTest";

    public static string ConnectionString { get; } = Resolve();

    private static string Resolve()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(TestKey);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
            return Validate(fromEnvironment);

        // Mirror EnvLoader.LoadUpwards: walk up from the working directory first,
        // then from the test assembly's own directory.
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                var path = Path.Combine(directory.FullName, ".env");
                if (File.Exists(path))
                {
                    var value = ReadValue(path, TestKey) ?? ReadValue(path, "ConnectionStrings_MarketplaceTest");
                    if (!string.IsNullOrWhiteSpace(value))
                        return Validate(value);
                }

                directory = directory.Parent;
            }
        }

        throw new InvalidOperationException(
            $"No test database connection string found. Set the {TestKey} environment variable " +
            "or add ConnectionStrings__MarketplaceTest to the repo-root .env file.");
    }

    private static string Validate(string connectionString)
    {
        var database = connectionString
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Trim().Split('=', 2))
            .FirstOrDefault(parts => parts.Length == 2 && parts[0].Equals("Database", StringComparison.OrdinalIgnoreCase))
            ?[1];

        if (database is null || !database.Contains("test", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Refusing to run integration tests against database '{database ?? "<missing>"}'. " +
                "They must target jobboard_test, never the dev database.");

        return connectionString;
    }

    private static string? ReadValue(string path, string key)
    {
        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var separator = line.IndexOf('=');
            if (separator <= 0)
                continue;

            if (line[..separator].Trim() == key)
                return line[(separator + 1)..].Trim().Trim('"');
        }

        return null;
    }
}
