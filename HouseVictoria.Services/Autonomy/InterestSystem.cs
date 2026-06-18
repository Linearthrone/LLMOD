using System.Text.RegularExpressions;
using HouseVictoria.Core.Models;

namespace HouseVictoria.Services.Autonomy
{
    /// <summary>
    /// Tracks Victoria's deepening interests across days (slow decay, fast growth on substantive work).
    /// </summary>
    internal static class InterestSystem
    {
        private const double GrowthPerAction = 0.12;
        private const double DecayRate = 0.008;
        private const double Baseline = 0.25;

        /// <summary>Migrate legacy drive keys and ensure interest collections exist.</summary>
        public static void MigrateState(AutonomyRuntimeState state)
        {
            if (state.Drives.TryGetValue("industry", out var legacy) && !state.Drives.ContainsKey("purpose"))
                state.Drives["purpose"] = legacy;

            if (state.DriveBaselines.TryGetValue("industry", out var legacyBase) && !state.DriveBaselines.ContainsKey("purpose"))
                state.DriveBaselines["purpose"] = legacyBase;

            state.Drives.Remove("industry");
            state.DriveBaselines.Remove("industry");

            state.InterestWeights ??= new Dictionary<string, double>();
            state.ActiveInterestTags ??= new List<string>();
        }

        public static void Decay(AutonomyRuntimeState state)
        {
            foreach (var key in state.InterestWeights.Keys.ToList())
            {
                var current = state.InterestWeights[key];
                var next = current + (Baseline - current) * DecayRate;
                state.InterestWeights[key] = Clamp(next);
            }

            RefreshActiveTags(state, maxTags: 3);
        }

        public static void RecordInterest(AutonomyRuntimeState state, string? topic, string? title, int maxTags)
        {
            var tag = ExtractTag(topic, title);
            if (string.IsNullOrWhiteSpace(tag))
                return;

            var current = state.InterestWeights.GetValueOrDefault(tag, Baseline);
            state.InterestWeights[tag] = Clamp(current + GrowthPerAction);
            RefreshActiveTags(state, maxTags);
        }

        public static string BuildHint(AutonomyRuntimeState state)
        {
            if (state.ActiveInterestTags.Count == 0)
                return "(no active interests yet)";

            return string.Join(", ", state.ActiveInterestTags
                .Select(t => $"{t} ({state.InterestWeights.GetValueOrDefault(t, Baseline):F2})"));
        }

        public static List<AutonomyInterestTag> Snapshot(AutonomyRuntimeState state, int maxTags)
        {
            RefreshActiveTags(state, maxTags);
            return state.InterestWeights
                .OrderByDescending(kv => kv.Value)
                .Take(maxTags + 5)
                .Select(kv => new AutonomyInterestTag
                {
                    Tag = kv.Key,
                    Weight = Math.Round(kv.Value, 2),
                    IsActive = state.ActiveInterestTags.Contains(kv.Key, StringComparer.OrdinalIgnoreCase)
                })
                .ToList();
        }

        private static void RefreshActiveTags(AutonomyRuntimeState state, int maxTags)
        {
            state.ActiveInterestTags = state.InterestWeights
                .Where(kv => kv.Value >= 0.35)
                .OrderByDescending(kv => kv.Value)
                .Take(maxTags)
                .Select(kv => kv.Key)
                .ToList();
        }

        public static string ExtractTag(string? topic, string? title)
        {
            var raw = !string.IsNullOrWhiteSpace(topic) ? topic : title;
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            var normalized = Regex.Replace(raw.ToLowerInvariant(), @"[^a-z0-9\s\-]", " ");
            var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length >= 4 && !StopWords.Contains(w))
                .Take(3);
            var tag = string.Join("-", words);
            return tag.Length > 48 ? tag[..48] : tag;
        }

        private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "the", "and", "for", "with", "from", "into", "your", "this", "that", "work", "project",
            "autonomy", "final", "expansion", "backtesting", "research", "strategy", "forex", "trade"
        };

        private static double Clamp(double v) => Math.Max(0.0, Math.Min(1.0, v));
    }
}
