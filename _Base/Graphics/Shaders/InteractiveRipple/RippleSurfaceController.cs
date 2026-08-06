using System;
using UnityEngine;

namespace Base.VisualEffects
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    public sealed class RippleSurfaceController : MonoBehaviour
    {
        public const int MaxRipples = 8;

        private static readonly int EffectTimeId = Shader.PropertyToID("_EffectTime");
        private static readonly int RippleCountId = Shader.PropertyToID("_RippleCount");
        private static readonly int RippleDataId = Shader.PropertyToID("_RippleData");
        private static readonly int RippleAxisScaleId = Shader.PropertyToID("_RippleAxisScale");

        [Header("References")]
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private MeshFilter meshFilter;

        [Header("Playback")]
        [SerializeField] private bool playOnEnable = true;
        [SerializeField, Min(0f)] private float timeScale = 1f;

        [Header("Ripple Creation")]
        [SerializeField, Min(0f)] private float defaultStrength = 1f;
        [SerializeField] private bool rejectPointsOutsideMeshBounds = true;
        [SerializeField, Min(0f)] private float boundsTolerance = 0.05f;

        private readonly Vector4[] rippleData = new Vector4[MaxRipples];

        private MaterialPropertyBlock propertyBlock;
        private float effectTime;
        private int rippleCount;
        private int nextRippleIndex;
        private bool isPlaying;

        public float EffectTime => effectTime;
        public bool IsPlaying => isPlaying;
        public int ActiveSlotCount => rippleCount;

        private void Reset()
        {
            targetRenderer = GetComponent<Renderer>();
            meshFilter = GetComponent<MeshFilter>();
        }

        private void Awake()
        {
            InitializeReferences();
            propertyBlock = new MaterialPropertyBlock();
            isPlaying = playOnEnable;
        }

        private void OnEnable()
        {
            InitializeReferences();
            propertyBlock ??= new MaterialPropertyBlock();
            isPlaying = playOnEnable;
            ApplyShaderProperties();
        }

        private void Update()
        {
            if (isPlaying)
            {
                effectTime += Time.deltaTime * timeScale;
            }

            ApplyShaderProperties();
        }

        private void OnValidate()
        {
            timeScale = Mathf.Max(0f, timeScale);
            defaultStrength = Mathf.Max(0f, defaultStrength);
            boundsTolerance = Mathf.Max(0f, boundsTolerance);

            InitializeReferences();
        }

        public bool EmitRipple(Vector3 worldPosition)
        {
            return EmitRipple(worldPosition, defaultStrength);
        }

        public bool EmitRipple(Vector3 worldPosition, float strength)
        {
            InitializeReferences();

            if (targetRenderer == null) return false;

            Vector3 localPosition = transform.InverseTransformPoint(worldPosition);

            if (rejectPointsOutsideMeshBounds && !IsInsideSurfaceBounds(localPosition))
            {
                return false;
            }

            rippleData[nextRippleIndex] = new Vector4(
                localPosition.x,
                localPosition.z,
                effectTime,
                strength
            );

            nextRippleIndex = (nextRippleIndex + 1) % MaxRipples;
            rippleCount = Mathf.Min(rippleCount + 1, MaxRipples);

            ApplyShaderProperties();
            return true;
        }

        public void Pause() => isPlaying = false;
        public void Resume() => isPlaying = true;
        public void SetTimeScale(float value) => timeScale = Mathf.Max(0f, value);

        public void ClearRipples()
        {
            Array.Clear(rippleData, 0, rippleData.Length);
            rippleCount = 0;
            nextRippleIndex = 0;
            ApplyShaderProperties();
        }

        public void RestartEffect()
        {
            effectTime = 0f;
            ClearRipples();
        }

        private bool IsInsideSurfaceBounds(Vector3 localPosition)
        {
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                return true;
            }

            Bounds bounds = meshFilter.sharedMesh.bounds;
            bool insideX = localPosition.x >= bounds.min.x - boundsTolerance && localPosition.x <= bounds.max.x + boundsTolerance;
            bool insideZ = localPosition.z >= bounds.min.z - boundsTolerance && localPosition.z <= bounds.max.z + boundsTolerance;
            return insideX && insideZ;
        }

        private void ApplyShaderProperties()
        {
            if (targetRenderer == null) return;

            propertyBlock ??= new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(propertyBlock);

            propertyBlock.SetFloat(EffectTimeId, effectTime);
            propertyBlock.SetFloat(RippleCountId, rippleCount);
            propertyBlock.SetVectorArray(RippleDataId, rippleData);

            float worldUnitsPerLocalX = Mathf.Max(transform.TransformVector(Vector3.right).magnitude, 0.0001f);
            float worldUnitsPerLocalZ = Mathf.Max(transform.TransformVector(Vector3.forward).magnitude, 0.0001f);

            propertyBlock.SetVector(RippleAxisScaleId, new Vector4(worldUnitsPerLocalX, worldUnitsPerLocalZ, 0f, 0f));
            targetRenderer.SetPropertyBlock(propertyBlock);
        }

        private void InitializeReferences()
        {
            if (targetRenderer == null) targetRenderer = GetComponent<Renderer>();
            if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
        }
    }
}
