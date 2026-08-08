using UnityEngine;

namespace HP.Framework.Graphics
{
    [DisallowMultipleComponent]
    public sealed class CharacterRippleEmitter : MonoBehaviour
    {
        public enum EmissionMode
        {
            AnimationEventOnly,
            DistanceBased
        }

        [Header("Mode")]
        [SerializeField] private EmissionMode emissionMode = EmissionMode.DistanceBased;

        [Header("Probe Points")]
        [SerializeField] private Transform centerProbe;
        [SerializeField] private Transform leftFoot;
        [SerializeField] private Transform rightFoot;

        [Header("Ground Detection")]
        [SerializeField] private LayerMask surfaceLayerMask = ~0;
        [SerializeField, Min(0f)] private float rayStartOffset = 0.15f;
        [SerializeField, Min(0.01f)] private float rayDistance = 1.5f;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

        [Header("Distance Based Mode")]
        [SerializeField, Min(0.01f)] private float distancePerRipple = 0.8f;
        [SerializeField, Min(0f)] private float minimumInterval = 0.12f;

        [Header("Strength")]
        [SerializeField, Min(0f)] private float baseStrength = 1f;
        [SerializeField, Min(0f)] private float speedStrengthMultiplier = 0.06f;
        [SerializeField, Min(0f)] private float maximumStrength = 2f;

        private Vector3 lastFramePosition;
        private Vector3 lastRipplePosition;
        private float currentSpeed;
        private float lastRippleTime = float.NegativeInfinity;
        private bool hasRipplePosition;

        private void Awake()
        {
            lastFramePosition = transform.position;
        }

        private void OnEnable()
        {
            lastFramePosition = transform.position;
            hasRipplePosition = false;
        }

        private void Update()
        {
            UpdateMovementSpeed();

            if (emissionMode != EmissionMode.DistanceBased)
            {
                return;
            }

            Vector3 currentPosition = transform.position;

            if (!hasRipplePosition)
            {
                TryEmitFromTransform(centerProbe != null ? centerProbe : transform);
                return;
            }

            Vector3 movement = currentPosition - lastRipplePosition;
            Vector3 planarMovement = Vector3.ProjectOnPlane(movement, Vector3.up);

            if (planarMovement.sqrMagnitude >= distancePerRipple * distancePerRipple)
            {
                TryEmitFromTransform(centerProbe != null ? centerProbe : transform);
            }
        }

        public void EmitFootstep()
        {
            TryEmitFromTransform(centerProbe != null ? centerProbe : transform);
        }

        public void EmitLeftFoot()
        {
            TryEmitFromTransform(leftFoot != null ? leftFoot : centerProbe != null ? centerProbe : transform);
        }

        public void EmitRightFoot()
        {
            TryEmitFromTransform(rightFoot != null ? rightFoot : centerProbe != null ? centerProbe : transform);
        }

        private void UpdateMovementSpeed()
        {
            float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            currentSpeed = Vector3.Distance(transform.position, lastFramePosition) / deltaTime;
            lastFramePosition = transform.position;
        }

        private bool TryEmitFromTransform(Transform probeTransform)
        {
            if (Time.time - lastRippleTime < minimumInterval)
            {
                return false;
            }

            Vector3 rayOrigin = probeTransform.position + Vector3.up * rayStartOffset;

            if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayDistance + rayStartOffset, surfaceLayerMask, triggerInteraction))
            {
                return false;
            }

            RippleSurfaceController surface = hit.collider.GetComponentInParent<RippleSurfaceController>();

            if (surface == null)
            {
                return false;
            }

            float strength = Mathf.Min(baseStrength + currentSpeed * speedStrengthMultiplier, maximumStrength);

            if (!surface.EmitRipple(hit.point, strength))
            {
                return false;
            }

            lastRippleTime = Time.time;
            lastRipplePosition = transform.position;
            hasRipplePosition = true;

            return true;
        }
    }
}
