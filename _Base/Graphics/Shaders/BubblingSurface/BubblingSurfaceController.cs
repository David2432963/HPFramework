using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Base.VisualEffects
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    public sealed class BubblingSurfaceController : MonoBehaviour
    {
        private static readonly int BaseColorLowId = Shader.PropertyToID("_BaseColorLow");
        private static readonly int BaseColorHighId = Shader.PropertyToID("_BaseColorHigh");
        private static readonly int BubbleColorId = Shader.PropertyToID("_BubbleColor");
        private static readonly int NoiseTilingId = Shader.PropertyToID("_NoiseTiling");
        private static readonly int NoiseSpeedAId = Shader.PropertyToID("_NoiseSpeedA");
        private static readonly int NoiseSpeedBId = Shader.PropertyToID("_NoiseSpeedB");
        private static readonly int SecondNoiseScaleId = Shader.PropertyToID("_SecondNoiseScale");
        private static readonly int DisplacementId = Shader.PropertyToID("_Displacement");
        private static readonly int WaveContributionId = Shader.PropertyToID("_WaveContribution");
        private static readonly int WaveFrequencyId = Shader.PropertyToID("_WaveFrequency");
        private static readonly int WaveSpeedId = Shader.PropertyToID("_WaveSpeed");
        private static readonly int BubbleDensityId = Shader.PropertyToID("_BubbleDensity");
        private static readonly int BubbleSpeedId = Shader.PropertyToID("_BubbleSpeed");
        private static readonly int BubbleChanceId = Shader.PropertyToID("_BubbleChance");
        private static readonly int BubbleMinRadiusId = Shader.PropertyToID("_BubbleMinRadius");
        private static readonly int BubbleMaxRadiusId = Shader.PropertyToID("_BubbleMaxRadius");
        private static readonly int BubbleRingWidthId = Shader.PropertyToID("_BubbleRingWidth");
        private static readonly int BubbleEmissionId = Shader.PropertyToID("_BubbleEmission");
        private static readonly int SpecularStrengthId = Shader.PropertyToID("_SpecularStrength");
        private static readonly int SpecularPowerId = Shader.PropertyToID("_SpecularPower");
        private static readonly int SurfaceSizeId = Shader.PropertyToID("_SurfaceSize");
        private static readonly int NormalSampleDistanceId = Shader.PropertyToID("_NormalSampleDistance");
        private static readonly int EffectTimeId = Shader.PropertyToID("_EffectTime");

        [Header("Surface Colors")]
        [SerializeField] private Color baseColorLow = new Color(0.12f, 0.02f, 0.12f, 1f);
        [SerializeField] private Color baseColorHigh = new Color(0.8f, 0.12f, 0.55f, 1f);
        [ColorUsage(false, true)]
        [SerializeField] private Color bubbleColor = new Color(1.5f, 0.35f, 1.2f, 1f);

        [Header("Surface Motion")]
        [SerializeField] private Vector2 noiseTiling = new Vector2(4f, 4f);
        [SerializeField] private Vector2 noiseSpeedA = new Vector2(0.08f, 0.025f);
        [SerializeField] private Vector2 noiseSpeedB = new Vector2(-0.035f, 0.06f);
        [Min(0.001f)] [SerializeField] private float secondNoiseScale = 1.85f;
        [Min(0f)] [SerializeField] private float displacement = 0.12f;
        [Range(0f, 1f)] [SerializeField] private float waveContribution = 0.15f;
        [Min(0f)] [SerializeField] private float waveFrequency = 10f;
        [SerializeField] private float waveSpeed = 1.2f;

        [Header("Bubbles")]
        [Range(1f, 30f)] [SerializeField] private float bubbleDensity = 8f;
        [Min(0f)] [SerializeField] private float bubbleSpeed = 0.45f;
        [Range(0f, 1f)] [SerializeField] private float bubbleChance = 0.45f;
        [Range(0.01f, 0.5f)] [SerializeField] private float bubbleMinRadius = 0.04f;
        [Range(0.01f, 0.7f)] [SerializeField] private float bubbleMaxRadius = 0.34f;
        [Range(0.002f, 0.15f)] [SerializeField] private float bubbleRingWidth = 0.035f;
        [Min(0f)] [SerializeField] private float bubbleEmission = 2f;

        [Header("Lighting")]
        [Range(0f, 2f)] [SerializeField] private float specularStrength = 0.45f;
        [Range(1f, 128f)] [SerializeField] private float specularPower = 32f;
        [Range(0.001f, 0.1f)] [SerializeField] private float normalSampleDistance = 0.01f;

        [Header("Mesh Size")]
        [SerializeField] private bool autoDetectSurfaceSize = true;
        [SerializeField] private Vector2 manualSurfaceSize = new Vector2(10f, 10f);

        [Header("Animation Time")]
        [SerializeField] private bool animationEnabled = true;
        [Min(0f)] [SerializeField] private float animationTimeScale = 1f;
        [SerializeField] private bool useUnscaledTime;
        [SerializeField] private bool previewInEditMode = true;
        [SerializeField] private bool resetTimeOnEnable;

        private Renderer cachedRenderer;
        private MaterialPropertyBlock propertyBlock;
        private float effectTime;

#if UNITY_EDITOR
        private double lastEditorTime;
#endif

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            Initialize();

            if (resetTimeOnEnable)
            {
                effectTime = 0f;
            }

#if UNITY_EDITOR
            lastEditorTime = EditorApplication.timeSinceStartup;
#endif

            ApplyAllProperties();
        }

        private void Update()
        {
            float deltaTime = GetDeltaTime();

            if (animationEnabled && deltaTime > 0f)
            {
                effectTime += deltaTime * animationTimeScale;
                ApplyTimeOnly();
            }
        }

        private void Initialize()
        {
            if (cachedRenderer == null)
            {
                cachedRenderer = GetComponent<Renderer>();
            }

            propertyBlock ??= new MaterialPropertyBlock();
        }

        private float GetDeltaTime()
        {
            if (Application.isPlaying)
            {
                return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            }

#if UNITY_EDITOR
            double currentEditorTime = EditorApplication.timeSinceStartup;
            float editorDelta = (float)(currentEditorTime - lastEditorTime);
            lastEditorTime = currentEditorTime;

            return previewInEditMode ? Mathf.Clamp(editorDelta, 0f, 0.1f) : 0f;
#else
            return 0f;
#endif
        }

        private Vector2 GetSurfaceSize()
        {
            if (autoDetectSurfaceSize && TryGetComponent(out MeshFilter meshFilter) && meshFilter.sharedMesh != null)
            {
                Vector3 meshSize = meshFilter.sharedMesh.bounds.size;
                if (meshSize.x > 0.0001f && meshSize.z > 0.0001f)
                {
                    return new Vector2(meshSize.x, meshSize.z);
                }
            }

            return new Vector2(
                Mathf.Max(0.001f, manualSurfaceSize.x),
                Mathf.Max(0.001f, manualSurfaceSize.y)
            );
        }

        public void ApplyAllProperties()
        {
            Initialize();

            if (cachedRenderer == null) return;

            cachedRenderer.GetPropertyBlock(propertyBlock);

            propertyBlock.SetColor(BaseColorLowId, baseColorLow);
            propertyBlock.SetColor(BaseColorHighId, baseColorHigh);
            propertyBlock.SetColor(BubbleColorId, bubbleColor);
            propertyBlock.SetVector(NoiseTilingId, new Vector4(noiseTiling.x, noiseTiling.y, 0f, 0f));
            propertyBlock.SetVector(NoiseSpeedAId, new Vector4(noiseSpeedA.x, noiseSpeedA.y, 0f, 0f));
            propertyBlock.SetVector(NoiseSpeedBId, new Vector4(noiseSpeedB.x, noiseSpeedB.y, 0f, 0f));
            propertyBlock.SetFloat(SecondNoiseScaleId, secondNoiseScale);
            propertyBlock.SetFloat(DisplacementId, displacement);
            propertyBlock.SetFloat(WaveContributionId, waveContribution);
            propertyBlock.SetFloat(WaveFrequencyId, waveFrequency);
            propertyBlock.SetFloat(WaveSpeedId, waveSpeed);
            propertyBlock.SetFloat(BubbleDensityId, bubbleDensity);
            propertyBlock.SetFloat(BubbleSpeedId, bubbleSpeed);
            propertyBlock.SetFloat(BubbleChanceId, bubbleChance);
            propertyBlock.SetFloat(BubbleMinRadiusId, bubbleMinRadius);
            propertyBlock.SetFloat(BubbleMaxRadiusId, bubbleMaxRadius);
            propertyBlock.SetFloat(BubbleRingWidthId, bubbleRingWidth);
            propertyBlock.SetFloat(BubbleEmissionId, bubbleEmission);
            propertyBlock.SetFloat(SpecularStrengthId, specularStrength);
            propertyBlock.SetFloat(SpecularPowerId, specularPower);
            propertyBlock.SetFloat(NormalSampleDistanceId, normalSampleDistance);

            Vector2 surfaceSize = GetSurfaceSize();
            propertyBlock.SetVector(SurfaceSizeId, new Vector4(surfaceSize.x, surfaceSize.y, 0f, 0f));
            propertyBlock.SetFloat(EffectTimeId, effectTime);

            cachedRenderer.SetPropertyBlock(propertyBlock);
        }

        private void ApplyTimeOnly()
        {
            if (cachedRenderer == null || propertyBlock == null) return;

            propertyBlock.SetFloat(EffectTimeId, effectTime);
            cachedRenderer.SetPropertyBlock(propertyBlock);
        }

        public void SetAnimationEnabled(bool enabled) => animationEnabled = enabled;
        public void PauseAnimation() => animationEnabled = false;
        public void ResumeAnimation() => animationEnabled = true;
        public void RestartAnimation() { effectTime = 0f; ApplyTimeOnly(); }
        public void SetAnimationTimeScale(float newTimeScale) => animationTimeScale = Mathf.Max(0f, newTimeScale);
        public void SetDisplacement(float newDisplacement) { displacement = Mathf.Max(0f, newDisplacement); ApplyAllProperties(); }
        public void SetBubbleEmission(float newEmission) { bubbleEmission = Mathf.Max(0f, newEmission); ApplyAllProperties(); }
        public void SetBubbleChance(float newChance) { bubbleChance = Mathf.Clamp01(newChance); ApplyAllProperties(); }
        public void SetColors(Color newLowColor, Color newHighColor, Color newBubbleColor)
        {
            baseColorLow = newLowColor;
            baseColorHigh = newHighColor;
            bubbleColor = newBubbleColor;
            ApplyAllProperties();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            noiseTiling.x = Mathf.Max(0.001f, noiseTiling.x);
            noiseTiling.y = Mathf.Max(0.001f, noiseTiling.y);
            secondNoiseScale = Mathf.Max(0.001f, secondNoiseScale);
            displacement = Mathf.Max(0f, displacement);
            waveFrequency = Mathf.Max(0f, waveFrequency);

            bubbleDensity = Mathf.Clamp(bubbleDensity, 1f, 30f);
            bubbleSpeed = Mathf.Max(0f, bubbleSpeed);
            bubbleChance = Mathf.Clamp01(bubbleChance);
            bubbleMinRadius = Mathf.Clamp(bubbleMinRadius, 0.01f, 0.5f);
            bubbleMaxRadius = Mathf.Max(bubbleMinRadius, Mathf.Clamp(bubbleMaxRadius, 0.01f, 0.7f));
            bubbleRingWidth = Mathf.Clamp(bubbleRingWidth, 0.002f, 0.15f);
            bubbleEmission = Mathf.Max(0f, bubbleEmission);
            animationTimeScale = Mathf.Max(0f, animationTimeScale);
            manualSurfaceSize.x = Mathf.Max(0.001f, manualSurfaceSize.x);
            manualSurfaceSize.y = Mathf.Max(0.001f, manualSurfaceSize.y);

            Initialize();
            ApplyAllProperties();
        }
#endif
    }
}
