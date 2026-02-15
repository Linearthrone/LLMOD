using System.Diagnostics;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HouseVictoria.Services.Memory
{
    /// <summary>
    /// Minimal pgvector client stub. Currently acts as a no-op placeholder until a Postgres backend is configured.
    /// </summary>
    public class PgVectorClient
    {
        private readonly string? _connectionString;

        public bool IsEnabled => !string.IsNullOrWhiteSpace(_connectionString);

        public PgVectorClient(string? connectionString)
        {
            _connectionString = connectionString;
        }

        public Task InitializeAsync()
        {
            // Placeholder: In a real implementation, create extension/table here.
            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                Debug.WriteLine("PgVectorClient skipped (no connection string).");
            }
            return Task.CompletedTask;
        }

        public Task UpsertAsync(string id, double[] embedding)
        {
            // Placeholder no-op. In production, store embedding vector.
            Debug.WriteLine($"PgVectorClient Upsert skipped for {id} (stub).");
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string id)
        {
            Debug.WriteLine($"PgVectorClient Delete skipped for {id} (stub).");
            return Task.CompletedTask;
        }

        public Task<IEnumerable<(string Id, double Score)>> SearchAsync(double[] embedding, int limit = 10)
        {
            // Placeholder: return empty result set.
            return Task.FromResult<IEnumerable<(string Id, double Score)>>(Array.Empty<(string, double)>());
        }
    }
}
