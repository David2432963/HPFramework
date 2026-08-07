using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Base.UI
{
    /// <summary>
    /// Drives UIHole/UIBlur shader properties without external tween dependencies.
    /// </summary>
    public sealed class UIHoleController : MonoBehaviour
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
        [SerializeField, Min(0f)] private float softness = 10f;
        [SerializeField] private AnimationCurve animationCurve =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private readonly Vector4[] holeCenters = new Vector4[MaxHoleCount];
        private readonly Vector4[] holeSizes = new Vector4[MaxHoleCount];
        private readonly float[] holeRadii = new float[MaxHoleCount];
        private readonly Vector4[] startCenters = new Vector4[MaxHoleCount];
        private readonly Vector4[] startSizes = new Vector4[MaxHoleCount];
        private readonly float[] startRadii = new float[MaxHoleCount];
        private readonly Vector4[] targetCenters = new Vector4[MaxHoleCount];
        private readonly Vector4[] targetSizes = new Vector4[MaxHoleCount];
        private readonly float[] targetRadii = new float[MaxHoleCount];

        private int currentHoleCount;
        private Coroutine animationRoutine;
        private Material originalMaterial;
        private Material materialInstance;
        private RectTransform selfRectTransform;

        private static readonly int HoleCentersId = Shader.PropertyToID("_HoleCenters");
        private static readonly int HoleSizesId = Shader.PropertyToID("_HoleSizes");
        private static readonly int HoleRadiiId = Shader.PropertyToID("_HoleRadii");
        private static readonly int HoleCountId = Shader.PropertyToID("_HoleCount");
        private static readonly int SoftnessId = Shader.PropertyToID("_Softness");
        private static readonly int UseHoleId = Shader.PropertyToID("_UseHole");

        public float Softness
        {
            get => softness;
            set
            {
                softness = Mathf.Max(0f, value);
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
                originalMaterial = targetGraphic.material;
                materialInstance = new Material(originalMaterial)
                {
                    name = originalMaterial.name + " (UIHole Instance)"
                };
                targetGraphic.material = materialInstance;
            }
        }

        public void SetHole(
            RectTransform target,
            float cornerRadius = 0f,
            Vector2 padding = default)
        {
            if (!TryBuildHole(target, cornerRadius, padding, out HoleData hole))
            {
                ClearHoles();
                return;
            }

            SetHoles(new[] { hole });
        }

        public void AnimateHole(
            RectTransform target,
            float duration,
            float cornerRadius = 0f,
            Vector2 padding = default)
        {
            if (!TryBuildHole(target, cornerRadius, padding, out HoleData hole))
            {
                ClearHoles();
                return;
            }

            AnimateHoles(new[] { hole }, duration);
        }

        public void SetHoles(HoleData[] holes)
        {
            StopAnimation();
            int count = holes == null ? 0 : Mathf.Min(holes.Length, MaxHoleCount);
            for (int i = 0; i < MaxHoleCount; i++)
            {
                if (i < count)
                {
                    holeCenters[i] = holes[i].center;
                    holeSizes[i] = holes[i].size;
                    holeRadii[i] = Mathf.Max(0f, holes[i].radius);
                }
                else
                {
                    holeCenters[i] = Vector4.zero;
                    holeSizes[i] = Vector4.zero;
                    holeRadii[i] = 0f;
                }
            }

            ApplyProperties(count);
        }

        public void AnimateHoles(HoleData[] holes, float duration)
        {
            StopAnimation();
            int targetCount = holes == null ? 0 : Mathf.Min(holes.Length, MaxHoleCount);
            int activeCountDuringTransition = Mathf.Max(currentHoleCount, targetCount);

            if (duration <= 0f || !isActiveAndEnabled)
            {
                SetHoles(holes);
                return;
            }

            if (currentHoleCount == 0)
            {
                for (int i = 0; i < targetCount; i++)
                {
                    holeCenters[i] = holes[i].center;
                    holeSizes[i] = Vector4.zero;
                    holeRadii[i] = Mathf.Max(0f, holes[i].radius);
                }
            }

            Array.Copy(holeCenters, startCenters, MaxHoleCount);
            Array.Copy(holeSizes, startSizes, MaxHoleCount);
            Array.Copy(holeRadii, startRadii, MaxHoleCount);

            for (int i = 0; i < MaxHoleCount; i++)
            {
                if (i < targetCount)
                {
                    targetCenters[i] = holes[i].center;
                    targetSizes[i] = holes[i].size;
                    targetRadii[i] = Mathf.Max(0f, holes[i].radius);
                }
                else
                {
                    targetCenters[i] = startCenters[i];
                    targetSizes[i] = Vector4.zero;
                    targetRadii[i] = 0f;
                }
            }

            animationRoutine = StartCoroutine(AnimateRoutine(
                duration,
                activeCountDuringTransition,
                targetCount));
        }

        public void ClearHoles()
        {
            SetHoles(null);
        }

        private IEnumerator AnimateRoutine(
            float duration,
            int activeCountDuringTransition,
            int targetCount)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                float t = animationCurve == null
                    ? normalized
                    : animationCurve.Evaluate(normalized);

                for (int i = 0; i < activeCountDuringTransition; i++)
                {
                    holeCenters[i] = Vector4.LerpUnclamped(
                        startCenters[i], targetCenters[i], t);
                    holeSizes[i] = Vector4.LerpUnclamped(
                        startSizes[i], targetSizes[i], t);
                    holeRadii[i] = Mathf.LerpUnclamped(
                        startRadii[i], targetRadii[i], t);
                }

                ApplyProperties(activeCountDuringTransition);
                yield return null;
            }

            Array.Copy(targetCenters, holeCenters, MaxHoleCount);
            Array.Copy(targetSizes, holeSizes, MaxHoleCount);
            Array.Copy(targetRadii, holeRadii, MaxHoleCount);
            ApplyProperties(targetCount);
            animationRoutine = null;
        }

        private bool TryBuildHole(
            RectTransform target,
            float cornerRadius,
            Vector2 padding,
            out HoleData hole)
        {
            hole = default;
            if (target == null || selfRectTransform == null)
            {
                return false;
            }

            Vector2 localPosition = selfRectTransform.InverseTransformPoint(target.position);
            Vector2 size = target.rect.size + padding;
            hole = new HoleData(localPosition, size, Mathf.Max(0f, cornerRadius));
            return true;
        }

        private void ApplyProperties(int count)
        {
            currentHoleCount = Mathf.Clamp(count, 0, MaxHoleCount);
            if (materialInstance == null)
            {
                return;
            }

            materialInstance.SetVectorArray(HoleCentersId, holeCenters);
            materialInstance.SetVectorArray(HoleSizesId, holeSizes);
            materialInstance.SetFloatArray(HoleRadiiId, holeRadii);
            materialInstance.SetInt(HoleCountId, currentHoleCount);
            if (materialInstance.HasProperty(SoftnessId))
            {
                materialInstance.SetFloat(SoftnessId, softness);
            }
            if (materialInstance.HasProperty(UseHoleId))
            {
                materialInstance.SetFloat(UseHoleId, currentHoleCount > 0 ? 1f : 0f);
            }
        }

        private void StopAnimation()
        {
            if (animationRoutine == null)
            {
                return;
            }

            StopCoroutine(animationRoutine);
            animationRoutine = null;
        }

        private void OnDisable()
        {
            StopAnimation();
        }

        private void OnDestroy()
        {
            StopAnimation();
            if (targetGraphic != null && targetGraphic.material == materialInstance)
            {
                targetGraphic.material = originalMaterial;
            }

            if (materialInstance != null)
            {
                if (Application.isPlaying) Destroy(materialInstance);
                else DestroyImmediate(materialInstance);
                materialInstance = null;
            }
        }
    }
}
