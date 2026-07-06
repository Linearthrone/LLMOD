using System.Text;
using System.Text.Json;
using HouseVictoria.Core.Models;
using HouseVictoria.Services.Autonomy;
using Xunit;

namespace HouseVictoria.Tests;

public class AutonomyJournalFileTests
{
    [Fact]
    public async Task ReadTailEntriesAsync_ParsesPrettyPrintedObjects()
    {
        var options = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var first = JsonSerializer.Serialize(new AutonomyJournalEntry
        {
            Id = "first",
            Summary = "First entry",
            Activity = AutonomyActivityKind.WriteResearch
        }, options);
        var second = JsonSerializer.Serialize(new AutonomyJournalEntry
        {
            Id = "second",
            Summary = "Second entry",
            Activity = AutonomyActivityKind.CreateArt
        }, options);

        var path = Path.Combine(Path.GetTempPath(), "hv-journal-" + Guid.NewGuid().ToString("N") + ".jsonl");
        try
        {
            await File.WriteAllTextAsync(path, first + Environment.NewLine + second);
            var entries = await AutonomyJournalFile.ReadTailEntriesAsync(path, 10);

            Assert.Equal(2, entries.Count);
            Assert.Equal("first", entries[0].Id);
            Assert.Equal("second", entries[1].Id);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task ReadTailEntriesAsync_ReturnsOnlyTail()
    {
        var options = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var builder = new StringBuilder();
        for (var i = 0; i < 5; i++)
        {
            builder.Append(JsonSerializer.Serialize(new AutonomyJournalEntry
            {
                Id = $"id-{i}",
                Summary = $"Entry {i}"
            }, options));
            builder.AppendLine();
        }

        var path = Path.Combine(Path.GetTempPath(), "hv-journal-" + Guid.NewGuid().ToString("N") + ".jsonl");
        try
        {
            await File.WriteAllTextAsync(path, builder.ToString());
            var entries = await AutonomyJournalFile.ReadTailEntriesAsync(path, 2);

            Assert.Equal(2, entries.Count);
            Assert.Equal("id-3", entries[0].Id);
            Assert.Equal("id-4", entries[1].Id);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
