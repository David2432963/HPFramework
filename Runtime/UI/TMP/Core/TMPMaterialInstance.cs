using TMPro;
using UnityEngine;

namespace HP.Framework.UI.TMP
{
    /// <summary>
    /// Owns a dedicated TMP material instance and restores the shared material on teardown.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class TMPMaterialInstance : MonoBehaviour
    {
        private TMP_Text textComponent;
        private Material originalSharedMaterial;
        private Material customMaterialInstance;

        private void Awake()
        {
            textComponent = GetComponent<TMP_Text>();
            originalSharedMaterial = textComponent.fontSharedMaterial;
        }

        public Material MaterialInstance
        {
            get
            {
                if (customMaterialInstance == null && textComponent != null)
                {
                    Material source = originalSharedMaterial != null
                        ? originalSharedMaterial
                        : textComponent.fontSharedMaterial;
                    if (source == null)
                    {
                        return null;
                    }

                    customMaterialInstance = new Material(source)
                    {
                        name = source.name + " (TMP Instance)"
                    };
                    textComponent.fontMaterial = customMaterialInstance;
                }

                return customMaterialInstance;
            }
        }

        public void SetOutlineColor(Color color)
        {
            MaterialInstance?.SetColor(ShaderUtilities.ID_OutlineColor, color);
        }

        public void SetOutlineWidth(float width)
        {
            MaterialInstance?.SetFloat(
                ShaderUtilities.ID_OutlineWidth,
                Mathf.Max(0f, width));
        }

        public void SetGlow(Color color, float power = 0.5f)
        {
            Material material = MaterialInstance;
            if (material == null)
            {
                return;
            }

            material.EnableKeyword(ShaderUtilities.Keyword_Glow);
            material.SetColor(ShaderUtilities.ID_GlowColor, color);
            material.SetFloat(ShaderUtilities.ID_GlowPower, Mathf.Max(0f, power));
        }

        private void OnDestroy()
        {
            if (textComponent != null && originalSharedMaterial != null)
            {
                textComponent.fontSharedMaterial = originalSharedMaterial;
            }

            if (customMaterialInstance == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(customMaterialInstance);
            }
            else
            {
                DestroyImmediate(customMaterialInstance);
            }

            customMaterialInstance = null;
        }
    }
}


