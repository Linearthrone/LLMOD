using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Npgsql;

namespace HouseVictoria.Services.Memory
{
    /// <summary>
    /// Postgres + pgvector storage for memory embeddings (cosine distance). Table: <c>house_victoria_memory_embeddings</c>.
    /// Uses text vector literals compatible with the pgvector extension.
    /// </summary>
    public class PgVectorClient : IAsyncDisposable
    {
        private readonly string? _connectionString;
        private readonly int _dimensions;
        private NpgsqlDataSource? _dataSource;
        private bool _initialized;

        public bool IsEnabled => !string.IsNullOrWhiteSpace(_connectionString);

        public PgVectorClient(string? connectionString, int dimensions = 768)
        {
            _connectionString = connectionString;
            _dimensions = dimensions > 0 ? dimensions : 768;
        }

        public async Task InitializeAsync()
        {
            if (!IsEnabled || _initialized)
                return;

            _dataSource = NpgsqlDataSource.Create(_connectionString!);

            await using var conn = await _dataSource.OpenConnectionAsync().ConfigureAwait(false);
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "CREATE EXTENSION IF NOT EXISTS vector;";
                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            var dim = _dimensions.ToString(CultureInfo.InvariantCulture);
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $@"
CREATE TABLE IF NOT EXISTS house_victoria_memory_embeddings (
  id TEXT PRIMARY KEY,
  content TEXT NOT NULL,
  embedding vector({dim})
);";
                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            _initialized = true;
        }

        private async Task EnsureInitAsync()
        {
            if (!IsEnabled)
                return;
            if (!_initialized)
                await InitializeAsync().ConfigureAwait(false);
        }

        private static string FormatVectorLiteral(IReadOnlyList<double> embedding)
        {
            return "[" + string.Join(",", embedding.Select(d => d.ToString("G17", CultureInfo.InvariantCulture))) + "]";

        }

        /// <summary>Upsert embedding and optional content snapshot for MCP / hybrid search.</summary>
        public async Task UpsertAsync(string id, double[] embedding, string? content = null)
        {
            await EnsureInitAsync().ConfigureAwait(false);
            if (_dataSource == null || embedding == null || embedding.Length == 0)
                return;

            if (embedding.Length != _dimensions)
            {
                Debug.WriteLine($"PgVectorClient: embedding length {embedding.Length} != configured {_dimensions}; skip upsert.");
                return;
            }

            var literal = FormatVectorLiteral(embedding);
            var text = content ?? string.Empty;

            await using var conn = await _dataSource!.OpenConnectionAsync().ConfigureAwait(false);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
INSERT INTO house_victoria_memory_embeddings (id, content, embedding)
VALUES (@id, @content, CAST(@vec AS vector))
ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, embedding = EXCLUDED.embedding;";
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("content", text);
            cmd.Parameters.AddWithValue("vec", literal);

            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        public async Task DeleteAsync(string id)
        {
            await EnsureInitAsync().ConfigureAwait(false);
            if (_dataSource == null)
                return;

            await using var conn = await _dataSource.OpenConnectionAsync().ConfigureAwait(false);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM house_victoria_memory_embeddings WHERE id = @id;";
            cmd.Parameters.AddWithValue("id", id);
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        public async Task<IEnumerable<(string Id, double Score)>> SearchAsync(double[] embedding, int limit = 10)
        {
            await EnsureInitAsync().ConfigureAwait(false);
            if (_dataSource == null || embedding == null || embedding.Length == 0)
                return Array.Empty<(string, double)>();

            if (embedding.Length != _dimensions)
            {
                Debug.WriteLine("PgVectorClient.Search: dimension mismatch.");
                return Array.Empty<(string, double)>();
            }

            var literal = FormatVectorLiteral(embedding);
            var results = new List<(string Id, double Score)>();

            await using var conn = await _dataSource.OpenConnectionAsync().ConfigureAwait(false);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
SELECT id, 1 - (embedding <=> CAST(@q AS vector)) AS score
FROM house_victoria_memory_embeddings
ORDER BY embedding <=> CAST(@q AS vector)
LIMIT @lim;";
            cmd.Parameters.AddWithValue("q", literal);
            cmd.Parameters.AddWithValue("lim", limit);

            await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var id = reader.GetString(0);
                var score = reader.GetDouble(1);
                results.Add((id, score));
            }

            return results;
        }

        public async ValueTask DisposeAsync()
        {
            if (_dataSource != null)
                await _dataSource.DisposeAsync().ConfigureAwait(false);
            _dataSource = null;
        }
    }
}
