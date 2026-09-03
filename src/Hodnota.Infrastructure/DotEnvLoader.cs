using DotNetEnv;

namespace Hodnota.Infrastructure;

// .env.local (gitignored, per-developer) overrides .env (committed, shared defaults) when both exist.
// Searches upward since `dotnet ef`'s design-time build runs from the startup project's output directory.
// .env loads with NoClobber so a real environment variable already set by an actual deployment always
public static class DotEnvLoader
{
    public static void LoadIfPresent()
    {
        Env.TraversePath().NoClobber().Load(".env");
        Env.TraversePath().Load(".env.local");
    }
}
