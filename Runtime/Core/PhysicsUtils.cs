using System.Collections.Generic;
using UnityEngine;

namespace HP.Framework.Common
{
    public static class PhysicsUtils
    {
        private static readonly RaycastHit2D[] SingleHit2DBuffer = new RaycastHit2D[1];

        #region Layer Mask

        public static bool IsInLayerMask(GameObject gameObject, LayerMask layerMask)
        {
            return gameObject != null && IsLayerInMask(gameObject.layer, layerMask);
        }

        public static bool IsInLayerMask(Component component, LayerMask layerMask)
        {
            return component != null && IsInLayerMask(component.gameObject, layerMask);
        }

        public static bool IsLayerInMask(int layer, LayerMask layerMask)
        {
            return layer >= 0 && layer <= 31 && (layerMask.value & (1 << layer)) != 0;
        }

        public static LayerMask AddLayerToMask(LayerMask layerMask, int layer)
        {
            if (layer >= 0 && layer <= 31)
            {
                layerMask.value |= 1 << layer;
            }

            return layerMask;
        }

        public static LayerMask RemoveLayerFromMask(LayerMask layerMask, int layer)
        {
            if (layer >= 0 && layer <= 31)
            {
                layerMask.value &= ~(1 << layer);
            }

            return layerMask;
        }

        #endregion

        #region Contact Filters

        public static ContactFilter2D CreateContactFilter2D(LayerMask layerMask, bool useTriggers = false)
        {
            ContactFilter2D filter = new ContactFilter2D
            {
                useTriggers = useTriggers,
                useLayerMask = true
            };
            filter.SetLayerMask(layerMask);
            return filter;
        }

        public static ContactFilter2D CreateCollisionFilter2D(Collider2D collider, bool useTriggers = false)
        {
            return collider == null
                ? CreateContactFilter2D(Physics2D.AllLayers, useTriggers)
                : CreateContactFilter2D(
                    Physics2D.GetLayerCollisionMask(collider.gameObject.layer),
                    useTriggers);
        }

        #endregion

        #region Cast 2D

        public static List<RaycastHit2D> CheckCollisions(Collider2D collider, Vector2 direction, float distance)
        {
            return CastCollider2D(collider, direction, distance);
        }

        public static List<RaycastHit2D> CastCollider2D(
            Collider2D collider,
            Vector2 direction,
            float distance,
            int bufferSize = 10,
            bool useTriggers = false)
        {
            if (collider == null || bufferSize <= 0)
            {
                return new List<RaycastHit2D>(0);
            }

            ContactFilter2D filter = CreateCollisionFilter2D(collider, useTriggers);
            return CastCollider2D(collider, direction, distance, filter, bufferSize);
        }

        public static List<RaycastHit2D> CastCollider2D(
            Collider2D collider,
            Vector2 direction,
            float distance,
            ContactFilter2D filter,
            int bufferSize = 10)
        {
            List<RaycastHit2D> hits = new List<RaycastHit2D>(Mathf.Max(0, bufferSize));
            if (collider == null || bufferSize <= 0)
            {
                return hits;
            }

            RaycastHit2D[] hitBuffer = new RaycastHit2D[bufferSize];
            int hitCount = CastCollider2DNonAlloc(
                collider,
                direction,
                distance,
                filter,
                hitBuffer);
            for (int i = 0; i < hitCount; i++)
            {
                hits.Add(hitBuffer[i]);
            }

            return hits;
        }

        /// <summary>
        /// Casts a Collider2D into a caller-owned result buffer without allocating.
        /// Returns the number of written hits.
        /// </summary>
        public static int CastCollider2DNonAlloc(
            Collider2D collider,
            Vector2 direction,
            float distance,
            ContactFilter2D filter,
            RaycastHit2D[] results)
        {
            if (collider == null || results == null || results.Length == 0)
            {
                return 0;
            }

            return collider.Cast(direction, filter, results, distance);
        }

        public static int CastCollider2DNonAlloc(
            Collider2D collider,
            Vector2 direction,
            float distance,
            LayerMask layerMask,
            RaycastHit2D[] results,
            bool useTriggers = false)
        {
            return CastCollider2DNonAlloc(
                collider,
                direction,
                distance,
                CreateContactFilter2D(layerMask, useTriggers),
                results);
        }

        public static bool HasCollision2D(
            Collider2D collider,
            Vector2 direction,
            float distance,
            LayerMask layerMask)
        {
            if (collider == null)
            {
                return false;
            }

            ContactFilter2D filter = CreateContactFilter2D(layerMask);
            return collider.Cast(direction, filter, SingleHit2DBuffer, distance) > 0;
        }

        #endregion

        #region Raycast 2D

        public static bool Raycast2D(
            Vector2 origin,
            Vector2 direction,
            float distance,
            LayerMask layerMask,
            out RaycastHit2D hit)
        {
            hit = Physics2D.Raycast(origin, direction, distance, layerMask);
            return hit.collider != null;
        }

