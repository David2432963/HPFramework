using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[ExecuteAlways]
public sealed class UIGrayScaleEffect : MonoBehaviour
{
    private static readonly int EffectAmountId = Shader.PropertyToID("_EffectAmount");

    [SerializeField] private Material grayScaleMaterial;
    [SerializeField][Range(0f, 1f)] private float initialEffectAmount = 0f;

    private Image targetImage;
    private float effectAmount;
    private Material instanceMaterial;
    private Material originalMaterial;

    private void Awake()
    {
        targetImage = GetComponent<Image>();
        originalMaterial = targetImage.material;
        instanceMaterial = new Material(grayScaleMaterial);
        targetImage.material = instanceMaterial;
        effectAmount = initialEffectAmount;
        ApplyMaterialProperties();
    }
    private void OnValidate()
    {
        // Ensure we have a separate material instance for preview without modifying the original
        if (targetImage == null) return;

        // Create a new material instance if needed and assign it to the image
        if (instanceMaterial == null || targetImage.material != instanceMaterial)
        {
            instanceMaterial = new Material(grayScaleMaterial);
            targetImage.material = instanceMaterial;
        }

        // Update the effect amount based on the editor slider and apply to the material
        effectAmount = initialEffectAmount;
        instanceMaterial.SetFloat(EffectAmountId, effectAmount);
    }
    public void SetGrayscale(bool isGrayscale)
    {
        SetEffectAmount(isGrayscale ? 1f : 0f);
    }

    public void SetEffectAmount(float amount)
    {
        effectAmount = Mathf.Clamp01(amount);
        ApplyMaterialProperties();
    }

    private void ApplyMaterialProperties()
    {
        if (instanceMaterial == null)
        {
            return;
        }

        instanceMaterial.SetFloat(EffectAmountId, effectAmount);
    }

    private void OnDestroy()
    {
        if (targetImage != null)
        {
            targetImage.material = originalMaterial;
        }

        if (instanceMaterial != null)
        {
            if (Application.isPlaying)
            {
                Destroy(instanceMaterial);
            }
            else
            {
                DestroyImmediate(instanceMaterial);
            }
        }
    }
}
