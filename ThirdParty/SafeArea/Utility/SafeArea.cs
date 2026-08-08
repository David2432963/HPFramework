using UnityEngine;

namespace HP.Framework.SafeArea
{
    /// <summary>
    /// Applies Screen.safeArea to a RectTransform and supports lightweight Editor simulation.
    /// </summary>
    [DisallowMultipleComponent]
    public class SafeArea : MonoBehaviour
    {
        public enum SimDevice
        {
            None,
            iPhoneX,
            iPhoneXsMax,
            Pixel3XL_LSL,
            Pixel3XL_LSR
        }

        /// <summary>Editor-only simulated device. Runtime builds always use Screen.safeArea.</summary>
        public static SimDevice Sim = SimDevice.None;

        private static readonly Rect[] NSA_iPhoneX =
        {
            new Rect(0f, 102f / 2436f, 1f, 2202f / 2436f),
            new Rect(132f / 2436f, 63f / 1125f, 2172f / 2436f, 1062f / 1125f)
        };

        private static readonly Rect[] NSA_iPhoneXsMax =
        {
            new Rect(0f, 102f / 2688f, 1f, 2454f / 2688f),
            new Rect(132f / 2688f, 63f / 1242f, 2424f / 2688f, 1179f / 1242f)
        };

        private static readonly Rect[] NSA_Pixel3XL_LSL =
        {
            new Rect(0f, 0f, 1f, 2789f / 2960f),
            new Rect(0f, 0f, 2789f / 2960f, 1f)
        };

        private static readonly Rect[] NSA_Pixel3XL_LSR =
        {
            new Rect(0f, 0f, 1f, 2789f / 2960f),
            new Rect(171f / 2960f, 0f, 2789f / 2960f, 1f)
        };

        [SerializeField] private bool ConformX = true;
        [SerializeField] private bool ConformY = true;
        [SerializeField] private bool Logging;
        [SerializeField, Min(0.05f)] private float refreshInterval = 0.25f;

        private RectTransform Panel;
        private Rect LastSafeArea = new Rect(0f, 0f, 0f, 0f);
        private Vector2Int LastScreenSize = Vector2Int.zero;
        private ScreenOrientation LastOrientation = ScreenOrientation.AutoRotation;
        private SimDevice lastSimDevice = SimDevice.None;
        private float nextRefreshTime;

        private void Awake()
        {
            Panel = GetComponent<RectTransform>();
            if (Panel == null)
            {
                Debug.LogError("Cannot apply safe area - no RectTransform found on " + name, this);
                enabled = false;
                return;
            }

            Refresh(force: true);
        }

        private void OnEnable()
        {
            if (Panel != null)
            {
                nextRefreshTime = 0f;
                Refresh(force: true);
            }
        }

        private void Update()
        {
            bool simulationChanged = Application.isEditor && Sim != lastSimDevice;
            if (!simulationChanged && Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + refreshInterval;
            Refresh(force: simulationChanged);
        }

        private void OnRectTransformDimensionsChange()
        {
            if (Panel != null && isActiveAndEnabled)
            {
                Refresh(force: false);
            }
        }

        /// <summary>Forces an immediate safe-area refresh.</summary>
        public void RefreshNow()
        {
            Refresh(force: true);
        }

        private void Refresh(bool force)
        {
            if (Panel == null)
            {
                return;
            }

            Rect safeArea = GetSafeArea();
            if (!force
                && safeArea == LastSafeArea
                && Screen.width == LastScreenSize.x
                && Screen.height == LastScreenSize.y
                && Screen.orientation == LastOrientation
                && (!Application.isEditor || Sim == lastSimDevice))
            {
                return;
            }

            LastScreenSize.x = Screen.width;
            LastScreenSize.y = Screen.height;
            LastOrientation = Screen.orientation;
            lastSimDevice = Sim;
            ApplySafeArea(safeArea);
        }

        private Rect GetSafeArea()
        {
            Rect safeArea = Screen.safeArea;
            if (!Application.isEditor || Sim == SimDevice.None)
            {
                return safeArea;
            }

            bool portrait = Screen.height > Screen.width;
            Rect normalizedSafeArea;
            switch (Sim)
            {
                case SimDevice.iPhoneX:
                    normalizedSafeArea = NSA_iPhoneX[portrait ? 0 : 1];
                    break;
                case SimDevice.iPhoneXsMax:
                    normalizedSafeArea = NSA_iPhoneXsMax[portrait ? 0 : 1];
                    break;
                case SimDevice.Pixel3XL_LSL:
                    normalizedSafeArea = NSA_Pixel3XL_LSL[portrait ? 0 : 1];
                    break;
                case SimDevice.Pixel3XL_LSR:
                    normalizedSafeArea = NSA_Pixel3XL_LSR[portrait ? 0 : 1];
                    break;
                default:
                    return safeArea;
            }

            return new Rect(
                Screen.width * normalizedSafeArea.x,
                Screen.height * normalizedSafeArea.y,
                Screen.width * normalizedSafeArea.width,
                Screen.height * normalizedSafeArea.height);
        }

        private void ApplySafeArea(Rect safeArea)
        {
            LastSafeArea = safeArea;

            if (!ConformX)
            {
                safeArea.x = 0f;
                safeArea.width = Screen.width;
            }

            if (!ConformY)
            {
                safeArea.y = 0f;
                safeArea.height = Screen.height;
            }

            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;
            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            if (!IsFiniteAndNonNegative(anchorMin) || !IsFiniteAndNonNegative(anchorMax))
            {
                return;
            }

            Panel.anchorMin = anchorMin;
            Panel.anchorMax = anchorMax;

            if (Logging)
            {
                Debug.LogFormat(
                    this,
                    "New safe area applied to {0}: x={1}, y={2}, w={3}, h={4} on full extents w={5}, h={6}",
                    name,
                    safeArea.x,
                    safeArea.y,
                    safeArea.width,
                    safeArea.height,
                    Screen.width,
                    Screen.height);
            }
        }

        private static bool IsFiniteAndNonNegative(Vector2 value)
        {
            return !float.IsNaN(value.x)
                && !float.IsNaN(value.y)
                && !float.IsInfinity(value.x)
                && !float.IsInfinity(value.y)
                && value.x >= 0f
                && value.y >= 0f;
        }
    }
}
