using DotNet.Testcontainers.Configurations;

namespace Tamirhane.Tests;

/// <summary>
/// Docker gerektiren test. Docker Ã§alÄ±ÅŸmÄ±yorsa test kÄ±rmÄ±zÄ± yanmaz, nedeni yazÄ±larak atlanÄ±r;
/// bÃ¶ylece Docker'Ä± olmayan biri de diÄŸer testleri Ã§alÄ±ÅŸtÄ±rabilir.
/// </summary>
public sealed class DockerFactAttribute : FactAttribute
{
    public const string SkipReason =
        "Docker Ã§alÄ±ÅŸmÄ±yor; PostgreSQL entegrasyon testleri atlandÄ±. Ã‡alÄ±ÅŸtÄ±rmak iÃ§in Docker Desktop'Ä± aÃ§Ä±n.";

    public DockerFactAttribute()
    {
        if (!DockerEngine.IsAvailable)
            Skip = SkipReason;
    }
}

/// <summary>Docker gerektiren, veriyle Ã§alÄ±ÅŸan test (<see cref="DockerFactAttribute"/> gibi atlanÄ±r).</summary>
public sealed class DockerTheoryAttribute : TheoryAttribute
{
    public DockerTheoryAttribute()
    {
        if (!DockerEngine.IsAvailable)
            Skip = DockerFactAttribute.SkipReason;
    }
}

public static class DockerEngine
{
    private static readonly Lazy<bool> Available = new(Ping);

    /// <summary>Docker'a bir kez sorulur; cevap test Aalï¿½ï¿½Ytï¿½ï¿½rmasï¿½ï¿½ boyunca saklanï¿½ï¿½r.</summary>
    public static bool IsAvailable => Available.Value;

    /// <summary>External PostgreSQL for environments without Docker (e.g. Neon).</summary>
    public static string? ExternalDatabaseUrl =>
        Environment.GetEnvironmentVariable("TEST_DATABASE_URL")
        ?? Environment.GetEnvironmentVariable("DATABASE_URL");

    public static bool UseExternalDatabase => !string.IsNullOrWhiteSpace(ExternalDatabaseUrl);

    public static string WithSchema(string schema)
    {
        var url = ExternalDatabaseUrl!;
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            (uri.Scheme.Equals("postgres", StringComparison.OrdinalIgnoreCase) ||
             uri.Scheme.Equals("postgresql", StringComparison.OrdinalIgnoreCase)))
            return url + (url.Contains('?') ? "&" : "?") + "search_path=" + schema;

        return url.TrimEnd(';') + ";Search Path=" + schema;
    }

    private static bool Ping()
    {
        if (UseExternalDatabase)
            return true;
        try
        {
            var endpoint = TestcontainersSettings.OS.DockerEndpointAuthConfig;
            if (endpoint is null)
                return false;

            using var client = endpoint.GetDockerClientBuilder(Guid.NewGuid()).Build();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            client.System.PingAsync(timeout.Token).GetAwaiter().GetResult();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
