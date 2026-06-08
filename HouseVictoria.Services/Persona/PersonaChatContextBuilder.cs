using System.Text;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;

namespace HouseVictoria.Services.Persona
{
    /// <summary>
    /// Builds optional system context for persona chat based on <see cref="PersonaKnowledgeSharing"/> flags.
    /// </summary>
    public sealed class PersonaChatContextBuilder
    {
        private static readonly HashSet<string> RetrievalStopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "the","and","for","that","with","this","you","your","yours","what","when","where","which",
            "about","have","has","had","are","was","were","will","would","could","should","can","cant",
            "did","does","from","into","they","them","then","than","there","here","just","like","want",
            "wanted","need","needs","know","knew","told","tell","made","make","making","thing","things",
            "really","said","says","she","her","hers","him","his","our","ours","not","but","all","any",
            "how","why","who","get","got","its","i'm","i've","dont","don't","doesn't","didn't","ok","okay",
            "yeah","yes","one","also","because","been","being","over","only","some","still","look","looks"
        };

        private readonly IMemoryService? _memoryService;
        private readonly IJournalService? _journalService;

        public PersonaChatContextBuilder(IMemoryService? memoryService, IJournalService? journalService)
        {
            _memoryService = memoryService;
            _journalService = journalService;
        }

        /// <summary>
        /// Builds retrieval context for the given persona and user message. Always includes an identity line.
        /// Returns null only when nothing would be added (should not happen — identity is always included).
        /// </summary>
        public async Task<string?> BuildAsync(AIContact contact, string userMessage, IReadOnlyList<string>? otherContactIds = null)
        {
            try
            {
                var sharing = PersonaKnowledgeSharing.Resolve(contact);
                var keywords = ExtractKeywords(userMessage);
                var sections = new List<(double score, string text)>();

                var builder = new StringBuilder();
                builder.AppendLine(
                    $"[Identity] You are {contact.Name}. Stay in character as {contact.Name} only. " +
                    "Do not present yourself as Victoria or any other persona unless the user explicitly asks for roleplay.");

                if (keywords.Count == 0)
                    return builder.ToString().TrimEnd();

                if (sharing.ShareUserBasics && _memoryService != null)
                {
                    try
                    {
                        var global = await _memoryService.SearchGlobalKnowledgeAsync(string.Join(' ', keywords))
                            .ConfigureAwait(false);
                        if (global == null || global.Count == 0)
                            global = await _memoryService.GetGlobalKnowledgeAsync().ConfigureAwait(false);

                        foreach (var entry in global
                                     .Select(e => (score: (double)ScoreText(e, keywords), text: e))
                                     .Where(x => x.score > 0)
                                     .OrderByDescending(x => x.score)
                                     .Take(3))
                        {
                            sections.Add((entry.score + 0.3, $"About the user:\n{TruncateText(entry.text, 400)}"));
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"User basics retrieval failed: {ex.Message}");
                    }
                }

                if (sharing.ShareHouseJournals && _journalService != null)
                {
                    try
                    {
                        var journals = await _journalService.GetAllJournalsAsync().ConfigureAwait(false);
                        foreach (var journal in journals)
                        {
                            var headerScore = ScoreText(journal.Title, keywords) * 2
                                              + ScoreText(journal.Topic, keywords) * 2
                                              + ScoreText(journal.Preface, keywords);

                            if (!string.IsNullOrWhiteSpace(journal.ConclusionSummary))
                            {
                                var concScore = ScoreText(journal.ConclusionSummary, keywords)
                                                + ScoreText(journal.ConclusionImplications, keywords)
                                                + headerScore;
                                if (concScore > 0)
                                {
                                    sections.Add((concScore + 0.5,
                                        $"House journal \"{journal.Title}\" ({journal.ConcludedAt:yyyy-MM-dd}):\n" +
                                        TruncateText(journal.ConclusionSummary, 600)));
                                }
                            }

                            foreach (var entry in journal.Entries)
                            {
                                if (entry.Kind == JournalEntryKind.Conclusion)
                                    continue;
                                var entryScore = ScoreText(entry.Title, keywords) * 2
                                                 + ScoreText(entry.Body, keywords)
                                                 + headerScore;
                                if (entryScore > 0)
                                {
                                    sections.Add((entryScore,
                                        $"House journal \"{journal.Title}\" — {entry.Title} ({entry.Timestamp:yyyy-MM-dd}):\n" +
                                        TruncateText(entry.Body, 600)));
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"House journal retrieval failed: {ex.Message}");
                    }
                }

                if (sharing.ShareOwnMemories && _memoryService != null)
                {
                    try
                    {
                        var memories = await _memoryService.GetMemoriesAsync(contact.Id).ConfigureAwait(false);
                        foreach (var memory in memories
                                     .Select(m => (score: (double)ScoreText(m, keywords), text: m))
                                     .Where(x => x.score > 0)
                                     .OrderByDescending(x => x.score)
                                     .Take(3))
                        {
                            sections.Add((memory.score, $"Your memory ({contact.Name}):\n{TruncateText(memory.text, 400)}"));
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Own memory retrieval failed: {ex.Message}");
                    }
                }

                if (sharing.ShareOtherPersonaMemories && _memoryService != null && otherContactIds != null)
                {
                    try
                    {
                        foreach (var otherId in otherContactIds.Where(id => !string.Equals(id, contact.Id, StringComparison.Ordinal)))
                        {
                            var memories = await _memoryService.GetMemoriesAsync(otherId).ConfigureAwait(false);
                            foreach (var memory in memories
                                         .Select(m => (score: (double)ScoreText(m, keywords), text: m))
                                         .Where(x => x.score > 0)
                                         .OrderByDescending(x => x.score)
                                         .Take(2))
                            {
                                sections.Add((memory.score * 0.8, $"Another persona's memory:\n{TruncateText(memory.text, 350)}"));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Other persona memory retrieval failed: {ex.Message}");
                    }
                }

                if ((sharing.ShareOwnDataBank || sharing.ShareSharedDataBanks) && _memoryService != null)
                {
                    try
                    {
                        var dataBanks = await _memoryService.GetAllDataBanksAsync().ConfigureAwait(false);
                        var personaName = contact.Name ?? string.Empty;
                        foreach (var bank in dataBanks)
                        {
                            if (bank == null || string.IsNullOrWhiteSpace(bank.Name))
                                continue;

                            var isOwn = !string.IsNullOrWhiteSpace(personaName) &&
                                          bank.Name.Contains(personaName, StringComparison.OrdinalIgnoreCase);
                            if (isOwn && !sharing.ShareOwnDataBank)
                                continue;
                            if (!isOwn && !sharing.ShareSharedDataBanks)
                                continue;

                            foreach (var entry in bank.DataEntries)
                            {
                                var score = ScoreText(entry.Title, keywords) * 2 + ScoreText(entry.Content, keywords);
                                if (score > 0)
                                {
                                    sections.Add((score,
                                        $"Data bank \"{bank.Name}\" — {entry.Title}:\n{TruncateText(entry.Content, 500)}"));
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Data bank retrieval failed: {ex.Message}");
                    }
                }

                if (sections.Count == 0)
                    return builder.ToString().TrimEnd();

                builder.AppendLine();
                builder.AppendLine(
                    "[Reference material below. Use only what applies to you as " + contact.Name + ". " +
                    "Do not claim another persona's work as your own.]");
                builder.AppendLine();

                var budget = 2000;
                foreach (var section in sections.OrderByDescending(s => s.score).Take(6))
                {
                    if (budget <= 0)
                        break;
                    var piece = section.text.Length > budget ? section.text.Substring(0, budget) + "…" : section.text;
                    builder.AppendLine(piece);
                    builder.AppendLine();
                    budget -= piece.Length;
                }

                return builder.ToString().TrimEnd();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PersonaChatContextBuilder failed: {ex.Message}");
                return $"[Identity] You are {contact.Name}. Stay in character as {contact.Name} only.";
            }
        }

        private static List<string> ExtractKeywords(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new List<string>();

            return System.Text.RegularExpressions.Regex.Matches(text.ToLowerInvariant(), @"[a-z0-9][a-z0-9'\-]{2,}")
                .Select(m => m.Value.Trim('\''))
                .Where(t => t.Length >= 3 && !RetrievalStopWords.Contains(t))
                .Distinct()
                .ToList();
        }

        private static int ScoreText(string? text, List<string> keywords)
        {
            if (string.IsNullOrWhiteSpace(text) || keywords.Count == 0)
                return 0;

            var lower = text.ToLowerInvariant();
            var score = 0;
            foreach (var keyword in keywords)
            {
                var idx = 0;
                while ((idx = lower.IndexOf(keyword, idx, StringComparison.Ordinal)) >= 0)
                {
                    score++;
                    idx += keyword.Length;
                }
            }
            return score;
        }

        private static string TruncateText(string? s, int max)
        {
            if (string.IsNullOrEmpty(s))
                return string.Empty;
            return s.Length <= max ? s : s.Substring(0, max).TrimEnd() + "…";
        }
    }
}
