using System.Text;
using System.Text.Json;
using HouseVictoria.Core.Models;

namespace HouseVictoria.Services.Autonomy
{
    /// <summary>
    /// Reads autonomy journal files written as consecutive pretty-printed JSON objects (not strict JSONL).
    /// </summary>
    public static class AutonomyJournalFile
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public static async Task<IReadOnlyList<AutonomyJournalEntry>> ReadTailEntriesAsync(
            string path,
            int maxEntries,
            CancellationToken cancellationToken = default)
        {
            if (maxEntries <= 0 || !File.Exists(path))
                return Array.Empty<AutonomyJournalEntry>();

            var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            return ReadTailEntriesFromText(text, maxEntries);
        }

        internal static IReadOnlyList<AutonomyJournalEntry> ReadTailEntriesFromText(string text, int maxEntries)
        {
            if (maxEntries <= 0 || string.IsNullOrWhiteSpace(text))
                return Array.Empty<AutonomyJournalEntry>();

            var entries = new LinkedList<AutonomyJournalEntry>();
            foreach (var block in SplitTopLevelJsonObjects(text))
            {
                try
                {
                    var entry = JsonSerializer.Deserialize<AutonomyJournalEntry>(block, JsonOptions);
                    if (entry == null)
                        continue;

                    entries.AddLast(entry);
                    while (entries.Count > maxEntries)
                        entries.RemoveFirst();
                }
                catch (JsonException)
                {
                    // Skip malformed object and continue scanning.
                }
            }

            return entries.ToList();
        }

        internal static IEnumerable<string> SplitTopLevelJsonObjects(string text)
        {
            var sb = new StringBuilder();
            var depth = 0;
            var inString = false;
            var escape = false;

            foreach (var c in text)
            {
                sb.Append(c);

                if (inString)
                {
                    if (escape)
                    {
                        escape = false;
                    }
                    else if (c == '\\')
                    {
                        escape = true;
                    }
                    else if (c == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == '{')
                {
                    depth++;
                    continue;
                }

                if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        yield return sb.ToString();
                        sb.Clear();
                    }
                }
            }
        }
    }
}
