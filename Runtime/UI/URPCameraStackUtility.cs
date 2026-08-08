namespace HP.Framework.UI
{
    using System;
    using System.Collections;
    using System.Reflection;
    using UnityEngine;

    /// <summary>
    /// Optional URP camera-stack integration without a hard assembly reference to URP.
    /// </summary>
    public static class URPCameraStackUtility
    {
        private const string AdditionalCameraDataTypeName =
            "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime";

        private static readonly Type AdditionalCameraDataType = Type.GetType(AdditionalCameraDataTypeName);
        private static readonly PropertyInfo RenderTypeProperty = AdditionalCameraDataType?.GetProperty("renderType");
        private static readonly PropertyInfo CameraStackProperty = AdditionalCameraDataType?.GetProperty("cameraStack");
        private static readonly Type CameraRenderTypeType = RenderTypeProperty?.PropertyType;
        private static readonly object BaseRenderType = ParseRenderType("Base");
        private static readonly object OverlayRenderType = ParseRenderType("Overlay");

        public static bool IsAvailable =>
            AdditionalCameraDataType != null
            && RenderTypeProperty != null
            && CameraStackProperty != null
            && CameraRenderTypeType != null
            && BaseRenderType != null
            && OverlayRenderType != null;

        public static bool ConfigureAsOverlay(Camera camera)
        {
            if (!IsAvailable || camera == null)
            {
                return false;
            }

            Component cameraData = GetOrAddAdditionalCameraData(camera);
            if (cameraData == null)
            {
                return false;
            }

            RenderTypeProperty.SetValue(cameraData, OverlayRenderType);
            return true;
        }

        public static bool AttachOverlay(Camera baseCamera, Camera overlayCamera)
        {
            if (!IsAvailable || baseCamera == null || overlayCamera == null || baseCamera == overlayCamera)
            {
                return false;
            }

            Component baseData = GetOrAddAdditionalCameraData(baseCamera);
            Component overlayData = GetOrAddAdditionalCameraData(overlayCamera);
            if (baseData == null || overlayData == null)
            {
                return false;
            }

            RenderTypeProperty.SetValue(baseData, BaseRenderType);
            RenderTypeProperty.SetValue(overlayData, OverlayRenderType);

            IList cameraStack;
            try
            {
                cameraStack = CameraStackProperty.GetValue(baseData) as IList;
            }
            catch (TargetInvocationException)
            {
                // URP can throw while resolving cameraStack when the Base camera has no active
                // ScriptableRenderer yet (for example during editor import or early startup).
                // Base/Overlay types are already correct; a later scene/load refresh can retry.
                return false;
            }

            if (cameraStack == null)
            {
                return false;
            }

            if (!cameraStack.Contains(overlayCamera))
            {
                cameraStack.Add(overlayCamera);
            }

            return true;
        }

        private static Component GetOrAddAdditionalCameraData(Camera camera)
        {
            Component cameraData = camera.GetComponent(AdditionalCameraDataType);
            return cameraData != null
                ? cameraData
                : camera.gameObject.AddComponent(AdditionalCameraDataType);
        }

        private static object ParseRenderType(string name)
        {
            if (CameraRenderTypeType == null || !CameraRenderTypeType.IsEnum)
            {
                return null;
            }

            try
            {
                return Enum.Parse(CameraRenderTypeType, name, ignoreCase: false);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }
}
