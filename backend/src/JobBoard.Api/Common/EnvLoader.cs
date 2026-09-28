namespace JobBoard.Api.Common;

/// <summary>
/// Loads the repo-root <c>.env</c> (gitignored) into process environment variables
/// before the configuration is built, so local secrets never land in the repo.
/// Real deployments set real environment variables instead.
/// </summary>
public static class EnvLoader
{
    public static void LoadUpwards(params string[] startDirectories)
    {
        foreach (var start in startDirectories.Where(d => !string.IsNullOrWhiteSpace(d)).Distinct())
        {
            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                var file = Path.Combine(dir.FullName, ".env");
                if (File.Exists(file))
                {
                    Load(file);
                    return;
                }
                dir = dir.Parent;
            }
        }
    }

    private static void Load(string path)
    {
        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var separator = line.IndexOf('=');
            if (separator <= 0)
                continue;

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim().Trim('"');

            // Real environment variables always win over the file.
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                Environment.SetEnvironmentVariable(key, value);
        }
    }
}
