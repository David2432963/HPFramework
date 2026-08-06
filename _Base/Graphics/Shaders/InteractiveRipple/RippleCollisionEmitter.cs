using UnityEngine;

namespace Base.VisualEffects
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class RippleCollisionEmitter : MonoBehaviour
    {
        [SerializeField] private RippleSurfaceController surface;
        [SerializeField, Min(0f)] private float minimumImpactSpeed = 0.5f;
        [SerializeField, Min(0f)] private float strengthPerSpeed = 0.15f;
        [SerializeField, Min(0f)] private float maximumStrength = 2f;
        [SerializeField, Min(0f)] private float cooldown = 0.05f;

        private float lastEmitTime = float.NegativeInfinity;

        private void Reset()
        {
            surface = GetComponent<RippleSurfaceController>();
        }

        private void Awake()
        {
            if (surface == null)
            {
                surface = GetComponent<RippleSurfaceController>();
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (surface == null) return;
            if (Time.time - lastEmitTime < cooldown) return;

            float impactSpeed = collision.relativeVelocity.magnitude;
            if (impactSpeed < minimumImpactSpeed) return;
            if (collision.contactCount <= 0) return;

            ContactPoint contact = collision.GetContact(0);
            float strength = Mathf.Min(impactSpeed * strengthPerSpeed, maximumStrength);

            if (surface.EmitRipple(contact.point, strength))
            {
                lastEmitTime = Time.time;
            }
        }
    }
}
