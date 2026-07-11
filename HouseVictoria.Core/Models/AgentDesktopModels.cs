namespace HouseVictoria.Core.Models
{
    public enum AgentDesktopActivityKind
    {
        Info,
        ToolStart,
        ToolEnd,
        Screenshot,
        Error
    }

    public sealed class AgentDesktopActivityEntry
    {
        public DateTime Timestamp { get; init; }
        public AgentDesktopActivityKind Kind { get; init; }
        public string Text { get; init; } = string.Empty;
    }

    public sealed class AgentDesktopFrame
    {
        public int Width { get; init; }
        public int Height { get; init; }
        public byte[] Bgra32 { get; init; } = Array.Empty<byte>();
        public DateTime CapturedAt { get; init; }
        /// <summary>Human-readable capture source, e.g. browser tab title.</summary>
        public string? SourceLabel { get; init; }
        public bool IsBrowserTab { get; init; }
    }

    public sealed class AgentDesktopSessionChangedEventArgs : EventArgs
    {
        public bool IsActive { get; init; }
        public string? ContactName { get; init; }
    }
}
