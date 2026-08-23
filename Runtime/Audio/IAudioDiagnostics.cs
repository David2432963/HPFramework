namespace HP.Framework.Audio
{
    public readonly struct AudioServiceStats
    {
        public AudioServiceStats(
            int activeSfxVoices,
            int maxSfxVoices,
            int playedTotal,
            int droppedTotal,
            int stolenTotal)
        {
            ActiveSfxVoices = activeSfxVoices;
            MaxSfxVoices = maxSfxVoices;
            PlayedTotal = playedTotal;
            DroppedTotal = droppedTotal;
            StolenTotal = stolenTotal;
        }

        public int ActiveSfxVoices { get; }
        public int MaxSfxVoices { get; }
        public int PlayedTotal { get; }
        public int DroppedTotal { get; }
        public int StolenTotal { get; }
    }

    public interface IAudioDiagnostics
    {
        AudioServiceStats Stats { get; }
    }
}
