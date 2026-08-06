using TMPro;
using UnityEngine;

namespace Base.UI.TMP
{
    /// <summary>
    /// Prevents Material/Memory leaks when modifying TextMeshPro material properties at runtime (Outline, Glow, Face Color).
    /// Tracks instantiated Material references and cleans them up on Destroy.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class TMPMaterialInstance : MonoBehaviour
    {
        private TMP_Text textComponent;
        private Material customMaterialInstance;

        private void Awake()
        {
            textComponent = GetComponent<TMP_Text>();
        }

        /// <summary>
        /// Gets or creates a safe runtime material instance for this TMP component.
        /// </summary>
        public Material MaterialInstance
        {
            get
            {
                if (customMaterialInstance == null && textComponent != null)
                {
                    customMaterialInstance = textComponent.fontMaterial;
                }
                return customMaterialInstance;
            }
        }

        public void SetOutlineColor(Color color)
        {
            if (MaterialInstance != null)
            {
                MaterialInstance.SetColor(ShaderUtilities.ID_OutlineColor, color);
            }
        }

        public void SetOutlineWidth(float width)
        {
            if (MaterialInstance != null)
            {
                MaterialInstance.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
            }
        }

        public void SetGlow(Color color, float power = 0.5f)
        {
            if (MaterialInstance != null)
            {
                MaterialInstance.EnableKeyword(ShaderUtilities.Keyword_Glow);
                MaterialInstance.SetColor(ShaderUtilities.ID_GlowColor, color);
                MaterialInstance.SetFloat(ShaderUtilities.ID_GlowPower, power);
            }
        }

        private void OnDestroy()
        {
            CleanUpMaterial();
        }

        private void CleanUpMaterial()
        {
            if (customMaterialInstance != null)
            {
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
}
