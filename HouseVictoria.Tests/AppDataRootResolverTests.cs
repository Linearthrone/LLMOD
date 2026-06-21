using HouseVictoria.Core.Utils;
using Xunit;

namespace HouseVictoria.Tests;

public class AppDataRootResolverTests
{
    [Fact]
    public void ResolveDataPath_UsesRepoRootWhenSolutionExists()
    {
        var repoRoot = FindRepoRoot();
        var appDir = Path.Combine(repoRoot, "HouseVictoria.App", "bin", "Release", "net8.0-windows");

        var resolved = AppDataRootResolver.ResolveDataPath(appDir, "Data/Memory");

        Assert.Equal(Path.GetFullPath(Path.Combine(repoRoot, "Data", "Memory")), resolved);
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "HouseVictoria.sln")))
                return dir;

            var parent = Path.GetDirectoryName(dir);
            if (string.IsNullOrEmpty(parent) || parent == dir)
                break;

            dir = parent;
        }

        throw new InvalidOperationException("Could not locate repo root for test.");
    }
}
