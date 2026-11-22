using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace AirCombat
{
    // One physical AA round: swept segment collisions, gravity, true flight time.
    // The visible line depicts this round, not a decorative hitscan beam.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(190)] // After flight and cabrage transform movement.
    public sealed class AntiAircraftProjectile : MonoBehaviour
    {
        private static readonly RaycastHit[] HitBuffer = new RaycastHit[32];
        private static int lastPhysicsSyncFrame = -1;

        private Vector3 velocity;
        private Transform shooter;
        private LineRenderer core;
        private LineRenderer glow;
        private Material sparkMaterial;
        private Action onAircraftHit;
        private float gravity;
        private float remainingRange;
        private float travelled;
        private float tracerLength;
        private bool launched;

        public void Launch(Vector3 direction, float speed, float drop, float range, float streakLength,
            Transform owner, Material tracer, Material impactSpark, Action hitCallback)
        {
            velocity = direction.normalized * speed;
            shooter = owner;
            sparkMaterial = impactSpark;
            onAircraftHit = hitCallback;
            gravity = drop;
            remainingRange = range;
            tracerLength = streakLength;
            core = CreateLine("Hot core", tracer, .65f,
                new Color(1f, .92f, .55f, 1f), new Color(1f, .55f, .12f, .9f));
            glow = CreateLine("Soft halo", tracer, 1.4f,
                new Color(1f, .60f, .16f, .21f), new Color(1f, .32f, .10f, .12f));
            launched = true;
            Draw();
        }

        private LineRenderer CreateLine(string label, Material material, float width,
            Color tailColor, Color tipColor)
        {
            var visual = new GameObject(label);
            visual.transform.SetParent(transform, false);
            var line = visual.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.widthMultiplier = width;
            line.numCapVertices = 2;
            line.startColor = tailColor;
            line.endColor = tipColor;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private void LateUpdate()
        {
            if (!launched) return;
            // FlightController moves the hitbox by Transform, not Rigidbody.
            // Sync exactly once per frame before sweeping all AA rounds.
            if (lastPhysicsSyncFrame != Time.frameCount)
            {
                Physics.SyncTransforms();
                lastPhysicsSyncFrame = Time.frameCount;
            }
            float dt = Time.deltaTime;
            Vector3 start = transform.position;
            Vector3 end = start + velocity * dt + Vector3.down * (.5f * gravity * dt * dt);
            Vector3 delta = end - start;
            float distance = delta.magnitude;
            Vector3 newVelocity = velocity + Vector3.down * (gravity * dt);

            if (distance > 0.0001f)
            {
                int count = Physics.RaycastNonAlloc(start, delta / distance, HitBuffer,
                    distance, ~0, QueryTriggerInteraction.Ignore);
                float nearest = float.PositiveInfinity;
                RaycastHit closest = default;
                for (int i = 0; i < count; i++)
                {
                    var hit = HitBuffer[i];
                    if (hit.collider == null || hit.distance >= nearest ||
                        (shooter != null && hit.collider.transform.IsChildOf(shooter))) continue;
                    nearest = hit.distance;
                    closest = hit;
                }
                if (!float.IsPositiveInfinity(nearest))
                {
                    var victim = closest.collider.GetComponentInParent<DamageableTarget>();
                    if (victim != null && victim.Domain == CombatTargetDomain.Air &&
                        victim.ApplyDamage(closest.point, 1)) onAircraftHit?.Invoke();
                    Impact(closest.point);
                    return;
                }
            }

            velocity = newVelocity;
            transform.position = end;
            travelled += distance;
            remainingRange -= distance;
            Draw();
            if (remainingRange <= 0f || transform.position.y <= 0f)
            {
                if (transform.position.y <= 0f) Impact(transform.position);
                else Destroy(gameObject);
            }
        }

        private void Draw()
        {
            Vector3 head = transform.position;
            Vector3 tail = head - velocity.normalized * Mathf.Min(tracerLength, travelled);
            core.SetPosition(0, tail);
            core.SetPosition(1, head);
            glow.SetPosition(0, tail);
            glow.SetPosition(1, head);
        }

        private void Impact(Vector3 position)
        {
            if (sparkMaterial != null)
            {
                var spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                spark.name = "AA impact spark";
                spark.transform.position = position;
                spark.transform.localScale = Vector3.one * .7f;
                var collider = spark.GetComponent<Collider>();
                if (collider != null) { collider.enabled = false; Destroy(collider); }
                var renderer = spark.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = sparkMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                Destroy(spark, .18f);
            }
            Destroy(gameObject);
        }
    }
}