        public static bool Raycast2D(
            Transform origin,
            Vector2 direction,
            float distance,
            LayerMask layerMask,
            out RaycastHit2D hit)
        {
            hit = default;
            return origin != null
                && Raycast2D(origin.position, direction, distance, layerMask, out hit);
        }

        public static bool ScreenRaycast2D(
            Camera camera,
            Vector2 screenPosition,
            LayerMask layerMask,
            out RaycastHit2D hit)
        {
            hit = default;
            if (camera == null)
            {
                return false;
            }

            Vector2 worldPosition = camera.ScreenToWorldPoint(screenPosition);
            hit = Physics2D.Linecast(worldPosition, worldPosition, layerMask);
            return hit.collider != null;
        }

        #endregion

        #region Overlap 2D

        public static bool OverlapPoint2D(Vector2 point, LayerMask layerMask, out Collider2D collider)
        {
            collider = Physics2D.OverlapPoint(point, layerMask);
            return collider != null;
        }

        public static Collider2D[] OverlapCircleAll2D(Vector2 point, float radius, LayerMask layerMask)
        {
            return Physics2D.OverlapCircleAll(point, Mathf.Max(0f, radius), layerMask);
        }

        public static Collider2D[] OverlapBoxAll2D(
            Vector2 point,
            Vector2 size,
            float angle,
            LayerMask layerMask)
        {
            return Physics2D.OverlapBoxAll(point, size, angle, layerMask);
        }

        public static int OverlapCircleNonAlloc2D(
            Vector2 point,
            float radius,
            LayerMask layerMask,
            Collider2D[] results,
            bool useTriggers = false)
        {
            if (results == null || results.Length == 0)
            {
                return 0;
            }

            ContactFilter2D filter = CreateContactFilter2D(layerMask, useTriggers);
            return Physics2D.OverlapCircle(point, Mathf.Max(0f, radius), filter, results);
        }

        public static int OverlapBoxNonAlloc2D(
            Vector2 point,
            Vector2 size,
            float angle,
            LayerMask layerMask,
            Collider2D[] results,
            bool useTriggers = false)
        {
            if (results == null || results.Length == 0)
            {
                return 0;
            }

            ContactFilter2D filter = CreateContactFilter2D(layerMask, useTriggers);
            return Physics2D.OverlapBox(point, size, angle, filter, results);
        }

        #endregion

        #region Raycast 3D

        public static bool Raycast3D(
            Vector3 origin,
            Vector3 direction,
            float distance,
            LayerMask layerMask,
            out RaycastHit hit)
        {
            return Physics.Raycast(origin, direction, out hit, distance, layerMask);
        }

        public static bool Raycast3D(
            Transform origin,
            Vector3 direction,
            float distance,
            LayerMask layerMask,
            out RaycastHit hit)
        {
            hit = default;
            return origin != null
                && Raycast3D(origin.position, direction, distance, layerMask, out hit);
        }

        public static bool ScreenRaycast3D(
            Camera camera,
            Vector2 screenPosition,
            float distance,
            LayerMask layerMask,
            out RaycastHit hit)
        {
            hit = default;
            if (camera == null)
            {
                return false;
            }

            Ray ray = camera.ScreenPointToRay(screenPosition);
            return Physics.Raycast(ray, out hit, distance, layerMask);
        }

        public static int ScreenRaycastNonAlloc3D(
            Camera camera,
            Vector2 screenPosition,
            float distance,
            LayerMask layerMask,
            RaycastHit[] results,
            QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.UseGlobal)
        {
            if (camera == null || results == null || results.Length == 0)
            {
                return 0;
            }

            return Physics.RaycastNonAlloc(
                camera.ScreenPointToRay(screenPosition),
                results,
                distance,
                layerMask,
                triggerInteraction);
        }

        #endregion

        #region Overlap 3D

        public static Collider[] OverlapSphereAll3D(Vector3 point, float radius, LayerMask layerMask)
        {
            return Physics.OverlapSphere(point, Mathf.Max(0f, radius), layerMask);
        }

        public static Collider[] OverlapBoxAll3D(
            Vector3 center,
            Vector3 halfExtents,
            Quaternion orientation,
            LayerMask layerMask)
        {
            return Physics.OverlapBox(center, halfExtents, orientation, layerMask);
        }

        public static int OverlapSphereNonAlloc3D(
            Vector3 point,
            float radius,
            LayerMask layerMask,
            Collider[] results,
            QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.UseGlobal)
        {
            if (results == null || results.Length == 0)
            {
                return 0;
            }

            return Physics.OverlapSphereNonAlloc(
                point,
                Mathf.Max(0f, radius),
                results,
                layerMask,
                triggerInteraction);
        }

        public static int OverlapBoxNonAlloc3D(
            Vector3 center,
            Vector3 halfExtents,
            Quaternion orientation,
            LayerMask layerMask,
            Collider[] results,
            QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.UseGlobal)
        {
            if (results == null || results.Length == 0)
            {
                return 0;
            }

            return Physics.OverlapBoxNonAlloc(
                center,
                halfExtents,
                results,
                orientation,
                layerMask,
                triggerInteraction);
        }

        #endregion
    }
}
