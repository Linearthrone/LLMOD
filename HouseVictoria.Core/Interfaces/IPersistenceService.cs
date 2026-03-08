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
}
