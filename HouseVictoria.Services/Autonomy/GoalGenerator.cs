using System.Text.Json;
using System.Text.RegularExpressions;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;

namespace HouseVictoria.Services.Autonomy
{
    /// <summary>
    /// Turns reflection + internal drives into self-initiated goals. Instead of only
    /// grinding pre-existing projects, the agent can decide there is something she
    /// genuinely wants to pursue and create a real project for it (budget-limited).
    /// </summary>
    internal sealed class GoalGenerator
    {
        private readonly IAIService _aiService;
        private readonly IProjectManagementService _projects;

        public GoalGenerator(IAIService aiService, IProjectManagementService projects)
        {
            _aiService = aiService;
            _projects = projects;
        }

        /// <summary>
        /// Propose at most one new self-initiated project. Returns the created project,
        /// or null if nothing worth starting (or it duplicates an existing project).
        /// </summary>
        public async Task<Project?> TryGenerateGoalAsync(
            AIContact contact,
            AutonomyRuntimeState state,
            IReadOnlyList<Project> existingProjects,
            int maxActiveSelfProjects,
            string interestHint,
            CancellationToken cancellationToken = default)
        {
            var activeSelfCount = CountActiveSelfInitiated(existingProjects, contact.Id);
            if (activeSelfCount >= maxActiveSelfProjects)
                return null;

            var dominant = DriveSystem.Dominant(state);
            var existingNames = string.Join("\n", existingProjects
                .Where(p => p.Phase != ProjectPhase.Completed)
                .Select(p => $"- {p.Name}: {Truncate(p.Description, 100)}"));
            if (string.IsNullOrWhiteSpace(existingNames))
                existingNames = "(none yet)";

            var recentTopics = string.Join(", ", state.RecentActivities
                .Where(a => !string.IsNullOrWhiteSpace(a.Topic))
                .Select(a => a.Topic)
                .Distinct()
                .Take(8));

            var prompt = $$"""
                You are {{contact.Name}}, the autonomous mind of House Victoria, during quiet time.
                Your strongest drive right now is "{{dominant.Name}}" ({{dominant.Value:F2}}).
                Active interests to deepen (prefer one of these): {{interestHint}}

                You may start ONE new self-initiated project that you genuinely want to pursue —
                something that satisfies that drive and is distinct from what already exists.
                Do NOT start a new project if an active interest can be advanced instead.

                Existing open projects (do NOT duplicate these):
                {{existingNames}}

                Recently touched topics (avoid repeating): {{recentTopics}}

                If — and only if — you have a worthwhile, novel idea, reply with ONLY this JSON:
                {"create":true,"name":"short project name","type":"research|design|writing|personal|coding","description":"1-2 sentences on what it is and why you want it","priority":3}
                If nothing genuinely new appeals, reply with ONLY: {"create":false}
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

            var proposal = ParseProposal(raw);
            if (proposal == null || !proposal.Create || string.IsNullOrWhiteSpace(proposal.Name))
                return null;

            // Dedupe against existing projects by name similarity.
            if (existingProjects.Any(p => NameMatches(p.Name, proposal.Name!)))
                return null;

            var project = await _projects.CreateProjectAsync(new Project
            {
                Name = proposal.Name!.Trim(),
                Type = MapType(proposal.Type),
                Description = string.IsNullOrWhiteSpace(proposal.Description)
                    ? "Self-initiated by Victoria."
                    : proposal.Description!.Trim(),
                Priority = Math.Clamp(proposal.Priority is > 0 and <= 6 ? proposal.Priority : 3, 1, 6),
                Phase = ProjectPhase.Planning,
                AssignedAIId = contact.Id
            }).ConfigureAwait(false);

            return project;
        }

        private static bool NameMatches(string a, string b)
        {
            var na = Normalize(a);
            var nb = Normalize(b);
            return na == nb || na.Contains(nb) || nb.Contains(na);
        }

        private static string Normalize(string s) =>
            Regex.Replace(s.ToLowerInvariant(), @"[^a-z0-9]", string.Empty);

        private static ProjectType MapType(string? type) => type?.ToLowerInvariant() switch
        {
            "research" => ProjectType.Research,
            "design" or "art" => ProjectType.Design,
            "writing" => ProjectType.Writing,
            "coding" or "code" => ProjectType.Coding,
            "business" => ProjectType.Business,
            _ => ProjectType.Personal
        };

        private static GoalProposal? ParseProposal(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            var json = raw.Trim();
            var match = Regex.Match(json, @"\{[\s\S]*\}");
            if (match.Success)
                json = match.Value;

            try
            {
                return JsonSerializer.Deserialize<GoalProposal>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch
            {
                return null;
            }
        }

        private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";

        private static int CountActiveSelfInitiated(IReadOnlyList<Project> projects, string contactId)
        {
            return projects.Count(p =>
                p.Phase != ProjectPhase.Completed &&
                (string.Equals(p.AssignedAIId, contactId, StringComparison.Ordinal) ||
                 (p.Description?.Contains("Self-initiated", StringComparison.OrdinalIgnoreCase) ?? false)));
        }

        private sealed class GoalProposal
        {
            public bool Create { get; set; }
            public string? Name { get; set; }
            public string? Type { get; set; }
            public string? Description { get; set; }
            public int Priority { get; set; } = 3;
        }
    }
}
