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
        private Action onHit;
        private int damage = 1;
        private float distanceTravelled;
        private static readonly RaycastHit[] HitBuffer = new RaycastHit[16];

        public void Configure(Vector3 forward, float velocity, float range, float length,
            LineRenderer line, LineRenderer halo, Action hitCallback, int hitDamage = 1)
        {
            direction = forward.normalized;
            speed = velocity;
            remaining = range;
            trailLength = length;
            tracer = line;
            glow = halo;
            onHit = hitCallback;
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
                    HitBuffer[i].distance >= best) continue;
                nearest = candidate;
                hitPoint = HitBuffer[i].point;
                best = HitBuffer[i].distance;
            }
            if (nearest != null)
            {
                transform.position = hitPoint;
                if (nearest.ApplyDamage(hitPoint, damage)) onHit?.Invoke();
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
        }
    }
}
