namespace PictureSite.Api.Admin;

public static class LocalAdminEnvironment
{
    private const string PasswordKey = "Admin__Password";

    public static void Load(string contentRoot)
    {
        if (Environment.GetEnvironmentVariable(PasswordKey) is not null) return;

        var candidateFiles = new[]
        {
            Path.Combine(contentRoot, ".env"),
            Path.Combine(contentRoot, "backend", ".env"),
        };

        var envFile = candidateFiles.FirstOrDefault(File.Exists);
        if (envFile is null) return;

        foreach (var rawLine in File.ReadLines(envFile))
        {
            var line = rawLine.Trim().TrimStart('\uFEFF');
            if (line.Length == 0 || line.StartsWith('#')) continue;

            var separator = line.IndexOf('=');
            if (separator < 0 || !string.Equals(line[..separator].Trim(), PasswordKey, StringComparison.Ordinal))
                continue;

            var value = line[(separator + 1)..].Trim();
            if (value.Length >= 2 &&
                ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
            {
                value = value[1..^1];
            }

            if (value.Length > 0)
                Environment.SetEnvironmentVariable(PasswordKey, value);
            return;
        }
    }
}
