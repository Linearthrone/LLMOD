using System.Security.Cryptography;
using System.Text;

namespace HouseVictoria.Services.Memory
{
    /// <summary>
    /// Lightweight embedding helper placeholder. For production, replace with a true embedding model.
    /// </summary>
    public static class EmbeddingHelper
    {
        public static Task<double[]> CreateEmbeddingAsync(string text, int dimensions = 64)
        {
            // Simple hash-based pseudo-embedding to avoid extra dependencies.
            var vector = new double[dimensions];
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text ?? string.Empty));

            for (int i = 0; i < dimensions; i++)
            {
                vector[i] = bytes[i % bytes.Length] / 255.0;
            }

            return Task.FromResult(vector);
        }
    }
}
