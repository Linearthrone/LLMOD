using HouseVictoria.Core.Models;

namespace HouseVictoria.Core.Interfaces
{
    /// <summary>
    /// Interface for data persistence operations
    /// </summary>
    public interface IPersistenceService
    {
        Task<T?> GetAsync<T>(string key) where T : class;
        Task SetAsync<T>(string key, T value) where T : class;
        Task DeleteAsync(string key);
        Task<bool> ExistsAsync(string key);
        Task<Dictionary<string, T>> GetAllAsync<T>() where T : class;
        Task ClearAllAsync();
    }

    /// <summary>
    /// Interface for memory storage (persistent and shared)
    /// </summary>
    public interface IMemoryService
    {
        /// <summary>
        /// Per-AI contact persistent memory
        /// </summary>
        Task AddMemoryAsync(string contactId, string memory);
        Task<List<string>> GetMemoriesAsync(string contactId);
        Task ClearMemoriesAsync(string contactId);

        /// <summary>
        /// Shared global knowledge base
        /// </summary>
        Task AddGlobalKnowledgeAsync(string knowledge);
        Task<List<string>> GetGlobalKnowledgeAsync();
        Task<List<string>> SearchGlobalKnowledgeAsync(string query);

        /// <summary>
        /// Data banks for context
        /// </summary>
        Task AddDataBankAsync(DataBank dataBank);
        Task<DataBank?> GetDataBankAsync(string bankId);
        Task<List<DataBank>> GetAllDataBanksAsync();
        Task AddDataToBankAsync(string bankId, string data);
        Task AddDataToBankAsync(string bankId, DataBankEntry entry);
        Task UpdateDataBankEntryAsync(string bankId, DataBankEntry entry);
        Task DeleteDataBankEntryAsync(string bankId, string entryId);
        Task DeleteDataBankAsync(string bankId);

        /// <summary>
        /// Memory v2: structured memory items
        /// </summary>
        Task UpsertMemoryAsync(MemoryItem item);
        Task<MemoryItem?> GetMemoryAsync(string id);
        Task<IReadOnlyList<MemorySearchResult>> SearchMemoryAsync(MemorySearchRequest request);
        Task<bool> DeleteMemoryAsync(string id);
        Task PinMemoryAsync(string id, bool pinned);
        Task TouchMemoryAsync(string id);
    }
}
