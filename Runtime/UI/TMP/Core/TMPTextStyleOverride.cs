using TMPro;
using UnityEngine;

namespace HP.Framework.UI.TMP
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    [AddComponentMenu("UI/TextMeshPro/Per Text Style Override")]
    public sealed class TMPTextStyleOverride : MonoBehaviour
    {
        public enum MaterialMode
        {
            UniqueInstance,
            SharedMaterialPreset
        }

        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
        private static readonly int UnderlayColorId = Shader.PropertyToID("_UnderlayColor");
        private static readonly int UnderlayOffsetXId = Shader.PropertyToID("_UnderlayOffsetX");
        private static readonly int UnderlayOffsetYId = Shader.PropertyToID("_UnderlayOffsetY");
        private static readonly int UnderlayDilateId = Shader.PropertyToID("_UnderlayDilate");
        private static readonly int UnderlaySoftnessId = Shader.PropertyToID("_UnderlaySoftness");
        private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
        private static readonly int GlowOffsetId = Shader.PropertyToID("_GlowOffset");
        private static readonly int GlowInnerId = Shader.PropertyToID("_GlowInner");
        private static readonly int GlowOuterId = Shader.PropertyToID("_GlowOuter");
        private static readonly int GlowPowerId = Shader.PropertyToID("_GlowPower");

        private const string UnderlayKeyword = "UNDERLAY_ON";
        private const string GlowKeyword = "GLOW_ON";

        [SerializeField] private MaterialMode _materialMode = MaterialMode.UniqueInstance;
        [SerializeField] private bool _overrideEnabled = true;
        [SerializeField, HideInInspector] private Material _baseSharedMaterial;
        [SerializeField] private Material _sharedMaterialPreset;

        [Header("Face")]
        [SerializeField, ColorUsage(true, true)] private Color _faceColor = Color.white;

        [Header("Outline")]
        [SerializeField, Range(0f, 1f)] private float _outlineWidth;
        [SerializeField, ColorUsage(true, true)] private Color _outlineColor = Color.black;

        [Header("Underlay Shadow")]
        [SerializeField] private bool _underlayEnabled;
        [SerializeField, ColorUsage(true, true)] private Color _underlayColor = Color.black;
        [SerializeField, Range(-1f, 1f)] private float _underlayOffsetX = 0.5f;
        [SerializeField, Range(-1f, 1f)] private float _underlayOffsetY = -0.5f;
        [SerializeField, Range(-1f, 1f)] private float _underlayDilate;
        [SerializeField, Range(0f, 1f)] private float _underlaySoftness;

        [Header("Glow")]
        [SerializeField] private bool _glowEnabled;
        [SerializeField, ColorUsage(true, true)] private Color _glowColor = Color.white;
        [SerializeField, Range(-1f, 1f)] private float _glowOffset;
        [SerializeField, Range(-1f, 1f)] private float _glowInner;
        [SerializeField, Range(-1f, 1f)] private float _glowOuter;
        [SerializeField, Range(0f, 1f)] private float _glowPower = 1f;

        [System.NonSerialized] private TMP_Text _text;
        [System.NonSerialized] private bool _hasApplied;
        [System.NonSerialized] private MaterialMode _appliedMode;

        public MaterialMode Mode => _materialMode;
        public Material SharedMaterialPreset => _sharedMaterialPreset;
        public TMP_Text Text => _text != null ? _text : GetComponent<TMP_Text>();
        public Material CurrentMaterial => Text != null ? Text.fontMaterial : null;

        private void OnEnable()
        {
            CacheText();
            ApplyStyle();
        }

        private void OnValidate()
        {
            CacheText();
            _hasApplied = false;
            ApplyStyle();
        }

        public void ApplyStyle()
        {
            CacheText();
            if (_text == null)
            {
                return;
            }

            if (_baseSharedMaterial == null)
            {
                _baseSharedMaterial = _text.fontSharedMaterial;
            }

            if (!_overrideEnabled)
            {
                RestoreSharedMaterial();
                return;
            }

            if (_materialMode == MaterialMode.SharedMaterialPreset)
            {
                if (_sharedMaterialPreset == null)
                {
                    return;
                }

                if (_text.fontSharedMaterial != _sharedMaterialPreset)
                {
                    _text.fontSharedMaterial = _sharedMaterialPreset;
                }

                _text.SetMaterialDirty();
                _hasApplied = true;
                _appliedMode = _materialMode;
                return;
            }

            if (!_hasApplied || _appliedMode != _materialMode)
            {
                RestoreSharedMaterial();
            }

            Material material = _text.fontMaterial;
            if (material == null)
            {
                return;
            }

            _text.color = _faceColor;
            SetColorIfSupported(material, OutlineColorId, _outlineColor);
            SetFloatIfSupported(material, OutlineWidthId, _outlineWidth);

            ApplyUnderlay(material);
            ApplyGlow(material);

            _text.UpdateMeshPadding();
            _text.SetVerticesDirty();
            _text.SetMaterialDirty();
            _hasApplied = true;
            _appliedMode = _materialMode;
        }

        public void RestoreBaseMaterial()
        {
            _overrideEnabled = false;
            RestoreSharedMaterial();
        }

        public void RebaseFromCurrentMaterial()
        {
            CacheText();
            if (_text == null)
            {
                return;
            }

            _baseSharedMaterial = _text.fontSharedMaterial;
            _hasApplied = false;
            ApplyStyle();
        }

        public void EnableOverride()
        {
            _overrideEnabled = true;
            _hasApplied = false;
            ApplyStyle();
        }

        private void CacheText()
        {
            if (_text == null)
            {
                _text = GetComponent<TMP_Text>();
            }
        }

        private void RestoreSharedMaterial()
        {
            if (_text == null || _baseSharedMaterial == null)
            {
                return;
            }

            if (_text.fontSharedMaterial != _baseSharedMaterial)
            {
                _text.fontSharedMaterial = _baseSharedMaterial;
            }

            _text.SetMaterialDirty();
            _hasApplied = false;
        }

        private void ApplyUnderlay(Material material)
        {
            SetKeyword(material, UnderlayKeyword, _underlayEnabled);
            SetColorIfSupported(material, UnderlayColorId, _underlayColor);
            SetFloatIfSupported(material, UnderlayOffsetXId, _underlayOffsetX);
            SetFloatIfSupported(material, UnderlayOffsetYId, _underlayOffsetY);
            SetFloatIfSupported(material, UnderlayDilateId, _underlayDilate);
            SetFloatIfSupported(material, UnderlaySoftnessId, _underlaySoftness);
        }

        private void ApplyGlow(Material material)
        {
            SetKeyword(material, GlowKeyword, _glowEnabled);
            SetColorIfSupported(material, GlowColorId, _glowColor);
            SetFloatIfSupported(material, GlowOffsetId, _glowOffset);
            SetFloatIfSupported(material, GlowInnerId, _glowInner);
            SetFloatIfSupported(material, GlowOuterId, _glowOuter);
            SetFloatIfSupported(material, GlowPowerId, _glowPower);
        }

        private static void SetColorIfSupported(Material material, int propertyId, Color value)
        {
            if (material.HasProperty(propertyId))
            {
                material.SetColor(propertyId, value);
            }
        }

        private static void SetFloatIfSupported(Material material, int propertyId, float value)
        {
            if (material.HasProperty(propertyId))
            {
                material.SetFloat(propertyId, value);
            }
        }

        private static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled)
            {
                material.EnableKeyword(keyword);
            }
            else
            {
                material.DisableKeyword(keyword);
            }
        }
    }
}
