using UnityEngine;
using UnityEngine.Rendering;

namespace AirCombat
{
    // A physical, unpowered gravity bomb. No lock-on, target snapping or homing.
    [DisallowMultipleComponent]
    public sealed class BombProjectile : MonoBehaviour
    {
        private Vector3 velocity;
        private Transform body;
        private Material flashMaterial;
        private Material scorchMaterial;
        private float lifetime;

        public void Launch(Vector3 initialVelocity, Material shell, Material flash,
            Material scorch)
        {
            velocity = initialVelocity;
            flashMaterial = flash;
            scorchMaterial = scorch;
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Bomb shell";
            visual.transform.SetParent(transform, false);
            visual.transform.localScale = new Vector3(.60f, 1.12f, .60f);
            body = visual.transform;
            var collider = visual.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false; // immediately absent from raycasts this frame
                Destroy(collider);
            }
            var renderer = visual.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = shell;
            // The same directional light renders the bomb and aircraft shadows.
            renderer.shadowCastingMode = ShadowCastingMode.On;
        }

        private void Update()
        {
            lifetime += Time.deltaTime;
            if (lifetime > 8f) { Destroy(gameObject); return; }
            float remaining = Time.deltaTime;
            while (remaining > 0f)
            {
                float step = Mathf.Min(BombTrajectory.IntegrationStep, remaining);
                Vector3 from = transform.position;
                Vector3 to = BombTrajectory.Integrate(ref velocity, from, step);
                Vector3 delta = to - from;
                float distance = delta.magnitude;
                if (distance > 0.0001f && Physics.Raycast(from, delta / distance,
                    out RaycastHit hit, distance, ~0, QueryTriggerInteraction.Ignore))
                {
                    Detonate(hit.point);
                    return;
                }
                if (to.y <= 0f)
                {
                    float fraction = Mathf.Clamp01(from.y / Mathf.Max(.0001f, from.y - to.y));
                    Detonate(Vector3.Lerp(from, to, fraction));
                    return;
                }
                transform.position = to;
                remaining -= step;
            }
            if (body != null && velocity.sqrMagnitude > .01f)
                body.rotation = Quaternion.FromToRotation(Vector3.up, velocity.normalized);
        }

        private void Detonate(Vector3 point)
        {
            const float radius = 13f;
            // Each structural zone has one collider; a blast can hit several zones.
            Collider[] hits = Physics.OverlapSphere(point, radius, ~0, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                var part = hit.GetComponent<GroundDepotPart>();
                if (part != null) part.ApplyDamage(point, 1);
            }
            BombImpactFx.Spawn(point, flashMaterial, scorchMaterial, radius);
            Destroy(gameObject);
        }
    }
}
