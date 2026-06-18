using System.Text.RegularExpressions;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;

namespace HouseVictoria.Services.Autonomy
{
    /// <summary>
    /// Scores autonomy outcomes with anti-repetition caps and plan-step rewards.
    /// </summary>
    internal sealed class OutcomeEvaluator
    {
        private readonly IAIService _aiService;

        public OutcomeEvaluator(IAIService aiService) => _aiService = aiService;

        private static readonly string[] HollowPhrases =
        {
            "i have completed", "i completed the", "i created the strategy",
            "task complete", "research is done", "i finished"
        };

        public async Task<AutonomyOutcome> EvaluateAsync(
            AIContact contact,
            AutonomyActivityKind activity,
            string? topic,
            string? projectId,
            string body,
            IReadOnlyList<AutonomyRecentActivity> recent,
            IReadOnlyList<AutonomyOutcome> recentOutcomes,
            bool planStepCompleted,
            bool useLlmCritique,
            CancellationToken cancellationToken = default)
        {
            var (heuristic, note) = ScoreHeuristic(activity, topic, body, recent, recentOutcomes, planStepCompleted);
            var score = heuristic;

            if (useLlmCritique)
            {
                var llm = await LlmCritiqueAsync(contact, activity, body, cancellationToken).ConfigureAwait(false);
                if (llm.HasValue)
                {
                    score = 0.5 * heuristic + 0.5 * llm.Value;
                    note += $" | self-critique {llm.Value:F2}";
                }
            }

            return new AutonomyOutcome
            {
                Activity = activity,
                Topic = topic,
                ProjectId = projectId,
                Score = Math.Round(Math.Clamp(score, 0, 1), 2),
                Note = note,
                TimestampUtc = DateTime.UtcNow
            };
        }

        private static (double Score, string Note) ScoreHeuristic(
            AutonomyActivityKind activity,
            string? topic,
            string body,
            IReadOnlyList<AutonomyRecentActivity> recent,
            IReadOnlyList<AutonomyOutcome> recentOutcomes,
            bool planStepCompleted)
        {
            body ??= string.Empty;
            var notes = new List<string>();
            var wordCount = body.Split(new[] { ' ', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;

            double lengthScore = wordCount switch
            {
                < 60 => 0.2,
                < 200 => 0.55,
                < 900 => 0.9,
                _ => 0.8
            };
            notes.Add($"{wordCount}w");

            if (activity == AutonomyActivityKind.Reflect && wordCount < 150)
            {
                lengthScore *= 0.6;
                notes.Add("thin-reflect");
            }

            var hasStructure = Regex.IsMatch(body, @"(^|\n)#{1,3}\s", RegexOptions.Multiline)
                               || Regex.IsMatch(body, @"(^|\n)\s*[-*]\s", RegexOptions.Multiline);
            double structureScore = hasStructure ? 1.0 : 0.5;

            var lower = body.ToLowerInvariant();
            var hollow = HollowPhrases.Any(p => lower.Contains(p)) && wordCount < 120;
            if (hollow)
                notes.Add("hollow");

            double noveltyScore = 1.0;
            if (!string.IsNullOrWhiteSpace(topic))
            {
                var repeats = recent.Count(a =>
                    !string.IsNullOrWhiteSpace(a.Topic) &&
                    TopicsSimilar(a.Topic, topic));

                if (repeats >= 1)
                {
                    noveltyScore = Math.Max(0.2, 1.0 - 0.35 * repeats);
                    notes.Add($"repeat x{repeats}");
                }

                var outcomeRepeats = recentOutcomes.Count(o =>
                    !string.IsNullOrWhiteSpace(o.Topic) &&
                    TopicsSimilar(o.Topic, topic));
                if (outcomeRepeats >= 1)
                {
                    noveltyScore = Math.Min(noveltyScore, 0.45);
                    notes.Add("outcome-repeat");
                }
            }

            var score = 0.4 * lengthScore + 0.25 * structureScore + 0.35 * noveltyScore;
            if (hollow)
                score *= 0.5;

            if (planStepCompleted)
            {
                score = Math.Min(1.0, score + 0.12);
                notes.Add("plan-step");
            }

            var hasExternalRef = Regex.IsMatch(body, @"https?://|doi\.|arxiv|\.edu|metaquotes|mql4|mql5", RegexOptions.IgnoreCase);
            if (hasExternalRef)
            {
                score = Math.Min(1.0, score + 0.08);
                notes.Add("cited");
            }

            // Hard cap when repeating the same topic despite good length.
            if (!string.IsNullOrWhiteSpace(topic) &&
                recent.Any(a => TopicsSimilar(a.Topic, topic)))
            {
                score = Math.Min(score, 0.5);
            }

            return (score, string.Join(", ", notes));
        }

        private static bool TopicsSimilar(string? a, string? b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
                return false;

            if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
                return true;

            var na = NormalizeTopic(a);
            var nb = NormalizeTopic(b);
            return na == nb || na.Contains(nb) || nb.Contains(na);
        }

        private static string NormalizeTopic(string s) =>
            Regex.Replace(s.ToLowerInvariant(), @"[^a-z0-9]", string.Empty);

        private async Task<double?> LlmCritiqueAsync(
            AIContact contact,
            AutonomyActivityKind activity,
            string body,
            CancellationToken cancellationToken)
        {
            var prompt = $$"""
                Rate the quality and usefulness of this autonomous {{activity}} output on a scale of 0 to 100,
                judging substance, specificity, and whether it genuinely advanced the work (not a hollow status update).
                Reply with ONLY a number 0-100.

                Output:
                {{Truncate(body, 1800)}}
                """;

            string raw;
            try
            {
                raw = await _aiService.SendMessageAsync(contact, prompt, null).ConfigureAwait(false);
            }
            catch
            {
                return null;
            }

            var match = Regex.Match(raw ?? string.Empty, @"\d{1,3}");
            if (match.Success && int.TryParse(match.Value, out var pct))
                return Math.Clamp(pct, 0, 100) / 100.0;

            return null;
        }

        public static string BuildFeedbackHint(IReadOnlyList<AutonomyOutcome> outcomes)
        {
            if (outcomes.Count == 0)
                return "(no feedback yet)";

            var recent = outcomes.TakeLast(5).Reverse();
            return string.Join("; ", recent.Select(o =>
            {
                var label = string.IsNullOrWhiteSpace(o.Topic) ? o.Activity.ToString() : o.Topic;
                return $"{label}={o.Score:F2}";
            }));
        }

        private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
    }
}
