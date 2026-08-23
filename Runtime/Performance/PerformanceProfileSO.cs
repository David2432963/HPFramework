using UnityEngine;

namespace HP.Framework.Performance
{
    [CreateAssetMenu(menuName = "HP Framework/Performance/Profile", fileName = "PerformanceProfile")]
    public sealed class PerformanceProfileSO : ScriptableObject
    {
        [SerializeField] private PerformanceTier tier = PerformanceTier.Medium;
        [SerializeField, Min(1)] private int targetFrameRate = 60;
        [SerializeField] private int unityQualityLevel;
        [SerializeField, Min(0.01f)] private float lodBias = 1f;
        [SerializeField] private bool allowUnlimitedFrameRate;

        public PerformanceTier Tier => tier;
        public int TargetFrameRate => targetFrameRate;
        public int UnityQualityLevel => unityQualityLevel;
        public float LodBias => lodBias;
        public bool AllowUnlimitedFrameRate => allowUnlimitedFrameRate;
    }
}
