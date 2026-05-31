using HouseVictoria.Core.Models;

namespace HouseVictoria.Core.Interfaces
{
    /// <summary>
    /// Generates and manages After Action Reports (AARs) for completed projects.
    /// Reports land in the AAR tray for the user to accept (reward) or reject (with feedback).
    /// </summary>
    public interface IAarService
    {
        event EventHandler<AarReportsChangedEventArgs>? ReportsChanged;

        /// <summary>Reports still awaiting the user's accept/reject decision.</summary>
        Task<IReadOnlyList<AfterActionReport>> GetPendingReportsAsync();

        Task<AfterActionReport?> GetReportAsync(string reportId);

        /// <summary>Builds (or returns existing) an AAR for a completed project.</summary>
        Task<AfterActionReport?> GenerateForProjectAsync(string projectId);

        /// <summary>Accept the report: reward and praise her, then clear it from the tray.</summary>
        Task AcceptAsync(string reportId);

        /// <summary>Reject the report: record feedback, reopen the project with new settings, clear it.</summary>
        Task RejectAsync(string reportId, AarRejectionFeedback feedback);
    }
}
