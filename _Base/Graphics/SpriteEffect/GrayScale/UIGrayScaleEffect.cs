using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[ExecuteAlways]
[RequireComponent(typeof(Image))]
public sealed class UIGrayScaleEffect : MonoBehaviour
{
    private static readonly int EffectAmountId = Shader.PropertyToID("_EffectAmount");

    [SerializeField] private Material grayScaleMaterial;
    [SerializeField, Range(0f, 1f)] private float initialEffectAmount;

    private Image targetImage;
    private float effectAmount;
    private Material originalMaterial;
    private Material instanceMaterial;

    private void OnEnable()
    {
        EnsureMaterialInstance();
        effectAmount = initialEffectAmount;
        ApplyMaterialProperties();
    }

    private void OnValidate()
    {
        initialEffectAmount = Mathf.Clamp01(initialEffectAmount);
        effectAmount = initialEffectAmount;
        if (instanceMaterial != null)
        {
            ApplyMaterialProperties();
        }
    }

    public void SetGrayscale(bool isGrayscale)
    {
        SetEffectAmount(isGrayscale ? 1f : 0f);
    }

    public void SetEffectAmount(float amount)
    {
        effectAmount = Mathf.Clamp01(amount);
        EnsureMaterialInstance();
        ApplyMaterialProperties();
    }

    private void EnsureMaterialInstance()
    {
        if (targetImage == null)
        {
            targetImage = GetComponent<Image>();
        }

        if (targetImage == null || grayScaleMaterial == null)
        {
            return;
        }

        if (instanceMaterial != null)
        {
            if (targetImage.material != instanceMaterial)
            {
                targetImage.material = instanceMaterial;
            }
            return;
        }

        originalMaterial = targetImage.material;
        instanceMaterial = new Material(grayScaleMaterial)
        {
            name = grayScaleMaterial.name + " (Grayscale Instance)"
        };
        targetImage.material = instanceMaterial;
    }

    private void ApplyMaterialProperties()
    {
        if (instanceMaterial != null && instanceMaterial.HasProperty(EffectAmountId))
        {
            instanceMaterial.SetFloat(EffectAmountId, effectAmount);
        }
    }

    private void OnDestroy()
    {
        if (targetImage != null && targetImage.material == instanceMaterial)
        {
            targetImage.material = originalMaterial;
        }

        if (instanceMaterial == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(instanceMaterial);
        }
        else
        {
            DestroyImmediate(instanceMaterial);
        }

        instanceMaterial = null;
    }
}
