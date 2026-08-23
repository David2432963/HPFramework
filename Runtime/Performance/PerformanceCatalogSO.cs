using UnityEngine;

namespace HP.Framework.Performance
{
    [CreateAssetMenu(menuName = "HP Framework/Performance/Catalog", fileName = "PerformanceCatalog")]
    public sealed class PerformanceCatalogSO : ScriptableObject
    {
        [SerializeField] private PerformanceTier defaultTier = PerformanceTier.Medium;
        [SerializeField] private PerformanceProfileSO[] profiles = System.Array.Empty<PerformanceProfileSO>();

        public PerformanceTier DefaultTier => defaultTier;
        public IReadOnlyListView Profiles => new IReadOnlyListView(profiles);

        public bool TryResolveProfile(PerformanceTier requestedTier, out PerformanceProfileSO profile)
        {
            profile = Find(requestedTier);
            if (profile != null)
            {
                return true;
            }

            profile = Find(defaultTier);
            if (profile != null)
            {
                return true;
            }

            for (int i = 0; i < profiles.Length; i++)
            {
                if (profiles[i] != null)
                {
                    profile = profiles[i];
                    return true;
                }
            }

            return false;
        }

        private PerformanceProfileSO Find(PerformanceTier tier)
        {
            for (int i = 0; i < profiles.Length; i++)
            {
                PerformanceProfileSO candidate = profiles[i];
                if (candidate != null && candidate.Tier == tier)
                {
                    return candidate;
                }
            }

            return null;
        }

        public readonly struct IReadOnlyListView
        {
            private readonly PerformanceProfileSO[] values;
            internal IReadOnlyListView(PerformanceProfileSO[] values) => this.values = values;
            public int Count => values?.Length ?? 0;
            public PerformanceProfileSO this[int index] => values[index];
        }
    }
}
