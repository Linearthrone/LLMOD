using System.Text.Json;
using System.Text.RegularExpressions;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;

namespace HouseVictoria.Services.Autonomy
{
    /// <summary>
    /// Decomposes a project into a small, concrete, multi-tick plan so autonomy advances
    /// real steps across ticks instead of blindly incrementing a completion percentage.
    /// </summary>
    internal sealed class AutonomyPlanner
    {
        private readonly IAIService _aiService;

        public AutonomyPlanner(IAIService aiService) => _aiService = aiService;

        /// <summary>Ask the LLM to break the project into 3-6 ordered, concrete steps.</summary>
        public async Task<AutonomyPlan> CreatePlanAsync(
            AIContact contact,
            Project project,
            string? priorWork,
            CancellationToken cancellationToken = default)
        {
            var priorSection = string.IsNullOrWhiteSpace(priorWork)
                ? ""
                : $"\n\nWork already done (do not re-plan these):\n{priorWork}";

            var prompt = $$"""
                You are {{contact.Name}}, planning how to actually finish the project "{{project.Name}}".
                Description: {{project.Description}}
                Current completion: {{project.CompletionPercentage}}%{{priorSection}}

                Break the remaining work into 3-6 concrete, ordered steps. Each step must be a
                real deliverable or investigation you can complete in one focused session — not
                vague phases like "research" or "finalize".

                Reply with ONLY a JSON array of step strings, e.g.:
                ["Define the data schema and example records", "Draft the core algorithm with pseudocode", "Write a worked example end to end"]
                """;

            string raw;
            try
            {
                raw = await _aiService.SendMessageAsync(contact, prompt, null).ConfigureAwait(false);
            }
            catch
            {
                raw = string.Empty;
            }

            var steps = ParseSteps(raw);
            if (steps.Count == 0)
            {
                // Fallback so we always have a usable plan rather than blocking the tick.
                steps = new List<string>
                {
                    "Clarify the objective and define what 'done' looks like",
                    "Produce the core deliverable with concrete substance",
                    "Review, refine, and document open questions"
                };
            }

            return new AutonomyPlan
            {
                ProjectId = project.Id,
                ProjectName = project.Name,
                CreatedUtc = DateTime.UtcNow,
                Steps = steps
                    .Take(6)
                    .Select(s => new AutonomyPlanStep { Description = s })
                    .ToList()
            };
        }

        private static List<string> ParseSteps(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return new List<string>();

            var json = raw.Trim();
            var match = Regex.Match(json, @"\[[\s\S]*\]");
            if (match.Success)
                json = match.Value;

            try
            {
                var parsed = JsonSerializer.Deserialize<List<string>>(json);
                if (parsed != null)
                    return parsed
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Select(s => s.Trim())
                        .ToList();
            }
            catch
            {
                // Fall through to line-based parsing.
            }

            // Tolerate a bullet/numbered list if JSON parsing failed.
            return raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(line => Regex.Replace(line, @"^\s*([-*]|\d+[.)])\s*", string.Empty).Trim())
                .Where(line => line.Length > 4)
                .ToList();
        }
    }
}
