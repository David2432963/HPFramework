using System.Collections.Generic;

namespace HP.Framework.Audio
{
    internal readonly struct AudioVoiceCandidate
    {
        public AudioVoiceCandidate(bool active, int priority, long sequence)
        {
            Active = active;
            Priority = priority;
            Sequence = sequence;
        }

        public bool Active { get; }
        public int Priority { get; }
        public long Sequence { get; }
    }

    internal static class AudioVoiceSelector
    {
        public static int SelectStealCandidate(
            IReadOnlyList<AudioVoiceCandidate> voices,
            int requestPriority)
        {
            int selected = -1;
            for (int i = 0; i < voices.Count; i++)
            {
                AudioVoiceCandidate candidate = voices[i];
                if (!candidate.Active || candidate.Priority > requestPriority)
                {
                    continue;
                }

                if (selected < 0
                    || candidate.Priority < voices[selected].Priority
                    || (candidate.Priority == voices[selected].Priority
                        && candidate.Sequence < voices[selected].Sequence))
                {
                    selected = i;
                }
            }

            return selected;
        }
    }
}
