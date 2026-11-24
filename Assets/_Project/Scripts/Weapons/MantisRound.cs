using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace AirCombat
{
    // Fixed 3D projectile, not a homing beam or a planar hit test. The altitude
    // cue changes appearance only. Swept collision precedes harmless air expiry.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(190)]
    public sealed class MantisRound : MonoBehaviour
    {
        private static readonly RaycastHit[] Hits = new RaycastHit[64];
        private static int syncedFrame = -1;
        private Transform owner;
        private PlayerHitPoints target;
        private Vector3 direction;
        private float speed, remaining, length, traveled, dangerHeight, ceilingOffset, planeHeight;
        private int damage;
        private Action onHit;
        private LineRenderer core, halo;
        private bool launched, puff;
        private float puffAge;
        private const float PuffSeconds = .22f;

        public void Launch(Transform shooter, PlayerHitPoints player, Vector3 heading, float velocity,
            float range, float tracerLength, float brightBand, float abovePlane, int hitDamage,
            Material material, Action hitCallback)
        {
            owner = shooter; target = player; direction = heading.normalized; speed = velocity;
            remaining = range; length = tracerLength; dangerHeight = brightBand; ceilingOffset = abovePlane;
            planeHeight = player.transform.position.y; damage = hitDamage; onHit = hitCallback;
            halo = MakeLine("Halo", material, 1.65f);
            core = MakeLine("Core", material, .8f);
            launched = true;
            DrawTracer();
        }

        private LineRenderer MakeLine(string label, Material material, float width)
        {
            var obj = new GameObject(label);
            obj.transform.SetParent(transform, false);
            var line = obj.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.widthMultiplier = width;
            line.numCapVertices = 4;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private void LateUpdate()
        {
            if (puff) { TickPuff(); return; }
            if (!launched) return;
            if (syncedFrame != Time.frameCount) { Physics.SyncTransforms(); syncedFrame = Time.frameCount; }
            if (target != null) planeHeight = target.transform.position.y;
            Vector3 start = transform.position;
            float step = Mathf.Min(remaining, speed * Time.deltaTime);
            float ceiling = planeHeight + ceilingOffset;
            bool airExpiry = direction.y > .0001f && start.y + direction.y * step >= ceiling;
            if (airExpiry) step = Mathf.Clamp((ceiling - start.y) / direction.y, 0f, step);
            if (step > .0001f)
            {
                int count = Physics.RaycastNonAlloc(start, direction, Hits, step, ~0, QueryTriggerInteraction.Ignore);
                float nearest = float.PositiveInfinity;
                RaycastHit closest = default;
                for (int i = 0; i < count; i++)
                {
                    var hit = Hits[i];
                    if (hit.collider == null || hit.distance >= nearest ||
                        (owner != null && hit.collider.transform.IsChildOf(owner))) continue;
                    nearest = hit.distance;
                    closest = hit;
                }
                if (!float.IsPositiveInfinity(nearest))
                {
                    if (target != null && closest.collider.GetComponentInParent<PlayerHitPoints>() == target &&
                        target.ApplyDamage(closest.point, damage)) onHit?.Invoke();
                    BeginPuff(closest.point);
                    return;
                }
            }
            transform.position = start + direction * step;
            traveled += step;
            remaining -= step;
            if (airExpiry || remaining <= .001f || transform.position.y < -1f)
            { BeginPuff(transform.position); return; }
            DrawTracer();
        }

        private void DrawTracer()
        {
            Vector3 head = transform.position;
            Vector3 tail = head - direction * Mathf.Min(length, traveled);
            float bright = 1f - Mathf.Clamp01(Mathf.Abs(planeHeight - head.y) / dangerHeight);
            Color hot = Color.Lerp(new Color(1f, .38f, .08f, .72f), new Color(1f, 1f, .72f, 1f), bright);
            Color glow = new Color(1f, .48f, .12f, Mathf.Lerp(.16f, .42f, bright));
            SetLine(core, tail, head, hot, .8f);
            SetLine(halo, tail, head, glow, 1.65f);
        }

        private static void SetLine(LineRenderer line, Vector3 a, Vector3 b, Color color, float width)
        {
            line.widthMultiplier = width;
            line.SetPosition(0, a); line.SetPosition(1, b);
            Color tail = color; tail.a *= .55f;
            line.startColor = tail; line.endColor = color;
        }

        private void BeginPuff(Vector3 point)
        {
            transform.position = point;
            launched = false; puff = true; puffAge = 0f;
            DrawPuff(0f);
        }

        private void TickPuff()
        {
            puffAge += Time.deltaTime;
            if (puffAge >= PuffSeconds) { Destroy(gameObject); return; }
            DrawPuff(puffAge / PuffSeconds);
        }

        private void DrawPuff(float t)
        {
            Vector3 p = transform.position;
            Vector3 axis = Vector3.Cross(direction, Vector3.up).normalized;
            if (axis.sqrMagnitude < .01f) axis = Vector3.right;
            float radius = Mathf.Lerp(.8f, 2.5f, t);
            float alpha = 1f - t;
            SetLine(halo, p - axis * radius * .28f, p + axis * radius * .28f,
                new Color(.56f, .51f, .43f, alpha * .55f), radius * 2f);
            SetLine(core, p - axis * radius * .15f, p + axis * radius * .15f,
                new Color(1f, .86f, .54f, alpha * alpha), Mathf.Lerp(1.7f, .5f, t));
        }
    }
}
