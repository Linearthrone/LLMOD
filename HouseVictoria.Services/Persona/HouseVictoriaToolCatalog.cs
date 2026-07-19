using HouseVictoria.Core.Models;
using System.Text.RegularExpressions;

namespace HouseVictoria.Services.Persona
{
    /// <summary>
    /// Short, always-on tool menu so personas know how to deliver real results (not narrate them).
    /// </summary>
    public static class HouseVictoriaToolCatalog
    {
        /// <summary>Hermes MCP tool name registered by computer-use-mcp on the gateway.</summary>
        public const string ComputerUseMcpToolName = "mcp_computer_use_computer";

        /// <summary>Hermes computer_use action for desktop screenshots (not "get_screenshot" — that name is rejected).</summary>
        public const string ComputerUseScreenshotAction = "capture";

        /// <summary>Hermes MCP tool for browser extension tab capture (house_victoria server).</summary>
        public const string BrowserCaptureTabToolName = "mcp_house_victoria_browser_capture_tab";

        public const string BrowserBridgeHealthToolName = "mcp_house_victoria_browser_bridge_health";

        public const string BrowserClickToolName = "mcp_house_victoria_browser_click";

        public const string BrowserTypeToolName = "mcp_house_victoria_browser_type";

        public const string BrowserKeyToolName = "mcp_house_victoria_browser_key";

        public const string BrowserScrollToolName = "mcp_house_victoria_browser_scroll";

        public const string UnrealEditorHealthToolName = "mcp_house_victoria_unreal_editor_health";

        public const string UnrealEditorScreenshotToolName = "mcp_house_victoria_unreal_editor_screenshot";

        public const string UnrealEditorSearchAssetsToolName = "mcp_house_victoria_unreal_editor_search_assets";

        public const string UnrealEditorGetPropertyToolName = "mcp_house_victoria_unreal_editor_get_property";

        public const string UnrealEditorSetPropertyToolName = "mcp_house_victoria_unreal_editor_set_property";

        public const string UnrealEditorCallToolName = "mcp_house_victoria_unreal_editor_call";

        public const string UnrealEditorConsoleToolName = "mcp_house_victoria_unreal_editor_console";

        public const string UnrealEditorSpawnActorToolName = "mcp_house_victoria_unreal_editor_spawn_actor";

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

