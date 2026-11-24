using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace AirCombat
{
    // Arcade three-phase projectile. Only the committed horizontal flight is
    // damaging. Once the heading is captured it never reads target position again.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(190)]
    public sealed class MantisVerticalRocket : MonoBehaviour
    {
        private enum Phase { Rise, Align, Flight, Puff }
        private static readonly RaycastHit[] Hits = new RaycastHit[64];
        private static int syncedFrame = -1;
        private Phase phase;
        private Transform owner;
        private PlayerHitPoints target;
        private Transform body;
        private LineRenderer exhaust, glow;
        private TrailRenderer trail;
        private AudioSource audioSource;
        private AudioClip ignition, burst;
        private float height, riseSpeed, alignSeconds, speed, remaining, age, volume;
        private int damage;
        private Vector3 heading;
        private Action onHit;
        private bool launched;

        public void Launch(Transform shooter, PlayerHitPoints player, float climbSpeed, float alignmentTime,
            float cruiseSpeed, float range, int hitDamage, Material hull, Material effects,
            AudioClip launchSound, AudioClip blastSound, float soundVolume, Action hitCallback)
        {
            owner = shooter; target = player; height = player.transform.position.y;
            riseSpeed = Mathf.Max(1f, climbSpeed); alignSeconds = Mathf.Max(.05f, alignmentTime);
            speed = Mathf.Max(1f, cruiseSpeed); remaining = Mathf.Max(1f, range);
            damage = hitDamage; onHit = hitCallback; volume = soundVolume;
            ignition = launchSound; burst = blastSound;
            heading = Vector3.up;
            phase = Phase.Rise;
            var model = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            model.name = "Rocket body";
            model.transform.SetParent(transform, false);
            model.transform.localScale = new Vector3(1.1f, 2.6f, 1.1f);
            var collider = model.GetComponent<Collider>();
            collider.enabled = false; Destroy(collider);
            var renderer = model.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = hull;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            body = model.transform;
            exhaust = MakeLine("Motor core", effects, 1.1f);
            glow = MakeLine("Motor glow", effects, 2.3f);
            var trailObject = new GameObject("Rocket smoke");
            trailObject.transform.SetParent(transform, false);
            trail = trailObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = effects;
            trail.time = .42f;
            trail.minVertexDistance = 1f;
            trail.startWidth = 1.8f; trail.endWidth = .25f;
            trail.startColor = new Color(.69f,.71f,.68f,.64f);
            trail.endColor = new Color(.55f,.57f,.54f,0f);
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.Clear(); trail.emitting = true;
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = .25f;
            audioSource.minDistance = 30f; audioSource.maxDistance = 350f;
            if (ignition != null) audioSource.PlayOneShot(ignition, volume * .65f);
            launched = true;
            DrawMotor();
        }

        private LineRenderer MakeLine(string label, Material material, float width)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true; line.positionCount = 2;
            line.widthMultiplier = width; line.numCapVertices = 4;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private void LateUpdate()
        {
            if (!launched) return;
            float dt = Time.deltaTime;
            if (phase == Phase.Puff)
            {
                age += dt;
                if (age >= .55f) { Destroy(gameObject); return; }
                DrawPuff(); return;
            }
            if (phase == Phase.Rise)
            {
                Vector3 p = transform.position;
                p.y = Mathf.MoveTowards(p.y, height, riseSpeed * dt);
                transform.position = p;
                if (Mathf.Abs(p.y - height) < .001f) { phase = Phase.Align; age = 0f; }
                DrawMotor(); return;
            }
            if (phase == Phase.Align)
            {
                if (target == null || !target.IsAlive) { BeginPuff(transform.position); return; }
                age += dt;
                Vector3 delta = target.transform.position - transform.position;
                delta.y = 0f;
                // Aim at the CURRENT position, never at a predicted intercept.
                Vector3 aim = delta.sqrMagnitude > .0001f ? delta.normalized : Vector3.forward;
                heading = Vector3.Slerp(Vector3.up, aim, Mathf.Clamp01(age / alignSeconds)).normalized;
                if (age >= alignSeconds)
                {
                    heading = aim; // one final snapshot; immutable thereafter
                    phase = Phase.Flight; age = 0f;
                    if (ignition != null) audioSource.PlayOneShot(ignition, volume);
                }
                DrawMotor(); return;
            }
            if (syncedFrame != Time.frameCount) { Physics.SyncTransforms(); syncedFrame = Time.frameCount; }
            float step = Mathf.Min(remaining, speed * dt);
            Vector3 start = transform.position;
            if (step > .0001f)
            {
                int count = Physics.RaycastNonAlloc(start, heading, Hits, step, ~0, QueryTriggerInteraction.Ignore);
                float nearest = float.PositiveInfinity;
                RaycastHit closest = default;
                for (int i = 0; i < count; i++)
                {
                    var hit = Hits[i];
                    if (hit.collider == null || hit.distance >= nearest ||
                        (owner != null && hit.collider.transform.IsChildOf(owner))) continue;
                    nearest = hit.distance; closest = hit;
                }
                if (!float.IsPositiveInfinity(nearest))
                {
                    if (target != null && closest.collider.GetComponentInParent<PlayerHitPoints>() == target &&
                        target.ApplyDamage(closest.point, damage)) onHit?.Invoke();
                    BeginPuff(closest.point); return;
                }
            }
            transform.position = start + heading * step;
            remaining -= step;
            age += dt;
            if (remaining <= .001f) { BeginPuff(transform.position); return; }
            DrawMotor();
        }

        private void DrawMotor()
        {
            body.rotation = Quaternion.FromToRotation(Vector3.up, heading);
            Vector3 nozzle = transform.position - heading * 2.4f;
            float length = phase == Phase.Flight ? Mathf.Lerp(13f, 6f, Mathf.Clamp01(age / .18f)) :
                phase == Phase.Align ? 1.8f : 4f;
            SetLine(exhaust, nozzle - heading * length, nozzle, 1.05f, new Color(1f,.91f,.55f,1f));
            SetLine(glow, nozzle - heading * length, nozzle, 2.4f, new Color(1f,.43f,.10f,.45f));
        }

        private void BeginPuff(Vector3 p)
        {
            transform.position = p; phase = Phase.Puff; age = 0f;
            body.gameObject.SetActive(false); trail.emitting = false;
            if (burst != null) audioSource.PlayOneShot(burst, volume * .65f);
            DrawPuff();
        }

        private void DrawPuff()
        {
            float t = Mathf.Clamp01(age / .55f);
            Vector3 p = transform.position;
            Vector3 side = Vector3.Cross(Vector3.up, heading).normalized;
            if (side.sqrMagnitude < .01f) side = Vector3.right;
            float r = Mathf.Lerp(1.5f, 5f, t);
            SetLine(glow, p - side * r * .25f, p + side * r * .25f, r * 2f,
                new Color(.46f,.43f,.39f,(1f-t)*.65f));
            SetLine(exhaust, p - side * .15f, p + side * .15f, Mathf.Lerp(7f,1f,t),
                new Color(1f,.82f,.36f,Mathf.Clamp01(1f-age/.13f)));
        }

        private static void SetLine(LineRenderer line, Vector3 a, Vector3 b, float width, Color color)
        {
            line.SetPosition(0,a); line.SetPosition(1,b); line.widthMultiplier = width;
            Color tail = color; tail.a *= .45f;
            line.startColor = tail; line.endColor = color;
        }
    }
}
