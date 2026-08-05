using System.Reflection;

namespace HmacManager.Kubernetes;

/// <summary>
/// Prints the startup banner to stdout, mirroring the block used in the Helm chart's
/// post-install NOTES.txt so the "HMAC MANAGER" mark is consistent across the product.
/// </summary>
/// <remarks>
/// Written directly to <c>Console.Out</c> rather than through <c>ILogger</c>: it is a
/// one-time, human-facing flourish, not a structured log event, so it isn't a <c>HmacManager.*</c>
/// category and must not be gated by <c>Logging:LogLevel</c> — it always prints once, regardless of
/// <c>logging.level</c>, the same way tools like etcd or CockroachDB print a version banner ahead of
/// their structured logs.
/// </remarks>
internal static class Banner
{
    public static void Print()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;

        Console.Out.WriteLine("""
            ╔═════════════════════════════════════════════════╗
            ║     __  ____  ______   ______                   ║
            ║    / / / /  |/  /   | / ____/                   ║
            ║   / /_/ / /|_/ / /| |/ /                        ║
            ║  / __  / /  / / ___ / /___                      ║
            ║ /_/ /_/_/  /_/_/  |_\____/                      ║
            ║     __  ______    _   _____   ________________  ║
            ║    /  |/  /   |  / | / /   | / ____/ ____/ __ \ ║
            ║   / /|_/ / /| | /  |/ / /| |/ / __/ __/ / /_/ / ║
            ║  / /  / / ___ |/ /|  / ___ / /_/ / /___/ _, _/  ║
            ║ /_/  /_/_/  |_/_/ |_/_/  |_\____/_____/_/ |_|   ║
            ╚═════════════════════════════════════════════════╝
            """);
        Console.Out.WriteLine($"          Ext-Authz Service · v{version?.ToString(3)}");
        Console.Out.WriteLine();
    }
}
