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
        private TrailRenderer trail;
        private LineRenderer outerFlame, innerFlame;
        private float travelled;
        private Material flashMaterial;
        private Material scorchMaterial;
        private Material impactParticles;
        private Action<int> onImpact;
        private Action<GameObject> onEffect;
        private float elapsed;
        private float blastRadius = 6f;
        private bool launched;

        public void Launch(Vector3 initialVelocity, Transform owner,
            Material shell, Material trailMaterial, Material flash, Material scorch,
            Action<int> impactCallback, float radius = 6f, bool castShadow = true,
            Action<GameObject> effectCallback = null, Material impactParticleMaterial = null)
        {
            velocity = initialVelocity;
            shooter = owner;
            flashMaterial = flash;
            scorchMaterial = scorch;
            impactParticles = impactParticleMaterial;
            onImpact = impactCallback;
            onEffect = effectCallback;
            blastRadius = Mathf.Max(.1f, radius);

            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "NURS shell";
            visual.transform.SetParent(transform, false);
            visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            visual.transform.localScale = new Vector3(.65f, 1.5f, .65f);
            var collider = visual.GetComponent<Collider>();
            if (collider != null) { collider.enabled = false; Destroy(collider); }
            var renderer = visual.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = shell;
            renderer.shadowCastingMode = castShadow ? ShadowCastingMode.On : ShadowCastingMode.Off;

            // Attached, continuous flame geometry remains readable even when the
            // rocket travels several body lengths between rendered frames.
            outerFlame = AddFlame("NURS flame / orange", trailMaterial, -1.4f, -6.4f,
                1.15f, new Color(1f, .38f, .06f, .95f), new Color(1f, .22f, .03f, 0f), 1);
            innerFlame = AddFlame("NURS flame / hot core", trailMaterial, -1.45f, -4.7f,
                .55f, new Color(1f, 1f, .83f, 1f), new Color(1f, .68f, .15f, .1f), 2);

            var streak = new GameObject("NURS trail");
            streak.transform.SetParent(transform, false);
            streak.transform.localPosition = Vector3.back * 4.5f;
            trail = streak.AddComponent<TrailRenderer>();
            trail.sharedMaterial = trailMaterial;
            trail.time = .045f;
            trail.minVertexDistance = .25f;
            trail.startWidth = .65f;
            trail.endWidth = 1.1f;
            trail.startColor = new Color(.68f, .64f, .58f, .24f);
            trail.endColor = new Color(.58f, .55f, .50f, 0f);
            trail.sortingOrder = 0;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.Clear();
            trail.emitting = false;
            launched = true;
            OrientBody();
            UpdateExhaust();
        }

        private LineRenderer AddFlame(string label, Material material, float front, float back,
            float width, Color hot, Color tail, int sortingOrder)
        {
            var flame = new GameObject(label);
            flame.transform.SetParent(transform, false);
            var line = flame.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.SetPosition(0, Vector3.forward * front);
            line.SetPosition(1, Vector3.forward * back);
            line.startWidth = width;
            line.endWidth = .04f;
            line.startColor = hot;
            line.endColor = tail;
            line.numCapVertices = 2;
            line.sortingOrder = sortingOrder;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        // The full rearward flame would reach back up through the wing when a
        // steeply pitched rocket first appears. Grow it as the rocket clears the pylon.
        private void UpdateExhaust()
        {
            outerFlame.SetPosition(1, Vector3.back * Mathf.Min(6.4f, 1.7f + travelled));
            innerFlame.SetPosition(1, Vector3.back * Mathf.Min(4.7f, 1.7f + travelled));
            if (trail != null && !trail.emitting && travelled >= 4.5f)
            {
                trail.Clear();
                trail.emitting = true;
            }
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
                    if (RadarTerrainObstacle.TryProjectileHit(gameObject.scene, from, to,
                            out float rockAt, out Vector3 rockPoint) && rockAt * distance <= closest)
                    {
                        Detonate(rockPoint);
                        return;
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
                travelled += distance;
                remaining -= dt;
            }
            OrientBody();
            UpdateExhaust();
        }

        private void OrientBody()
        {
            if (velocity.sqrMagnitude > .01f)
                transform.rotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);
        }

        private void Detonate(Vector3 point)
        {
            int damagedParts = GroundBlastDamage.Apply(point, blastRadius, 1);
            var effect = impactParticles != null
                ? RocketImpactFx.Spawn(point, impactParticles, blastRadius)
                : BombImpactFx.Spawn(point, flashMaterial, scorchMaterial, blastRadius);
            onEffect?.Invoke(effect);
            onImpact?.Invoke(damagedParts);
            Finish();
        }

        public void Cancel()
        {
            launched = false;
            onImpact = null;
            onEffect = null;
            gameObject.SetActive(false); // Attached trail is destroyed with the rocket.
            Destroy(gameObject);
        }

        private void Finish()
        {
            launched = false;
            if (trail != null)
            {
                trail.emitting = false;
                trail.transform.SetParent(null, true);
                onEffect?.Invoke(trail.gameObject);
                Destroy(trail.gameObject, trail.time + .1f);
                trail = null;
            }
            Destroy(gameObject);
        }
    }
}
