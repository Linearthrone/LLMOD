using HouseVictoria.Core.Models;

namespace HouseVictoria.Services.Persona
{
    /// <summary>
    /// Short, always-on tool menu so personas know how to deliver real results (not narrate them).
    /// </summary>
    public static class HouseVictoriaToolCatalog
    {
        public static string BuildChatDeliverableGuide(string? generatedFilesPath = null)
        {
            var pathLine = string.IsNullOrWhiteSpace(generatedFilesPath)
                ? "Media/GeneratedFiles (File Retrieval 📥 in the top tray)"
                : generatedFilesPath;

            return $"""
                HOW TO DELIVER FILES TO THE USER (chat — works even without calling tools):
                - Put the full document inside [FILE]filename.ext[/FILE] markers in your reply.
                - Example:
                  [FILE]research_paper.md
                  # Title
                  Body text here...
                  [/FILE]
                - Files land in File Retrieval: {pathLine}
                - Do not say the file was sent until the [FILE] block is in your message (or a tool succeeded below).
                """;
        }

        public static string BuildHermesToolGuide(string? generatedFilesPath = null)
        {
            var pathLine = string.IsNullOrWhiteSpace(generatedFilesPath)
                ? "Media/GeneratedFiles"
                : generatedFilesPath;

            return $"""
                YOUR HOUSE VICTORIA TOOLS (Hermes MCP — use these for real actions):
                | Tool | When to use |
                |------|-------------|
                | save_to_file_retrieval(filename, content) | User wants a document/paper/report in File Retrieval 📥 |
                | list_house_victoria_tools | List all tools and when to use them (call if unsure) |
                | mt4_status | Check MT4 bridge before trading |
                | mt4_list_symbols | Resolve broker symbol names before trades |
                | mt4_get_market_data | Current bid/ask |
                | mt4_get_open_positions | Open House Victoria trades |
                | mt4_execute_trade | Place a verified live trade (requires stop loss) |
                | mt4_run_backtest | Backtest a strategy on historical data |
                | memory_store / memory_search | Persist or recall facts |
                | project_bank_create / knowledge_bank_add | Project & knowledge banks |

                File Retrieval path on disk: {pathLine}
                Prefer save_to_file_retrieval OR [FILE]...[/FILE] for user deliverables — not made-up paths like docs/...
                After a tool succeeds, tell the user the actual filename/path from the tool result.
                """;
        }

        public static bool ShouldIncludeHermesGuide(AIContact contact, AppConfig? config)
        {
            if (config == null)
                return false;

            if (!string.Equals(config.PrimaryLLM, "hermes", StringComparison.OrdinalIgnoreCase))
                return false;

            if (contact.AdditionalServers.TryGetValue("hermes", out var flag) &&
                string.Equals(flag, "false", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (contact.AdditionalServers.TryGetValue("hermes", out var onFlag) &&
                string.Equals(onFlag, "true", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return PersonaPromptComposer.IsPrimaryPersona(contact);
        }
    }
}
