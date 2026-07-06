using HouseVictoria.Core.Models;
using HouseVictoria.Core.Utils;
using HouseVictoria.Services.Autonomy;
using HouseVictoria.Services.Logging;
using HouseVictoria.Services.Persistence;
using Xunit;

namespace HouseVictoria.Tests;

public class LoggingServiceIntegrationTests
{
    [Fact]
    public async Task RefreshLogs_LoadsRepoAutonomyJournalWithoutThrowing()
    {
        var repoRoot = FindRepoRoot();
        var appDir = Path.Combine(repoRoot, "HouseVictoria.App", "bin", "Release", "net8.0-windows");
        var journalPath = Path.Combine(repoRoot, "Data", "Autonomy", "journal.jsonl");
        if (!File.Exists(journalPath))
        {
            // Skip when autonomy data has not been migrated locally.
            return;
        }

        var appConfig = new AppConfig
        {
            AutonomyDataPath = AppDataRootResolver.ResolveDataPath(appDir, "Data/Autonomy"),
            MediaPath = Path.Combine(repoRoot, "Media"),
            DataBankPath = AppDataRootResolver.ResolveDataPath(appDir, "Data/Databanks"),
            PersistentMemoryPath = AppDataRootResolver.ResolveDataPath(appDir, "Data/Memory")
        };

        var persistence = new DatabasePersistenceService(appConfig);
        var logging = new LoggingService(appConfig, persistence);

        var exception = await Record.ExceptionAsync(async () =>
        {
            await logging.RefreshLogsAsync();
            var categories = await logging.PeekLogCategoriesAsync();
            Assert.NotEmpty(categories);
        });

        Assert.Null(exception);
    }

    [Fact]
    public async Task AutonomyJournalFile_ParsesRealJournalTail()
    {
        var journalPath = Path.Combine(FindRepoRoot(), "Data", "Autonomy", "journal.jsonl");
        if (!File.Exists(journalPath))
            return;

        var entries = await AutonomyJournalFile.ReadTailEntriesAsync(journalPath, 400);
        Assert.True(entries.Count > 0, "Expected journal entries from real autonomy journal.");
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
