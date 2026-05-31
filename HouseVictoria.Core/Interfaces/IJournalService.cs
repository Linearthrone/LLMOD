using HouseVictoria.Core.Models;

namespace HouseVictoria.Core.Interfaces
{
    public interface IJournalService
    {
        event EventHandler<JournalUpdatedEventArgs>? JournalUpdated;

        Task<IReadOnlyList<ResearchJournal>> GetAllJournalsAsync();
        Task<ResearchJournal?> GetJournalAsync(string journalId);
        Task<ResearchJournal?> FindByProjectIdAsync(string projectId);
        Task<ResearchJournal?> FindByTopicAsync(string topic);

        Task<ResearchJournal> CreateJournalAsync(
            string title,
            string preface,
            string? topic = null,
            string? projectId = null,
            string? projectName = null);

        Task<ResearchJournal> FindOrCreateForProjectAsync(
            string projectId,
            string projectName,
            string preface);

        Task<ResearchJournal> FindOrCreateForTopicAsync(
            string topic,
            string preface,
            string? projectId = null,
            string? projectName = null);

        Task AddPageEntryAsync(string journalId, JournalPageEntry entry);
        Task AddReferenceAsync(string journalId, ReferencedMaterial reference, string? entryId = null);

        Task<ResearchJournal> RecordAutonomyActivityAsync(
            AutonomyJournalEntry autonomyEntry,
            string? topicTitle = null,
            string? prefaceReason = null);

        Task ConcludeJournalAsync(
            string journalId,
            string summary,
            string implications,
            IEnumerable<ReferencedMaterial>? citations = null);

        Task<ResearchJournal?> GenerateConclusionAsync(string journalId, AIContact contact);

        Task SyncFromAutonomyLogAsync();

        Task ConsolidateSimilarJournalsAsync();

        Task<string?> GetPriorWorkContextAsync(string topic, string? projectId = null, int maxEntries = 6);
    }
}
