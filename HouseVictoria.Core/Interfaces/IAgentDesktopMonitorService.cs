using HouseVictoria.Core.Models;

namespace HouseVictoria.Core.Interfaces
{
    /// <summary>
    /// Live view of Hermes / computer-use desktop activity: screen frames + tool log lines.
    /// </summary>
    public interface IAgentDesktopMonitorService
    {
        bool IsSessionActive { get; }
        string? ActiveContactName { get; }
        AgentDesktopFrame? LatestFrame { get; }
        IReadOnlyList<AgentDesktopActivityEntry> RecentActivity { get; }

        event EventHandler<AgentDesktopSessionChangedEventArgs>? SessionChanged;
        event EventHandler<AgentDesktopActivityEntry>? ActivityAdded;
        event EventHandler<AgentDesktopFrame>? FrameCaptured;

        /// <summary>
        /// When true, chat messages sent to Victoria (via Hermes) include a live screenshot so
        /// she sees exactly what the user is looking at.
        /// </summary>
        bool ShareScreenWithAI { get; set; }
        event EventHandler<bool>? ShareScreenChanged;

        /// <summary>
        /// When true, Victoria may act on the desktop via the Hermes <c>computer_use</c> tool.
        /// This is the "allow control" concept, separate from passively sharing the screen.
        /// Persisted so it survives restart. Default false for safety.
        /// </summary>
        bool AllowComputerControl { get; set; }
        event EventHandler<bool>? AllowComputerControlChanged;

        void BeginSession(string? contactName = null);
        void EndSession();

        /// <summary>Keep screen capture running while the Desktop tab is visible.</summary>
        void RequestPreview();
        void ReleasePreview();

        /// <summary>Captures the current screen encoded as PNG bytes, or null if capture failed.</summary>
        byte[]? CaptureScreenPng(int maxWidth = 1280);
    }
}
