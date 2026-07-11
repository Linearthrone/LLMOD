namespace HouseVictoria.Core.Models
{
    /// <summary>One active cognitive thread shown as a colored wavelength on the pulse widget.</summary>
    public sealed class CognitionThoughtSubject
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        /// <summary>Latest visible thought or tool action (trimmed).</summary>
        public string? LatestSnippet { get; set; }
        public CognitionVitalRhythm Rhythm { get; set; } = CognitionVitalRhythm.Resting;
        public double BeatsPerMinute { get; set; } = 52;
        /// <summary>0–1 waveform amplitude.</summary>
        public double Intensity { get; set; } = 0.25;
        /// <summary>0–1 blend weight — higher = more visually prominent.</summary>
        public double AttentionWeight { get; set; } = 0.5;
        public string WaveColorHex { get; set; } = "#4FC3F7";
        public DateTime LastPulseUtc { get; set; } = DateTime.UtcNow;
    }

    public sealed class CognitionThoughtStreamChangedEventArgs : EventArgs
    {
        public IReadOnlyList<CognitionThoughtSubject> Subjects { get; init; } = Array.Empty<CognitionThoughtSubject>();
        public string? Caption { get; init; }
        public string? LatestSnippet { get; init; }
    }
}
