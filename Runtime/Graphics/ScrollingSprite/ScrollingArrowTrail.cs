using UnityEngine;

namespace HP.Framework.Graphics
{
    [DisallowMultipleComponent]
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class ScrollingArrowTrail : MonoBehaviour
    {
        private enum TrailAxis
        {
            LocalX,
            LocalY
        }

        private enum ScrollDirection
        {
            Forward,
            Reverse
        }

        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private TrailAxis trailAxis = TrailAxis.LocalY;
        [SerializeField] private ScrollDirection scrollDirection = ScrollDirection.Forward;
        [SerializeField, Min(0f)] private float scrollSpeed = 1f;
        [SerializeField, Range(0f, 0.5f)] private float edgeFadePortion = 0.1f;

        private const string ShaderName = "Base/ScrollingArrowTrail";

        private static readonly int SpriteUvRectId = Shader.PropertyToID("_SpriteUvRect");
        private static readonly int TrailSizeId = Shader.PropertyToID("_TrailSize");
        private static readonly int TileSizeId = Shader.PropertyToID("_TileSize");
        private static readonly int TrailAxisId = Shader.PropertyToID("_TrailAxis");
        private static readonly int ScrollDirectionId = Shader.PropertyToID("_ScrollDirection");
        private static readonly int ScrollSpeedId = Shader.PropertyToID("_ScrollSpeed");
        private static readonly int EdgeFadePortionId = Shader.PropertyToID("_EdgeFadePortion");
        private static readonly int FlipId = Shader.PropertyToID("_Flip");

        private MaterialPropertyBlock propertyBlock;
        private Sprite lastSprite;
        private Vector2 lastRendererSize;
        private bool lastFlipX;
        private bool lastFlipY;
        private TrailAxis lastTrailAxis;
        private ScrollDirection lastScrollDirection;
        private float lastScrollSpeed = float.NaN;
        private float lastEdgeFadePortion = float.NaN;

        private void Reset()
        {
            targetRenderer = GetComponent<SpriteRenderer>();
        }

        private void OnEnable()
        {
            ValidateMaterial();
            ApplyProperties(force: true);
        }

        private void LateUpdate()
        {
            ApplyProperties(force: false);
        }

        private void OnValidate()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<SpriteRenderer>();
            }

            if (!isActiveAndEnabled)
            {
                return;
            }

            ValidateMaterial();
            ApplyProperties(force: true);
        }

        private void OnDisable()
        {
            StopScrollingWithoutClearingOtherProperties();
            InvalidateCache();
        }

        public void Refresh()
        {
            ApplyProperties(force: true);
        }

        private void ValidateMaterial()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<SpriteRenderer>();
            }

            if (targetRenderer == null)
            {
                return;
            }

            Material material = targetRenderer.sharedMaterial;
            if (material != null && material.shader != null && material.shader.name == ShaderName)
            {
                return;
            }

            if (material == null)
            {
                Debug.LogWarning(
                    $"{nameof(ScrollingArrowTrail)} on {name} requires a material assigned on the SpriteRenderer using shader '{ShaderName}'.",
                    this);
                return;
            }

            Debug.LogWarning(
                $"{nameof(ScrollingArrowTrail)} on {name} needs the assigned material to use shader '{ShaderName}'.",
                this);
        }

        private void ApplyProperties(bool force)
        {
            if (targetRenderer == null || targetRenderer.sprite == null)
            {
                return;
            }

            Sprite sprite = targetRenderer.sprite;
            Vector2 rendererSize = targetRenderer.size;
            bool flipX = targetRenderer.flipX;
            bool flipY = targetRenderer.flipY;

            if (!force
                && sprite == lastSprite
                && rendererSize == lastRendererSize
                && flipX == lastFlipX
                && flipY == lastFlipY
                && trailAxis == lastTrailAxis
                && scrollDirection == lastScrollDirection
                && Mathf.Approximately(scrollSpeed, lastScrollSpeed)
                && Mathf.Approximately(edgeFadePortion, lastEdgeFadePortion))
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(propertyBlock);

            Texture texture = sprite.texture;
            Rect textureRect = sprite.textureRect;
            Vector4 spriteUvRect = new Vector4(
                textureRect.x / texture.width,
                textureRect.y / texture.height,
                textureRect.width / texture.width,
                textureRect.height / texture.height);

            Vector2 trailSize = new Vector2(
                Mathf.Max(0.0001f, rendererSize.x),
                Mathf.Max(0.0001f, rendererSize.y));
            Vector2 tileSize = new Vector2(
                Mathf.Max(0.0001f, sprite.bounds.size.x),
                Mathf.Max(0.0001f, sprite.bounds.size.y));

            propertyBlock.SetVector(SpriteUvRectId, spriteUvRect);
            propertyBlock.SetVector(TrailSizeId, trailSize);
            propertyBlock.SetVector(TileSizeId, tileSize);
            propertyBlock.SetFloat(TrailAxisId, trailAxis == TrailAxis.LocalX ? 0f : 1f);
            propertyBlock.SetFloat(ScrollDirectionId, scrollDirection == ScrollDirection.Forward ? 1f : -1f);
            propertyBlock.SetFloat(ScrollSpeedId, scrollSpeed);
            propertyBlock.SetFloat(EdgeFadePortionId, edgeFadePortion);
            propertyBlock.SetVector(FlipId, new Vector4(flipX ? 1f : 0f, flipY ? 1f : 0f, 0f, 0f));
            targetRenderer.SetPropertyBlock(propertyBlock);

            lastSprite = sprite;
            lastRendererSize = rendererSize;
            lastFlipX = flipX;
            lastFlipY = flipY;
            lastTrailAxis = trailAxis;
            lastScrollDirection = scrollDirection;
            lastScrollSpeed = scrollSpeed;
            lastEdgeFadePortion = edgeFadePortion;
        }

        private void StopScrollingWithoutClearingOtherProperties()
        {
            if (targetRenderer == null)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(ScrollSpeedId, 0f);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }

        private void InvalidateCache()
        {
            lastSprite = null;
            lastScrollSpeed = float.NaN;
            lastEdgeFadePortion = float.NaN;
        }
    }
}
