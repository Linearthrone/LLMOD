namespace HouseVictoria.Core.Interfaces
{
    /// <summary>
    /// Interface for Text-to-Speech service
    /// </summary>
    public interface ITTSService
    {
        /// <summary>
        /// Converts text to speech audio data
        /// </summary>
        /// <param name="text">Text to convert</param>
        /// <param name="voice">Optional voice name</param>
        /// <param name="speed">Speech speed (0.5 to 2.0, default 1.0)</param>
        /// <returns>Audio data as byte array (WAV format)</returns>
        Task<byte[]?> SynthesizeSpeechAsync(string text, string? voice = null, float speed = 1.0f);

        /// <summary>
        /// Checks if TTS service is available
        /// </summary>
        Task<bool> IsAvailableAsync();

        /// <summary>
        /// Gets a list of available voice models (Piper + optional Windows TTS fallback)
        /// </summary>
        /// <returns>List of available voice names/identifiers</returns>
        Task<List<string>> GetAvailableVoicesAsync();

        /// <summary>
        /// Gets only Piper TTS voice models (from Piper server or local Piper data dir). Does not include Windows TTS voices.
        /// </summary>
        /// <returns>List of Piper voice names/identifiers</returns>
        Task<List<string>> GetAvailablePiperVoicesAsync();
    }
}
