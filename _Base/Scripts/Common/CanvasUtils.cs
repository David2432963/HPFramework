using System.Collections;
using System.Collections.Generic;
using Base.Extensions;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Base.Common
{
    public static class CanvasUtils
    {
        public static bool IsPointerOverUI()
        {
            if (EventSystem.current == null) return false;

            if (EventSystem.current.IsPointerOverGameObject())
            {
                return true;
            }
            else
            {
                PointerEventData pe = new PointerEventData(EventSystem.current);
                pe.position = Input.mousePosition;
                List<RaycastResult> hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pe, hits);
                return hits.Count > 0;
            }
        }

        public static bool IsPointerOverUIObject()
        {
            if (EventSystem.current == null) return false;

            var eventDataCurrentPosition = new PointerEventData(EventSystem.current);
            eventDataCurrentPosition.position = new Vector2(Input.mousePosition.x, Input.mousePosition.y);

            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventDataCurrentPosition, results);

            return results.Count > 0;
        }

        public static Vector3 FlexPosition(Transform pointTarget, Camera camUI, Canvas targetCanvas = null)
        {
            Vector3 worldPosition;
            if (targetCanvas != null && targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                RectTransformUtility.ScreenPointToWorldPointInRectangle(pointTarget.GetRectTransform(),
                    pointTarget.position, targetCanvas.worldCamera, out worldPosition);
            }
            else
            { 
                Vector3 screenPoint = camUI.WorldToScreenPoint(pointTarget.position);
                screenPoint.z = Mathf.Abs(camUI.transform.position.z);
                worldPosition = camUI.ScreenToWorldPoint(screenPoint);
            }
            return worldPosition;
        }

        public static Vector3 ConvertToUICameraSpace(Transform pointTarget, Camera mainCamera, Camera uiCamera, Canvas targetCanvas = null)
        {
            Vector3 uiWorldPosition;
            if (targetCanvas != null && targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                RectTransformUtility.ScreenPointToWorldPointInRectangle(pointTarget.GetRectTransform(),
                    pointTarget.position, uiCamera, out uiWorldPosition);
            }
            else
            {
                Vector3 screenPoint = mainCamera.WorldToScreenPoint(pointTarget.position);
                if (screenPoint.z < 0)
                {
                    BaseLog.LogWarning("Object is behind the main camera, unable to convert position.");
                    return Vector3.zero;
                }

                uiWorldPosition = uiCamera.ScreenToWorldPoint(screenPoint);
                uiWorldPosition.z = 0;
            }

            return uiWorldPosition;
        }

        public static Vector2 WorldToCanvasPosition(RectTransform canvas, Camera camera, Vector3 position)
        {
            Vector2 viewportPosition = camera.WorldToViewportPoint(position);

            viewportPosition.x *= canvas.sizeDelta.x;
            viewportPosition.y *= canvas.sizeDelta.y;

            viewportPosition.x -= canvas.sizeDelta.x * canvas.pivot.x;
            viewportPosition.y -= canvas.sizeDelta.y * canvas.pivot.y;
            return viewportPosition;
        }

        public static Vector3 WorldToCanvasPosition(RectTransform canvas, Camera camera, Transform transform)
        {
            Vector3 viewportPos = camera.WorldToViewportPoint(transform.position);

            var canPos = new Vector3(viewportPos.x.Remap(0.5f, 1.5f, 0f, canvas.rect.width),
                viewportPos.y.Remap(0.5f, 1.5f, 0f, canvas.rect.height), 0);

            canPos = canvas.transform.TransformPoint(canPos);
            canPos = transform.parent == null
                ? transform.InverseTransformPoint(canPos)
                : transform.parent.InverseTransformPoint(canPos);
            return canPos;
        }

        public static Vector2 SwitchToRectTransform(RectTransform from, RectTransform to)
        {
            Vector2 localPoint;
            Vector2 fromPivotDerivedOffset = new Vector2(from.rect.width * from.pivot.x + from.rect.xMin,
                from.rect.height * from.pivot.y + from.rect.yMin);
            Vector2 screenP = RectTransformUtility.WorldToScreenPoint(null, from.position);

            screenP += fromPivotDerivedOffset;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(to, screenP, null, out localPoint);
            Vector2 pivotDerivedOffset = new Vector2(to.rect.width * to.pivot.x + to.rect.xMin,
                to.rect.height * to.pivot.y + to.rect.yMin);
            return localPoint - pivotDerivedOffset;
        }

        public static Camera GetCanvasCamera(Transform canvasChild)
        {
            Canvas canvas = GetCanvas(canvasChild);
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                return canvas.worldCamera;
            }
            return null;
        }
 
        public static Canvas GetCanvas(Transform transform)
        {
            if (transform == null)
            {
                return null;
            }
            Canvas canvas = transform.GetComponent<Canvas>();
            if (canvas != null)
            {
                return canvas;
            }
            return GetCanvas(transform.parent);
        }
    }
}
