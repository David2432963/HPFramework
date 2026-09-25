using TMPro;
using UnityEngine;

namespace HP.Framework.UI.TMP
{
    public enum TMPOutlineDirection
    {
        Centered,
        Outside,
        Inside
    }

    /// <summary>
    /// Applies TMP outline, shadow, and glow through a material owned by this text only.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    [AddComponentMenu("UI/TextMeshPro/TMP Text Effects")]
    public sealed class TMPTextStyleOverride : MonoBehaviour
    {
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
        private static readonly int OutlineDirectionId = Shader.PropertyToID("_OutlineDirection");
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

        private const string OutlineKeyword = "OUTLINE_ON";
        private const string UnderlayKeyword = "UNDERLAY_ON";
        private const string GlowKeyword = "GLOW_ON";

        [SerializeField] private bool _overrideEnabled = true;

        [Header("Outline")]
        [SerializeField] private bool _outlineEnabled;
        [SerializeField] private TMPOutlineDirection _outlineDirection = TMPOutlineDirection.Centered;
        [SerializeField, Range(0f, 1f)] private float _outlineWidth;
        [SerializeField, ColorUsage(true, true)] private Color _outlineColor = Color.black;

        [Header("Shadow")]
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

        // Version 0 assets predate the explicit outline toggle. Their width remains authoritative.
        [SerializeField, HideInInspector] private int _serializedVersion;

        [System.NonSerialized] private TMP_Text _text;
        [System.NonSerialized] private Material _sourceMaterial;
        [System.NonSerialized] private Material _ownedMaterial;
        [System.NonSerialized] private Material _ownedSourceMaterial;

        public TMP_Text Text => _text != null ? _text : GetComponent<TMP_Text>();
        public Material CurrentMaterial => _ownedMaterial != null ? _ownedMaterial : Text != null ? Text.fontSharedMaterial : null;

        private bool IsOutlineEnabled => _serializedVersion == 0 ? _outlineWidth > 0f : _outlineEnabled;
        private bool HasActiveEffects => _overrideEnabled && (IsOutlineEnabled || _underlayEnabled || _glowEnabled);

        private void OnEnable()
        {
            CacheText();
            ApplyStyle();
        }

        private void OnValidate()
        {
            CacheText();
            ApplyStyle();
        }

        private void OnDisable()
        {
            RestoreSourceMaterial();
            ReleaseOwnedMaterial();
        }

        private void OnDestroy()
        {
            RestoreSourceMaterial();
            ReleaseOwnedMaterial();
        }

        public void ApplyStyle()
        {
            CacheText();
            if (_text == null)
            {
                return;
            }

            Material source = ResolveSourceMaterial();
            if (source == null)
            {
                return;
            }

            if (!HasActiveEffects)
            {
                RestoreSourceMaterial();
                ReleaseOwnedMaterial();
                return;
            }

            EnsureOwnedMaterial(source);
            if (_ownedMaterial == null)
            {
                return;
            }

            _ownedMaterial.CopyPropertiesFromMaterial(source);
            _ownedMaterial.shaderKeywords = source.shaderKeywords;
            ApplyEffects(_ownedMaterial);

            if (_text.fontSharedMaterial != _ownedMaterial)
            {
                _text.fontMaterial = _ownedMaterial;
            }

            _text.UpdateMeshPadding();
            _text.SetVerticesDirty();
            _text.SetMaterialDirty();
        }

        public void SetOutline(Color color, float width, bool enabled = true)
        {
            _serializedVersion = 1;
            _outlineEnabled = enabled;
            _outlineColor = color;
            _outlineWidth = Mathf.Clamp01(width);
            ApplyStyle();
        }

        public void SetGlow(Color color, float power = 0.5f, bool enabled = true)
        {
            _glowEnabled = enabled;
            _glowColor = color;
            _glowPower = Mathf.Clamp01(power);
            ApplyStyle();
        }

        public void EnableOverride()
        {
            _overrideEnabled = true;
            ApplyStyle();
        }

        public void RestoreBaseMaterial()
        {
            _overrideEnabled = false;
            ApplyStyle();
        }

        private void CacheText()
        {
            if (_text == null)
            {
                _text = GetComponent<TMP_Text>();
            }
        }

        private Material ResolveSourceMaterial()
        {
            Material current = _text.fontSharedMaterial;
            if (current != _ownedMaterial && IsCompatibleWithCurrentFont(current))
            {
                _sourceMaterial = current;
            }

            if (!IsCompatibleWithCurrentFont(_sourceMaterial))
            {
                _sourceMaterial = _text.font != null ? _text.font.material : current;
            }

            return _sourceMaterial;
        }

        private bool IsCompatibleWithCurrentFont(Material material)
        {
            if (material == null || _text == null || _text.font == null)
            {
                return material != null;
            }

            Texture materialAtlas = material.GetTexture(ShaderUtilities.ID_MainTex);
            Texture fontAtlas = _text.font.atlasTexture;
            return materialAtlas == null || fontAtlas == null || materialAtlas == fontAtlas;
        }

        private void EnsureOwnedMaterial(Material source)
        {
            if (_ownedMaterial != null && _ownedSourceMaterial == source)
            {
                return;
            }

            RestoreSourceMaterial();
            ReleaseOwnedMaterial();
            _sourceMaterial = source;
            _ownedSourceMaterial = source;
            _ownedMaterial = new Material(source)
            {
                name = source.name + " (TMP Text Effects)",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        private void RestoreSourceMaterial()
        {
            if (_text == null || _ownedMaterial == null || _text.fontSharedMaterial != _ownedMaterial)
            {
                return;
            }

            Material source = ResolveSourceMaterial();
            if (source != null && source != _ownedMaterial)
            {
                _text.fontSharedMaterial = source;
                _text.UpdateMeshPadding();
                _text.SetMaterialDirty();
            }
        }

        private void ReleaseOwnedMaterial()
        {
            if (_ownedMaterial == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_ownedMaterial);
            }
            else
            {
                DestroyImmediate(_ownedMaterial);
            }

            _ownedMaterial = null;
            _ownedSourceMaterial = null;
        }

        private void ApplyEffects(Material material)
        {
            SetColorIfSupported(material, OutlineColorId, _outlineColor);
            SetFloatIfSupported(material, OutlineWidthId, IsOutlineEnabled ? _outlineWidth : 0f);
            SetFloatIfSupported(material, OutlineDirectionId, (float)_outlineDirection);
            SetKeyword(material, OutlineKeyword, IsOutlineEnabled);

            SetKeyword(material, UnderlayKeyword, _underlayEnabled);
            SetColorIfSupported(material, UnderlayColorId, _underlayColor);
            SetFloatIfSupported(material, UnderlayOffsetXId, _underlayOffsetX);
            SetFloatIfSupported(material, UnderlayOffsetYId, _underlayOffsetY);
            SetFloatIfSupported(material, UnderlayDilateId, _underlayDilate);
            SetFloatIfSupported(material, UnderlaySoftnessId, _underlaySoftness);

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

