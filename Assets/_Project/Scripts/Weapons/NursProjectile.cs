using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace AirCombat
{
    // Real, unguided rocket with swept collision against roofs/terrain.
    // Its small blast damages nearby G3 depot parts; air targets are not NURS targets.
    [DisallowMultipleComponent]
    public sealed class NursProjectile : MonoBehaviour
    {
        private static readonly RaycastHit[] Hits = new RaycastHit[32];
        private Vector3 velocity;
        private Transform shooter;
        private Transform body;
        private TrailRenderer trail;
        private Material flashMaterial;
        private Material scorchMaterial;
        private Action<int> onImpact;
        private float elapsed;
        private bool launched;

        public void Launch(Vector3 initialVelocity, Transform owner,
            Material shell, Material trailMaterial, Material flash, Material scorch,
            Action<int> impactCallback)
        {
            velocity = initialVelocity;
            shooter = owner;
            flashMaterial = flash;
            scorchMaterial = scorch;
            onImpact = impactCallback;

            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "NURS shell";
            visual.transform.SetParent(transform, false);
            visual.transform.localScale = new Vector3(.36f, .85f, .36f);
            body = visual.transform;
            var collider = visual.GetComponent<Collider>();
            if (collider != null) { collider.enabled = false; Destroy(collider); }
            var renderer = visual.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = shell;
            renderer.shadowCastingMode = ShadowCastingMode.On;

            var streak = new GameObject("NURS trail");
            streak.transform.SetParent(transform, false);
            trail = streak.AddComponent<TrailRenderer>();
            trail.sharedMaterial = trailMaterial;
            trail.time = .5f;
            trail.minVertexDistance = .35f;
            trail.startWidth = .8f;
            trail.endWidth = .04f;
            trail.startColor = new Color(1f, .82f, .47f, .8f);
            trail.endColor = new Color(.7f, .74f, .7f, 0f);
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.Clear();
            trail.emitting = true;
            launched = true;
            OrientBody();
        }

        private void Update()
        {
            if (!launched) return;
            elapsed += Time.deltaTime;
            if (elapsed > 7f) { Finish(); return; }

            float remaining = Time.deltaTime;
            while (remaining > 0f)
            {
                float dt = Mathf.Min(NursBallistics.IntegrationStep, remaining);
                Vector3 from = transform.position;
                Vector3 to = NursBallistics.Integrate(ref velocity, from, dt);
                Vector3 delta = to - from;
                float distance = delta.magnitude;
                if (distance > .0001f)
                {
                    int count = Physics.RaycastNonAlloc(from, delta / distance, Hits,
                        distance, ~0, QueryTriggerInteraction.Ignore);
                    float closest = float.PositiveInfinity;
                    Vector3 impact = default;
                    for (int i = 0; i < count; i++)
                    {
                        RaycastHit hit = Hits[i];
                        if (hit.collider == null || hit.distance >= closest ||
                            (shooter != null && hit.collider.transform.IsChildOf(shooter))) continue;
                        var target = hit.collider.GetComponentInParent<DamageableTarget>();
                        if (target != null && target.Domain == CombatTargetDomain.Air) continue;
                        closest = hit.distance;
                        impact = hit.point;
                    }
                    if (!float.IsPositiveInfinity(closest))
                    {
                        Detonate(impact);
                        return;
                    }
                }
                if (to.y <= 0f)
                {
                    float fraction = Mathf.Clamp01(from.y / Mathf.Max(.0001f, from.y - to.y));
                    Detonate(Vector3.Lerp(from, to, fraction));
                    return;
                }
                transform.position = to;
                remaining -= dt;
            }
            OrientBody();
        }

        private void OrientBody()
        {
            if (body != null && velocity.sqrMagnitude > .01f)
                body.rotation = Quaternion.FromToRotation(Vector3.up, velocity.normalized);
        }

        private void Detonate(Vector3 point)
        {
            const float blastRadius = 6f;
            int damagedParts = 0;
            var hits = Physics.OverlapSphere(point, blastRadius, ~0, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                var part = hit.GetComponent<GroundDepotPart>();
                if (part != null && part.ApplyDamage(point, 1)) damagedParts++;
            }
            BombImpactFx.Spawn(point, flashMaterial, scorchMaterial, blastRadius);
            onImpact?.Invoke(damagedParts);
            Finish();
        }

        private void Finish()
        {
            launched = false;
            if (trail != null)
            {
                trail.emitting = false;
                trail.transform.SetParent(null, true);
                Destroy(trail.gameObject, trail.time + .1f);
                trail = null;
            }
            Destroy(gameObject);
        }
    }
}
