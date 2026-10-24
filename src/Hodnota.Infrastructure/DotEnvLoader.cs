using DotNetEnv;

namespace Hodnota.Infrastructure;

// .env.local (gitignored, per-developer) overrides .env (committed, shared defaults) when both exist.
// Searches upward since `dotnet ef`'s design-time build runs from the startup project's output directory.
// Loaded together with NoClobber so a real environment variable already set by an actual deployment
// always wins over both files — two separate NoClobber Load() calls would not do this: the first
// call's own writes would count as "already set" for the second call, blocking .env.local from
// overriding .env. LoadMulti merges the files first (NoClobber reverses precedence to first-listed
// file wins, per DotNetEnv's docs) and only then checks the real environment once per key.
public static class DotEnvLoader
{
    public static void LoadIfPresent()
    {
        Env.TraversePath().NoClobber().LoadMulti([".env.local", ".env"]);
    }
}
