using TMPro;
using UnityEngine;

namespace HP.Framework.UI.TMP
{
    /// <summary>
    /// Backward-compatible bridge for the retired material helper.
    /// New code should use <see cref="TMPTextStyleOverride"/> directly.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class TMPMaterialInstance : MonoBehaviour
    {
        private TMPTextStyleOverride _effects;
        private Color _outlineColor = Color.black;
        private float _outlineWidth;

        public Material MaterialInstance => Effects.CurrentMaterial;

        public void SetOutlineColor(Color color)
        {
            _outlineColor = color;
            Effects.SetOutline(_outlineColor, _outlineWidth);
        }

        public void SetOutlineWidth(float width)
        {
            _outlineWidth = width;
            Effects.SetOutline(_outlineColor, _outlineWidth);
        }

        public void SetGlow(Color color, float power = 0.5f)
        {
            Effects.SetGlow(color, power);
        }

        private TMPTextStyleOverride Effects
        {
            get
            {
                if (_effects == null)
                {
                    _effects = GetComponent<TMPTextStyleOverride>();
                    if (_effects == null)
                    {
                        _effects = gameObject.AddComponent<TMPTextStyleOverride>();
                    }
                }

                return _effects;
            }
        }
    }
}

