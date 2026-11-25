using System;
using UnityEngine;

namespace AirCombat
{
    // Swept ballistic projectile for air targets; ground stays immune to air guns.
    [DisallowMultipleComponent]
    public sealed class BallisticProjectile : MonoBehaviour
    {
        private Vector3 direction;
        private float speed;
        private float remaining;
        private float trailLength;
        private LineRenderer tracer;
        private LineRenderer glow;
        private LineRenderer outerGlow;
        private LineRenderer head;
        private float headSize;
        private Vector3 headSide;
        private Action onHit;
        private Action<Vector3> onImpactFx;
        private int damage = 1;
        private Transform owner;
        private AircraftSide ownerSide;

        // Only modular aircraft guns supply an owner. Legacy emitters retain
        // their original target rules. Capture the side at emission time.
        public void BindOwner(Transform source)
        {
            owner = source;
            var side = source != null ? source.GetComponent<AircraftTeam>() : null;
            ownerSide = side != null ? side.Side : AircraftSide.Unassigned;
        }

        public bool IsOwnedBy(Transform source) => source != null && owner == source;

        private bool IsFriendly(DamageableTarget candidate)
        {
            if (owner != null && (candidate.transform == owner || candidate.transform.IsChildOf(owner)))
                return true;
            if (ownerSide == AircraftSide.Unassigned) return false;
            var team = candidate.GetComponentInParent<AircraftTeam>();
            return team != null && team.Side == ownerSide;
        }
        private float distanceTravelled;
        private static readonly RaycastHit[] HitBuffer = new RaycastHit[16];

        public void Configure(Vector3 forward, float velocity, float range, float length,
            LineRenderer line, LineRenderer halo, Action hitCallback, int hitDamage = 1,
            LineRenderer outer = null, LineRenderer leadingHead = null,
            float leadingHeadSize = 0f, Action<Vector3> impactFx = null)
        {
            direction = forward.normalized;
            speed = velocity;
            remaining = range;
            trailLength = length;
            tracer = line;
            glow = halo;
            outerGlow = outer;
            head = leadingHead;
            headSize = leadingHeadSize;
            headSide = Vector3.Cross(direction, Vector3.up).normalized;
            if (headSide.sqrMagnitude < .01f) headSide = Vector3.right;
            onHit = hitCallback;
            onImpactFx = impactFx;
            damage = Mathf.Max(1, hitDamage);
            Draw();
        }

        private void Update()
        {
            if (remaining <= 0f) { Destroy(gameObject); return; }
            float travel = Mathf.Min(remaining, speed * Time.deltaTime);
            Vector3 start = transform.position;
            // Preserve original swept collision and nearest eligible target.
            int count = Physics.RaycastNonAlloc(start, direction, HitBuffer, travel, ~0, QueryTriggerInteraction.Ignore);
            DamageableTarget nearest = null;
            Vector3 hitPoint = default;
            float best = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var candidate = HitBuffer[i].collider.GetComponentInParent<DamageableTarget>();
                if (candidate == null || candidate.Domain != CombatTargetDomain.Air ||
                    HitBuffer[i].distance >= best || IsFriendly(candidate)) continue;
                nearest = candidate;
                hitPoint = HitBuffer[i].point;
                best = HitBuffer[i].distance;
            }
            if (RadarTerrainObstacle.TryProjectileHit(gameObject.scene, start, start + direction * travel,
                    out float rockAt, out Vector3 rockPoint) && rockAt * travel <= best)
            {
                transform.position = rockPoint;
                if (onImpactFx != null) onImpactFx(rockPoint);
                else if (tracer != null) ArcadeCannonImpactFx.Spawn(rockPoint, tracer.sharedMaterial, .65f);
                Destroy(gameObject);
                return;
            }
            if (nearest != null)
            {
                transform.position = hitPoint;
                if (nearest.ApplyDamage(hitPoint, damage))
                {
                    onHit?.Invoke();
                    onImpactFx?.Invoke(hitPoint);
                }
                Destroy(gameObject);
                return;
            }
            transform.position = start + direction * travel;
            distanceTravelled += travel;
            remaining -= travel;
            Draw();
        }

        private void Draw()
        {
            if (tracer == null) return;
            Vector3 front = transform.position;
            Vector3 tail = front - direction * Mathf.Min(trailLength, distanceTravelled);
            tracer.SetPosition(0, tail);
            tracer.SetPosition(1, front);
            if (glow != null)
            {
                glow.SetPosition(0, tail);
                glow.SetPosition(1, front);
            }
            if (outerGlow != null)
            {
                outerGlow.SetPosition(0, tail);
                outerGlow.SetPosition(1, front);
            }
            if (head != null)
            {
                head.SetPosition(0, front - headSide * (headSize * .5f));
                head.SetPosition(1, front + headSide * (headSize * .5f));
            }
        }
    }
}
