using UnityEngine;

namespace Base.VisualEffects
{
    /// <summary>
    /// Điều khiển thông số của shader ScrollingSurfaceURP bằng
    /// MaterialPropertyBlock để tránh tạo material instance riêng.
    /// Hỗ trợ GPU Instancing nếu shader có bật Multi Compile Instancing.
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

        [Tooltip("Số lần texture lặp theo trục X và Y.")]
        [SerializeField] private Vector2 tiling = new Vector2(4f, 4f);

        [Tooltip("Tốc độ và hướng texture chạy theo UV.")]
        [SerializeField] private Vector2 scrollSpeed = new Vector2(0.1f, 0f);

        [Tooltip("Offset bổ sung, không phụ thuộc thời gian.")]
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
        /// Áp dụng toàn bộ thông số hiện tại lên Renderer.
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
