using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace Base.UI
{
    /// <summary>
    /// Controller for driving the "UIHole" and "UIBlur" shaders on a UI Graphic component (e.g. Image).
    /// Used to punch single or multiple rounded rectangular holes into UI overlays (e.g. tutorial spotlights).
    /// 
    /// Example:
    /// <code>
    /// [SerializeField] private UIHoleController holeController;
    /// [SerializeField] private RectTransform targetButton;
    /// 
    /// private void HighlightButton()
    /// {
    ///     holeController.SetHole(targetButton, cornerRadius: 16f); // Snap instantly
    ///     holeController.AnimateHole(targetButton, 0.5f, cornerRadius: 16f); // Move smoothly
    /// }
    /// </code>
    /// </summary>

    public class UIHoleController : MonoBehaviour
    {
        public struct HoleData
        {
            public Vector2 center;
            public Vector2 size;
            public float radius;

            public HoleData(Vector2 center, Vector2 size, float radius)
            {
                this.center = center;
                this.size = size;
                this.radius = radius;
            }
        }

        private const int MaxHoleCount = 8;

        [SerializeField] private Graphic targetGraphic;
        [SerializeField] private float softness = 10f;

        private readonly Vector4[] holeCenters = new Vector4[MaxHoleCount];
        private readonly Vector4[] holeSizes = new Vector4[MaxHoleCount];
        private readonly float[] holeRadii = new float[MaxHoleCount];

        // Backup for animation
        private readonly Vector4[] startCenters = new Vector4[MaxHoleCount];
        private readonly Vector4[] startSizes = new Vector4[MaxHoleCount];
        private readonly float[] startRadii = new float[MaxHoleCount];

        private int currentHoleCount = 0;
        private Tween currentTween;
        private Material instantiatedMaterial;
        private RectTransform selfRectTransform;

        private static readonly int HoleCentersID = Shader.PropertyToID("_HoleCenters");
        private static readonly int HoleSizesID = Shader.PropertyToID("_HoleSizes");
        private static readonly int HoleRadiiID = Shader.PropertyToID("_HoleRadii");
        private static readonly int HoleCountID = Shader.PropertyToID("_HoleCount");
        private static readonly int SoftnessID = Shader.PropertyToID("_Softness");
        private static readonly int UseHoleID = Shader.PropertyToID("_UseHole");

        public float Softness
        {
            get => softness;
            set
            {
                softness = value;
                ApplyProperties(currentHoleCount);
            }
        }

        private void Awake()
        {
            if (targetGraphic == null)
            {
                targetGraphic = GetComponent<Graphic>();
            }

            selfRectTransform = transform as RectTransform;

            if (targetGraphic != null && targetGraphic.material != null)
            {
                instantiatedMaterial = new Material(targetGraphic.material);
                targetGraphic.material = instantiatedMaterial;
            }
        }

        /// <summary>
        /// Set a single hole positioned over a target RectTransform instantly.
        /// </summary>
        public void SetHole(RectTransform target, float cornerRadius = 0f, Vector2 padding = default)
        {
            if (target == null || selfRectTransform == null)
            {
                ClearHoles();
                return;
            }

            Vector3 worldPos = target.position;
            Vector2 localPos = selfRectTransform.InverseTransformPoint(worldPos);
            Vector2 size = target.rect.size + padding;

            SetHoles(new[] { new HoleData(localPos, size, cornerRadius) });
        }

        /// <summary>
        /// Smoothly transition to a single hole over a target RectTransform using DOTween.
        /// </summary>
        public void AnimateHole(RectTransform target, float duration, float cornerRadius = 0f, Vector2 padding = default, Ease ease = Ease.OutQuad)
        {
            if (target == null || selfRectTransform == null)
            {
                ClearHoles();
                return;
            }

            Vector3 worldPos = target.position;
            Vector2 localPos = selfRectTransform.InverseTransformPoint(worldPos);
            Vector2 size = target.rect.size + padding;

            AnimateHoles(new[] { new HoleData(localPos, size, cornerRadius) }, duration, ease);
        }

        /// <summary>
        /// Set multiple holes instantly.
        /// </summary>
        public void SetHoles(HoleData[] holes)
        {
            currentTween?.Kill();

            int count = holes != null ? Mathf.Min(holes.Length, MaxHoleCount) : 0;

            for (int i = 0; i < count; i++)
            {
                holeCenters[i] = holes[i].center;
                holeSizes[i] = holes[i].size;
                holeRadii[i] = holes[i].radius;
            }

            for (int i = count; i < MaxHoleCount; i++)
            {
                holeCenters[i] = Vector4.zero;
                holeSizes[i] = Vector4.zero;
                holeRadii[i] = 0f;
            }

            ApplyProperties(count);
        }

        /// <summary>
        /// Smoothly transition multiple holes.
        /// </summary>
        public void AnimateHoles(HoleData[] holes, float duration, Ease ease = Ease.OutQuad)
        {
            int targetCount = holes != null ? Mathf.Min(holes.Length, MaxHoleCount) : 0;
            int maxActiveCount = Mathf.Max(currentHoleCount, targetCount); // ensure we lerp currently visible holes

            // If transitioning from 0 holes to 1 hole, initialize starting position at target center with 0 size
            if (currentHoleCount == 0 && targetCount > 0)
            {
                for (int i = 0; i < targetCount; i++)
                {
                    holeCenters[i] = holes[i].center;
                    holeSizes[i] = Vector4.zero;
                    holeRadii[i] = holes[i].radius;
                }
            }
            
            // Backup current state to lerp from
            Array.Copy(holeCenters, startCenters, MaxHoleCount);
            Array.Copy(holeSizes, startSizes, MaxHoleCount);
            Array.Copy(holeRadii, startRadii, MaxHoleCount);

            // Prepare target arrays
            Vector4[] targetCenters = new Vector4[MaxHoleCount];
            Vector4[] targetSizes = new Vector4[MaxHoleCount];
            float[] targetRadii = new float[MaxHoleCount];

            for (int i = 0; i < targetCount; i++)
            {
                targetCenters[i] = holes[i].center;
                targetSizes[i] = holes[i].size;
                targetRadii[i] = holes[i].radius;
            }

            for (int i = targetCount; i < MaxHoleCount; i++)
            {
                targetCenters[i] = startCenters[i]; // shrink to current center
                targetSizes[i] = Vector4.zero;      // size approaches zero
                targetRadii[i] = 0f;
            }

            currentTween?.Kill();
            
            // We use DOTween.To to interpolate
            currentTween = DOTween.To(() => 0f, t =>
            {
                for (int i = 0; i < maxActiveCount; i++)
                {
                    holeCenters[i] = Vector4.Lerp(startCenters[i], targetCenters[i], t);
                    holeSizes[i] = Vector4.Lerp(startSizes[i], targetSizes[i], t);
                    holeRadii[i] = Mathf.Lerp(startRadii[i], targetRadii[i], t);
                }
                ApplyProperties(maxActiveCount); // keep max active during transition
            }, 1f, duration)
            .SetEase(ease)
            .OnComplete(() => ApplyProperties(targetCount)); // snap to target count at end
        }

        /// <summary>
        /// Clear all active holes on the shader overlay.
        /// </summary>
        public void ClearHoles()
        {
            SetHoles(null);
        }

        private void ApplyProperties(int count)
        {
            currentHoleCount = count;

            if (instantiatedMaterial == null && targetGraphic != null)
            {
                instantiatedMaterial = targetGraphic.material;
            }

            if (instantiatedMaterial == null) return;

            instantiatedMaterial.SetVectorArray(HoleCentersID, holeCenters);
            instantiatedMaterial.SetVectorArray(HoleSizesID, holeSizes);
            instantiatedMaterial.SetFloatArray(HoleRadiiID, holeRadii);
            instantiatedMaterial.SetInt(HoleCountID, count);
            
            if (instantiatedMaterial.HasProperty(SoftnessID))
            {
                instantiatedMaterial.SetFloat(SoftnessID, softness);
            }
            
            if (instantiatedMaterial.HasProperty(UseHoleID))
            {
                instantiatedMaterial.SetFloat(UseHoleID, count > 0 ? 1f : 0f);
            }
        }

        private void OnDestroy()
        {
            currentTween?.Kill();
            if (instantiatedMaterial != null)
            {
                Destroy(instantiatedMaterial);
                instantiatedMaterial = null;
            }
        }
    }
}
