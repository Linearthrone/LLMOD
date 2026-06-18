using HouseVictoria.Core.Models;

namespace HouseVictoria.Services.Autonomy
{
    /// <summary>
    /// Composes operating-mode and session context blocks for autonomy LLM calls.
    /// </summary>
    internal static class AutonomyPromptComposer
    {
        public static string OperatingModesBlock => """
            **OPERATING MODES (autonomy session)**
            - **Execute mode** (user guidance, P7+ projects, open plan steps): Be direct. One concrete deliverable per session. No new projects unless blocked.
            - **Explore mode** (self-chosen curiosity / active interests): Deepen ONE interest. End with: what you learned, what changed, next experiment.
            - **Reflect mode**: Short and honest — not a substitute when Execute mode has open steps.

            **INTEREST RULE**
            Maintain at most 3 active interests. If you touched a topic in the last 8 actions, advance a plan step or switch interest — never rephrase the same content.

            **AUTONOMY INTEGRITY**
            Real markdown, real tool results, real backtest numbers. High drama titles are fine; hollow completion notices are not.
            """;

        public static string BuildSessionContext(
            AutonomyRuntimeState state,
            AppConfig config,
            string? userGuidance,
            int guidanceTicksRemaining,
            string? planStepDescription,
            string? lastDeliverableSnippet)
        {
            var interests = InterestSystem.BuildHint(state);
            var guidanceSection = string.IsNullOrWhiteSpace(userGuidance)
                ? string.Empty
                : $"""

                **USER GUIDANCE (priority — {guidanceTicksRemaining} actions remaining):** {userGuidance}
                """;

            var planSection = string.IsNullOrWhiteSpace(planStepDescription)
                ? string.Empty
                : $"""

                **CURRENT PLAN STEP:** {planStepDescription}
                """;

            var lastSection = string.IsNullOrWhiteSpace(lastDeliverableSnippet)
                ? string.Empty
                : $"""

                **LAST DELIVERABLE (do not repeat):** {Truncate(lastDeliverableSnippet, 200)}
                """;

            return $"""
                {OperatingModesBlock}
                Active interests: {interests}
                Budget: {state.ActionsThisHour}/{AutonomyLevelProfile.EffectiveMaxActionsPerHour(config)} actions this hour; {state.SelfGoalsToday}/{config.AutonomyMaxSelfGoalsPerDay} self-goals today.
                {guidanceSection}{planSection}{lastSection}
                """;
        }

        public static string BuildDecisionFeedbackGuidance() =>
            "High scores on repeated topics do NOT justify repeating them. Prefer advancing the next plan step, following user guidance, or switching to an active interest.";

        private static string Truncate(string s, int max) =>
            s.Length <= max ? s : s[..max].TrimEnd() + "…";
    }
}
