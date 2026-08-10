using UnityEngine;

namespace HP.Framework.Graphics
{
    /// <summary>
    /// Controls ScrollingSurfaceURP shader properties through a
    /// MaterialPropertyBlock to avoid creating material instances.
    /// Supports GPU instancing when the shader enables its instancing variant.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    public sealed class ScrollingSurfaceController : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int TilingId = Shader.PropertyToID("_Tiling");
        private static readonly int ScrollSpeedId = Shader.PropertyToID("_ScrollSpeed");
        private static readonly int ManualOffsetId = Shader.PropertyToID("_ManualOffset");
        private static readonly int AnimationEnabledId = Shader.PropertyToID("_AnimationEnabled");

        [Header("Surface Settings")]
        [SerializeField] private Color baseColor = Color.white;

        [Tooltip("Texture repetitions along the X and Y axes.")]
        [SerializeField] private Vector2 tiling = new Vector2(4f, 4f);

        [Tooltip("Texture scroll speed and direction in UV space.")]
        [SerializeField] private Vector2 scrollSpeed = new Vector2(0.1f, 0f);

        [Tooltip("Additional offset independent of animation time.")]
        [SerializeField] private Vector2 manualOffset = Vector2.zero;

        [Header("Runtime")]
        [SerializeField] private bool animationEnabled = true;

        private Renderer cachedRenderer;
        private MaterialPropertyBlock propertyBlock;

        private void Awake()
        {
            cachedRenderer = GetComponent<Renderer>();
            propertyBlock = new MaterialPropertyBlock();
            ApplyProperties();
        }

        private void OnEnable()
        {
            propertyBlock ??= new MaterialPropertyBlock();
            ApplyProperties();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            tiling.x = Mathf.Max(0.001f, tiling.x);
            tiling.y = Mathf.Max(0.001f, tiling.y);

            if (!Application.isPlaying)
            {
                cachedRenderer = GetComponent<Renderer>();
                propertyBlock ??= new MaterialPropertyBlock();
            }

            ApplyProperties();
        }
#endif

        /// <summary>
        /// Applies the current surface properties to the renderer.
        /// </summary>
        public void ApplyProperties()
        {
            propertyBlock ??= new MaterialPropertyBlock();
            cachedRenderer.GetPropertyBlock(propertyBlock);

            propertyBlock.SetColor(BaseColorId, baseColor);
            propertyBlock.SetVector(TilingId, new Vector4(tiling.x, tiling.y, 0f, 0f));
            propertyBlock.SetVector(
                ScrollSpeedId,
                new Vector4(scrollSpeed.x, scrollSpeed.y, 0f, 0f)
            );
            propertyBlock.SetVector(
                ManualOffsetId,
                new Vector4(manualOffset.x, manualOffset.y, 0f, 0f)
            );
            propertyBlock.SetFloat(AnimationEnabledId, animationEnabled ? 1f : 0f);

            cachedRenderer.SetPropertyBlock(propertyBlock);
        }

        public void SetAnimationEnabled(bool enabled)
        {
            animationEnabled = enabled;
            ApplyProperties();
        }

        public void SetScrollSpeed(Vector2 newSpeed)
        {
            scrollSpeed = newSpeed;
            ApplyProperties();
        }

        public void SetTiling(Vector2 newTiling)
        {
            tiling = new Vector2(
                Mathf.Max(0.001f, newTiling.x),
                Mathf.Max(0.001f, newTiling.y)
            );

            ApplyProperties();
        }

        public void SetManualOffset(Vector2 newOffset)
        {
            manualOffset = newOffset;
            ApplyProperties();
        }

        public void SetBaseColor(Color newColor)
        {
            baseColor = newColor;
            ApplyProperties();
        }
    }
}
