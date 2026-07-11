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

        /// <summary>computer-use-mcp action for screenshots (not "screenshot").</summary>
        public const string ComputerUseScreenshotAction = "get_screenshot";

        /// <summary>Hermes MCP tool for browser extension tab capture (house_victoria server).</summary>
        public const string BrowserCaptureTabToolName = "mcp_house_victoria_browser_capture_tab";

        public const string BrowserBridgeHealthToolName = "mcp_house_victoria_browser_bridge_health";

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

        public static string BuildHermesToolGuide(string? generatedFilesPath = null, bool includeComputerUse = false)
        {
            var pathLine = string.IsNullOrWhiteSpace(generatedFilesPath)
                ? "Media/GeneratedFiles"
                : generatedFilesPath;

            // Only advertise desktop control when the user has explicitly allowed it, so the model
            // is not nudged to grab the mouse/keyboard unprompted.
            var computerUseSection = includeComputerUse
                ? $"""


                DESKTOP CONTROL (the user has allowed you to act on their computer):
                | Tool | When to use |
                |------|-------------|
                | {ComputerUseMcpToolName} (action={ComputerUseScreenshotAction}) | See the LOCAL desktop — use this, NOT browser_vision |
                | {ComputerUseMcpToolName} (action=left_click / double_click / right_click) | Click a target on screen |
                | {ComputerUseMcpToolName} (action=type) | Type text into the focused field |
                | {ComputerUseMcpToolName} (action=scroll / key) | Scroll or press keys |
                | list_desktop_windows | When you lost the browser — list open window titles + bounds |
                | focus_desktop_window(title_contains) | Bring the target browser/app to the front BEFORE clicking |
                | {BrowserCaptureTabToolName} | ACTIVE browser tab screenshot + page map — use for web pages, NOT computer_use |
                | {BrowserBridgeHealthToolName} | Check extension bridge on :17891 if capture times out |
                | terminal | Run a shell command (NOT for desktop screenshots) |

                FORBIDDEN for local desktop / Maestro / the user's Chrome (they spawn ghost Chrome windows):
                - browser, browser_navigate, browser_vision, browser_snapshot (Hermes's own Chromium)
                - mcp__puppeteer__* / puppeteer_navigate / puppeteer_screenshot (separate testing Chromium)
                USE INSTEAD: {BrowserCaptureTabToolName} to see the user's tab; computer_use + focus_desktop_window to click/type.

                WINDOW FOCUS DISCIPLINE (critical on Windows):
                - Pick ONE target window (e.g. Chrome/Edge/Firefox) and stay on it for the whole task.
                - Do NOT open a parallel Hermes browser or Puppeteer session for local desktop work.
                - For tasks INSIDE a browser tab (web apps, docs, GitHub): call {BrowserCaptureTabToolName}
                  first — it captures the tab directly and returns a page_map with element coordinates.
                  Do NOT use {ComputerUseMcpToolName} get_screenshot for browser tabs (House Victoria overlay
                  pollutes desktop framebuffer captures).
                - For full-desktop / non-browser tasks: {ComputerUseScreenshotAction} via computer_use.
                - If House Victoria, Cursor, or another app is on top: call focus_desktop_window with a
                  substring from list_desktop_windows, then {ComputerUseScreenshotAction} again.
                - If a click does nothing, the window may not be focused — focus it, screenshot, click again.
                - Prefer keyboard shortcuts (Ctrl+L, Tab, Enter) over fragile coordinate clicks when possible.
                - After each action, take another {ComputerUseScreenshotAction} to confirm before continuing.

                For anything on the user's local desktop/screen/window: call {ComputerUseMcpToolName} with
                action={ComputerUseScreenshotAction} first. Do NOT use browser_vision — it cannot see the local desktop.
                Take a screenshot first to see the screen, act in small steps, and confirm the result.
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
            If the target window is off-screen or buried: list_desktop_windows → focus_desktop_window → get_screenshot.
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
            It returns a screenshot_path and page_map.elements (interactive elements with viewport coordinates).
            Do NOT use {ComputerUseMcpToolName} get_screenshot for browser tab content — desktop capture is
            polluted by the House Victoria overlay.

            FORBIDDEN on this turn: vision_analyze, browser_vision, browser, terminal, skill-discovery tools.

            After the tool returns, answer using the page_map and screenshot evidence.
            """;

        public static string BuildBrowserCaptureSteering() =>
            $"""
            [BROWSER TAB ROUTING: Use {BrowserCaptureTabToolName} for the active browser tab.
            Read page_map.elements for buttons/links/inputs and their center coordinates.
            Use computer_use clicks only after you know viewport coordinates from page_map.]
            """;

        public static bool IsBrowserPageRequest(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return false;

            return Regex.IsMatch(
                message,
                @"\b(browser tab|in (chrome|edge|firefox|brave)|web ?page|this page|on the page|website|url bar|what('?m| am i) (reading|viewing)|tab screenshot|page map)\b",
                RegexOptions.IgnoreCase);
        }

        public static bool IsDesktopScreenshotRequest(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return false;

            if (IsBrowserPageRequest(message))
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
