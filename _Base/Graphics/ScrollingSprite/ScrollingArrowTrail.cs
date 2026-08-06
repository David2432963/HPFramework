using UnityEngine;

[DisallowMultipleComponent]
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class ScrollingArrowTrail : MonoBehaviour, ILateTickHandler
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

    private const string ShaderName = "KeyboardEscape/ScrollingArrowTrail";

    private static readonly int SpriteUvRectId = Shader.PropertyToID("_SpriteUvRect");
    private static readonly int TrailSizeId = Shader.PropertyToID("_TrailSize");
    private static readonly int TileSizeId = Shader.PropertyToID("_TileSize");
    private static readonly int TrailAxisId = Shader.PropertyToID("_TrailAxis");
    private static readonly int ScrollDirectionId = Shader.PropertyToID("_ScrollDirection");
    private static readonly int ScrollSpeedId = Shader.PropertyToID("_ScrollSpeed");
    private static readonly int EdgeFadePortionId = Shader.PropertyToID("_EdgeFadePortion");
    private static readonly int FlipId = Shader.PropertyToID("_Flip");

    private MaterialPropertyBlock propertyBlock;

    private void Reset()
    {
        targetRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        ValidateMaterial();
        ApplyProperties();
        if (Application.isPlaying)
        {
            Main.Instance.Register(this);
        }
    }

    public void LateTick(float deltaTime)
    {
        ApplyProperties();
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
        ApplyProperties();
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
        {
            Main.Instance?.Unregister(this);
        }

        ClearPropertyBlock();
    }

    private void OnDestroy()
    {
        ClearPropertyBlock();
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

    private void ApplyProperties()
    {
        if (targetRenderer == null || targetRenderer.sprite == null)
        {
            return;
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        Sprite sprite = targetRenderer.sprite;
        Texture texture = sprite.texture;
        Rect textureRect = sprite.textureRect;

        Vector4 spriteUvRect = new Vector4(
            textureRect.x / texture.width,
            textureRect.y / texture.height,
            textureRect.width / texture.width,
            textureRect.height / texture.height);

        Vector2 trailSize = new Vector2(
            Mathf.Max(0.0001f, targetRenderer.size.x),
            Mathf.Max(0.0001f, targetRenderer.size.y));

        Vector2 tileSize = new Vector2(
            Mathf.Max(0.0001f, sprite.bounds.size.x),
            Mathf.Max(0.0001f, sprite.bounds.size.y));

        propertyBlock.Clear();
        propertyBlock.SetVector(SpriteUvRectId, spriteUvRect);
        propertyBlock.SetVector(TrailSizeId, trailSize);
        propertyBlock.SetVector(TileSizeId, tileSize);
        propertyBlock.SetFloat(TrailAxisId, trailAxis == TrailAxis.LocalX ? 0f : 1f);
        propertyBlock.SetFloat(ScrollDirectionId, scrollDirection == ScrollDirection.Forward ? 1f : -1f);
        propertyBlock.SetFloat(ScrollSpeedId, scrollSpeed);
        propertyBlock.SetFloat(EdgeFadePortionId, edgeFadePortion);
        propertyBlock.SetVector(FlipId, new Vector4(targetRenderer.flipX ? 1f : 0f, targetRenderer.flipY ? 1f : 0f, 0f, 0f));
        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    private void ClearPropertyBlock()
    {
        if (targetRenderer == null)
        {
            return;
        }

        targetRenderer.SetPropertyBlock(null);
    }
}
