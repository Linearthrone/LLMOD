using HouseVictoria.Core.Models;

namespace HouseVictoria.Core.Interfaces
{
    /// <summary>
    /// Tracks live cognitive threads (chat, Maestro, email, autonomy, etc.) for the pulse widget.
    /// </summary>
    public interface ICognitionThoughtStreamService : IDisposable
    {
        IReadOnlyList<CognitionThoughtSubject> GetActiveSubjects();
        string? GetStreamCaption();
        string? GetLatestSnippet();

        void NotifyChatTurnStarted(string? contactName);
        void NotifyChatTurnEnded();
        void NotifyThoughtSnippet(string subjectId, string label, string snippet, double? arousalHint = null);
        void NotifyAutonomyVitals(CognitionVitalsSnapshot vitals);

        /// <summary>Live token delta from Hermes SSE streaming.</summary>
        void NotifyStreamDelta(string deltaText, string accumulatedSegment);

        /// <summary>hermes.tool.progress SSE event (running / completed).</summary>
        void NotifyHermesToolProgress(string toolName, string label, string status);

        event EventHandler<CognitionThoughtStreamChangedEventArgs>? StreamChanged;
    }
}
