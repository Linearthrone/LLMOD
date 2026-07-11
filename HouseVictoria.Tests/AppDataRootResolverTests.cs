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

    [Fact]
    public void ResolveDataPath_PrefersSolutionOverNestedBinDataFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), "hv-resolver-" + Guid.NewGuid().ToString("N"));
        var appDir = Path.Combine(root, "HouseVictoria.App", "bin", "Release", "net8.0-windows");
        var repoMemory = Path.Combine(root, "Data", "Memory");
        var shadowMemory = Path.Combine(appDir, "Data", "Memory");

        try
        {
            Directory.CreateDirectory(repoMemory);
            Directory.CreateDirectory(shadowMemory);
            File.WriteAllText(Path.Combine(root, "HouseVictoria.sln"), string.Empty);

            var resolved = AppDataRootResolver.ResolveDataPath(appDir, "Data/Memory");

            Assert.Equal(Path.GetFullPath(repoMemory), resolved);
            Assert.NotEqual(Path.GetFullPath(shadowMemory), resolved);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CoerceDataPath_RemapsBinReleasePathToRepoRoot()
    {
        var repoRoot = FindRepoRoot();
        var appDir = Path.Combine(repoRoot, "HouseVictoria.App", "bin", "Release", "net8.0-windows");
        var shadow = Path.Combine(appDir, "Data", "Databanks");

        var resolved = AppDataRootResolver.CoerceDataPath(appDir, shadow, "Data/Databanks");

        Assert.Equal(Path.GetFullPath(Path.Combine(repoRoot, "Data", "Databanks")), resolved);
    }

    [Fact]
    public void ToPortableDataPath_ReturnsRelativeUnderRepoRoot()
    {
        var repoRoot = FindRepoRoot();
        var appDir = Path.Combine(repoRoot, "HouseVictoria.App", "bin", "Debug", "net8.0-windows");
        var absolute = Path.Combine(repoRoot, "Data", "Memory");

        var portable = AppDataRootResolver.ToPortableDataPath(appDir, absolute, "Data/Memory");

        Assert.Equal("Data/Memory", portable);
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
