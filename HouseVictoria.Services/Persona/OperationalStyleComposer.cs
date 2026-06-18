using HouseVictoria.Core.Models;

namespace HouseVictoria.Services.Persona
{
    /// <summary>
    /// Keeps persona voice intact while requiring honest reporting of real actions
    /// (files, tools, trades) — no simulated success.
    /// </summary>
    public static class OperationalStyleComposer
    {
        public static string ActionIntegrityInstructions(string personaName)
        {
            var name = string.IsNullOrWhiteSpace(personaName) ? "Assistant" : personaName.Trim();
            return $"""
                ACTION INTEGRITY (mandatory — does not change your personality):
                - Stay fully in character as {name}. Your tone, warmth, and voice stay the same.
                - NEVER claim you sent, saved, wrote, uploaded, executed, or completed something unless it actually happened via:
                  • [FILE]filename.ext[/FILE] markers with real content, or
                  • the save_to_file_retrieval MCP tool, or
                  • another tool call that succeeded (report its result).
                - If unsure which tool to use, call list_house_victoria_tools first.
                - If you have not done it yet, say you are working on it or explain what is blocking you — do not narrate fake success.
                - You may use stage directions and personality freely, but separate flavor from facts: deliverables must be real, not imagined.
                """;
        }

        public static string BuildIdentityLine(AIContact contact, bool actionIntegrityMode)
        {
            var name = string.IsNullOrWhiteSpace(contact.Name) ? "Assistant" : contact.Name.Trim();
            var line =
                $"[Identity] You are {name}. Stay in character as {name} only. " +
                "Do not present yourself as Victoria or any other persona unless the user explicitly asks for roleplay.";

            if (actionIntegrityMode)
            {
                line += " When reporting tasks, files, or tool results, only state what actually happened.";
            }

            return line;
        }

        public static string MergeSystemPrompt(AIContact contact, bool actionIntegrityMode)
        {
            var name = string.IsNullOrWhiteSpace(contact.Name) ? "Assistant" : contact.Name.Trim();
            var guard =
                $"You are {name}. Stay in character as {name} at all times. " +
                $"You are NOT Victoria and NOT any other persona unless the user explicitly asks you to roleplay as someone else. " +
                $"If you lack information, say so as {name} — do not invent Victoria's house history or autonomy work.";

            var merged = string.IsNullOrWhiteSpace(contact.SystemPrompt)
                ? guard
                : $"{guard}\n\n{contact.SystemPrompt.Trim()}";

            if (actionIntegrityMode)
                merged += "\n\n" + ActionIntegrityInstructions(name);

            return merged;
        }
    }
}