        public static string BuildHermesToolGuide(
            string? generatedFilesPath = null,
            bool includeComputerUse = false,
            bool includeUnrealEditorWrite = false)
        {
            var pathLine = string.IsNullOrWhiteSpace(generatedFilesPath)
                ? "Media/GeneratedFiles"
                : generatedFilesPath;

            // Browser capture/drive is always available (bridge + extension) — not gated on desktop control.
            var browserSection = $"""


                BROWSER TAB (user's real Chrome/Edge — always use these for web pages):
                | Tool | When to use |
                |------|-------------|
                | {BrowserCaptureTabToolName} | ACTIVE tab screenshot + page_map (NOT computer_use capture) |
                | {BrowserClickToolName} | Click by selector, page_map.elements[].index, or viewport x/y |
                | {BrowserTypeToolName} | Type into selector/index or the focused field |
                | {BrowserKeyToolName} | Keys/combos (Enter, Tab, Ctrl+A) in the tab |
                | {BrowserScrollToolName} | Scroll by deltas or to an element |
                | {BrowserBridgeHealthToolName} | Check bridge :17891 if capture/actions time out |

                Browser loop: capture → click/type/key/scroll → capture again to verify.
                Prefer selector or page_map index; fall back to x/y from page_map.elements[].center.
                FORBIDDEN for the user's Chrome: browser, browser_navigate, browser_vision, puppeteer_* (ghost Chromium).
                """;

            var unrealWriteRows = includeUnrealEditorWrite
                ? $"""
                | {UnrealEditorSetPropertyToolName} | Set a UObject property (writes allowed) |
                | {UnrealEditorCallToolName} | Call a UFunction on an object path |
                | {UnrealEditorConsoleToolName} | Editor console command (destructive cmds blocked) |
                | {UnrealEditorSpawnActorToolName} | Spawn actor in the current level |
                """
                : """
                | (writes disabled) | Enable Allow Unreal Editor Control in Settings for set/call/console/spawn |
                """;

            var unrealSection = $"""


                UNREAL EDITOR (open .uproject via Remote Control :30010 — NOT the world WebSocket :8888):
                | Tool | When to use |
                |------|-------------|
                | {UnrealEditorHealthToolName} | Check RC before editor work |
                | {UnrealEditorScreenshotToolName} | Viewport capture request |
                | {UnrealEditorSearchAssetsToolName} | Search Content Browser assets |
                | {UnrealEditorGetPropertyToolName} | Read actor/component properties |
                {unrealWriteRows}
                Editor loop: health → search/screenshot → mutate (if allowed) → verify with get_property/screenshot.
                Do NOT use computer_use to click the Unreal Editor UI when these tools apply.
                """;

            // Only advertise OS desktop control when the user has explicitly allowed it.
            var computerUseSection = includeComputerUse
                ? $"""


                DESKTOP CONTROL (non-browser apps / full desktop — user allowed computer control):
                | Tool | When to use |
                |------|-------------|
                | {ComputerUseMcpToolName} (action={ComputerUseScreenshotAction}) | See the LOCAL desktop outside the browser |
                | {ComputerUseMcpToolName} (action=left_click / type / scroll / key) | Act on non-browser desktop UI |
                | list_desktop_windows / focus_desktop_window | Focus a desktop window before OS clicks |
                | terminal | Run a shell command (NOT for desktop screenshots) |

                Do NOT use computer_use for tasks INSIDE a browser tab — use {BrowserCaptureTabToolName}
                and {BrowserClickToolName}/{BrowserTypeToolName}/{BrowserKeyToolName} instead (overlay-safe).
                FORBIDDEN ghost browsers: browser, browser_vision, puppeteer_*.
                {BuildComputerUseWindowFocusReminder()}
                """
                : string.Empty;

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
                {browserSection}
                {unrealSection}
                File Retrieval path on disk: {pathLine}
                Prefer save_to_file_retrieval OR [FILE]...[/FILE] for user deliverables — not made-up paths like docs/...
                After a tool succeeds, tell the user the actual filename/path from the tool result.{computerUseSection}
                """;
        }

        /// <summary>
        /// Mandatory first-action block prepended to the user turn when control is ON and the
        /// message asks about the local desktop. Steering text alone was insufficient (QA-023).
        /// </summary>
        public static string BuildDesktopScreenshotMandatoryFirstAction() =>
            $"""
            [MANDATORY FIRST ACTION — execute before any other tool or reply]
            You MUST call {ComputerUseMcpToolName} with action={ComputerUseScreenshotAction} as your FIRST tool call on this turn.
            Do NOT call any other tool before this screenshot completes.

            FORBIDDEN on this turn: vision_analyze, browser_vision, browser, terminal, skills_list,
            skill_view, mcp_house_victoria_list_house_victoria_tools, and any skill/MCP discovery tool.

            After the screenshot tool returns, answer the user using ONLY that screenshot evidence.
            """;

        /// <summary>
        /// Short reminder appended to the Hermes tool guide when desktop control is allowed.
        /// </summary>
        public static string BuildComputerUseWindowFocusReminder() =>
            """
            If the target window is off-screen or buried: list_desktop_windows → focus_desktop_window → capture.
            """;

        /// <summary>
        /// Extra steering appended to the user turn when control is ON and the message asks about
        /// the local desktop. Keeps models from picking browser_vision for screenshot wording.
        /// </summary>
        public static string BuildDesktopScreenshotSteering() =>
            $"""
            [DESKTOP SCREENSHOT ROUTING: The user is asking about their LOCAL desktop/screen.
            Your first tool call MUST be {ComputerUseMcpToolName} with action={ComputerUseScreenshotAction}.
            Do NOT use vision_analyze, browser_vision, browser, terminal, or skill-discovery tools.
            After the screenshot tool returns, read the result and answer from that evidence only.]
            """;

        /// <summary>
        /// Appended on desktop-control turns so the model keeps one browser window in frame.
        /// </summary>
        public static string BuildComputerUseSessionSteering() =>
            $"""
            [DESKTOP CONTROL SESSION: Stay on ONE browser window for this task.
            Loop: {ComputerUseScreenshotAction} → act → {ComputerUseScreenshotAction} to verify.
            If the browser is not visible, call list_desktop_windows then focus_desktop_window before clicking.
            Do not switch to the Hermes browser tool for local desktop tasks.]
            """;

        public static string BuildBrowserCaptureMandatoryFirstAction() =>
            $"""
            [MANDATORY FIRST ACTION — browser tab task]
            You MUST call {BrowserCaptureTabToolName} as your FIRST tool call on this turn.
            It returns a screenshot_path and page_map.elements (selector, index, center).
            Do NOT use {ComputerUseMcpToolName} capture for browser tab content — desktop capture is
            polluted by the House Victoria overlay.

            FORBIDDEN on this turn: vision_analyze, browser_vision, browser, terminal, skill-discovery tools.

            After the tool returns, answer using the page_map and screenshot evidence.
            To interact: {BrowserClickToolName} / {BrowserTypeToolName} / {BrowserKeyToolName} (not computer_use).
            """;

        public static string BuildBrowserCaptureSteering() =>
            $"""
            [BROWSER TAB ROUTING: Use {BrowserCaptureTabToolName} for the active browser tab.
            Interact with {BrowserClickToolName} / {BrowserTypeToolName} / {BrowserKeyToolName} / {BrowserScrollToolName}
            using page_map selector or index (prefer), or x/y from page_map.elements[].center.
            Do NOT use computer_use clicks for browser tab work.]
            """;

        public static bool IsBrowserPageRequest(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return false;

            // Prefer Unreal Editor when the user is clearly talking about the Editor / uproject.
            if (IsUnrealEditorRequest(message))
                return false;

            return Regex.IsMatch(
                message,
                @"\b(browser tab|in (chrome|edge|firefox|brave)|web ?page|this page|on the page|website|url bar|what('?m| am i) (reading|viewing)|tab screenshot|page map)\b",
                RegexOptions.IgnoreCase);
        }

        public static bool IsUnrealEditorRequest(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return false;

            return Regex.IsMatch(
                message,
                @"\b(unreal editor|ue5? editor|content browser|uproject|blueprint editor|place (an? )?actor|spawn (an? )?actor|remote control|editor viewport|outliner|details panel|level editor|vessel (in|into) (the )?editor|build (the )?(vessel|house) in (unreal|ue))\b",
                RegexOptions.IgnoreCase);
        }

        public static string BuildUnrealEditorMandatoryFirstAction() =>
            $"""
            [MANDATORY FIRST ACTION — Unreal Editor task]
            You MUST call {UnrealEditorHealthToolName} as your FIRST tool call on this turn.
            If healthy, use {UnrealEditorSearchAssetsToolName} / {UnrealEditorScreenshotToolName} / {UnrealEditorGetPropertyToolName}
            before mutating. Writes require Allow Unreal Editor Control.
            Do NOT use {ComputerUseMcpToolName} to click the Unreal Editor chrome for these tasks.
            Do NOT use the world WebSocket embodiment path for Editor asset/property work.
            """;

        public static string BuildUnrealEditorSteering() =>
            $"""
            [UNREAL EDITOR ROUTING: Use unreal_editor_* Remote Control tools for the open Editor.
            Loop: health → search/screenshot → set/call/spawn (if allowed) → verify.
            World/vessel PIE control is a separate future track (:8888) — not these tools.]
            """;

        public static bool IsDesktopScreenshotRequest(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return false;

            if (IsBrowserPageRequest(message) || IsUnrealEditorRequest(message))
                return false;

            return Regex.IsMatch(
                message,
                @"\b(screenshot|desktop|active window|my screen|what('?m| am i) (looking at|seeing))\b",
                RegexOptions.IgnoreCase);
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
