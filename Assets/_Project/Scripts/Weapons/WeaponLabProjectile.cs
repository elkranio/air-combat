using UnityEngine;

namespace AirCombat
{
    // Moving projectile, not instant hitscan. Dogfight rounds hit the 5-HP enemy aircraft.
    [DisallowMultipleComponent]
    public sealed class WeaponLabProjectile : MonoBehaviour
    {
        private Vector3 direction;
        private float speed;
        private float remaining;
        private float trailLength;
        private LineRenderer tracer;
        private LineRenderer glow;
        private WeaponLabCannon owner;
        private int damage = 1;
        private float distanceTravelled;
        private static readonly RaycastHit[] HitBuffer = new RaycastHit[16];

        public void Configure(Vector3 forward, float velocity, float range, float length,
            LineRenderer line, LineRenderer halo, WeaponLabCannon source, int hitDamage = 1)
        {
            direction = forward.normalized;
            speed = velocity;
            remaining = range;
            trailLength = length;
            tracer = line;
            glow = halo;
            owner = source;
            damage = Mathf.Max(1, hitDamage);
            Draw();
        }

        private void Update()
        {
            if (remaining <= 0f) { Destroy(gameObject); return; }
            float travel = Mathf.Min(remaining, speed * Time.deltaTime);
            Vector3 start = transform.position;
            // Only the unarmed lab enemy takes damage; ignore player, ground and scenery.
            int count = Physics.RaycastNonAlloc(start, direction, HitBuffer, travel, ~0, QueryTriggerInteraction.Ignore);
            WeaponLabEnemy nearest = null;
            Vector3 hitPoint = default;
            float best = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var candidate = HitBuffer[i].collider.GetComponentInParent<WeaponLabEnemy>();
                if (candidate == null || HitBuffer[i].distance >= best) continue;
                nearest = candidate;
                hitPoint = HitBuffer[i].point;
                best = HitBuffer[i].distance;
            }
            if (nearest != null)
            {
                transform.position = hitPoint;
                if (nearest.Hit(hitPoint, damage) && owner != null) owner.RegisterHit();
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
