using System.Security.Cryptography;
using System.Text;
using HouseVictoria.Core.Models;

namespace HouseVictoria.Services.Memory
{
    /// <summary>
    /// Embedding helpers: Ollama-backed embeddings via <see cref="OllamaEmbeddingClient"/>, with hash fallback.
    /// </summary>
    public static class EmbeddingHelper
    {
        /// <summary>Creates embeddings using Ollama when <paramref name="config"/> is provided; otherwise pseudo-embeddings.</summary>
        public static Task<double[]> CreateEmbeddingAsync(string text, AppConfig? config, int dimensions = 768)
        {
            if (config != null)
                return OllamaEmbeddingClient.CreateEmbeddingAsync(text, config);
            return Task.FromResult(CreatePseudoEmbedding(text, dimensions));
        }

        /// <summary>Deterministic pseudo-embedding (offline / tests).</summary>
        public static double[] CreatePseudoEmbedding(string? text, int dimensions)
        {
            var vector = new double[dimensions];
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text ?? string.Empty));
            for (int i = 0; i < dimensions; i++)
                vector[i] = bytes[i % bytes.Length] / 255.0;
            return vector;
        }
    }
}
