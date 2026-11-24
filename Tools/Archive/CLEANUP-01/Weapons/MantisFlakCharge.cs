using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace AirCombat
{
    // Arcade air burst. The flight is a telegraph, the committed world-space
    // charge is stationary, and only the single detonation can deal damage.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(190)]
    public sealed class MantisFlakCharge : MonoBehaviour
    {
        private const int RingSegments = 64;
        private const float BurstSeconds = 1.05f;
        private PlayerHitPoints target;
        private Vector3 launchPoint, center;
        private float travel, fuse, radius, startedAt, explodedAt, volume;
        private int damage;
        private Action onHit;
        private AudioClip blastClip;
        private AudioSource audioSource;
        private LineRenderer core, halo, ring, trail, shockwave;
        private readonly LineRenderer[] sparks = new LineRenderer[12];
        private readonly LineRenderer[] smoke = new LineRenderer[7];
        private readonly Vector3[] sparkDirs = new Vector3[12];
        private readonly Vector3[] smokeOffsets = new Vector3[7];
        private bool launched, exploded;

        public void Launch(PlayerHitPoints player, Vector3 point, float travelSeconds,
            float fuseSeconds, float blastRadius, int hitDamage, Material material,
            AudioClip shot, AudioClip blast, float soundVolume, Action hitCallback)
        {
            target = player; launchPoint = transform.position; center = point;
            travel = Mathf.Max(.05f, travelSeconds); fuse = Mathf.Max(.1f, fuseSeconds);
            radius = Mathf.Max(1f, blastRadius); damage = hitDamage; onHit = hitCallback;
            volume = soundVolume; blastClip = blast;
            core = MakeLine("Hot shell", material, 2);
            halo = MakeLine("Shell glow", material, 2);
            trail = MakeLine("Rise streak", material, 2);
            ring = MakeLine("World-space blast envelope", material, RingSegments + 1);
            ring.enabled = false;
            shockwave = MakeLine("Expanding shockwave", material, RingSegments + 1);
            shockwave.enabled = false;
            for (int i = 0; i < sparks.Length; i++)
            {
                sparks[i] = MakeLine("Blast spark", material, 2);
                sparks[i].enabled = false;
                float angle = i * Mathf.PI * 2f / sparks.Length;
                sparkDirs[i] = new Vector3(Mathf.Cos(angle), .15f * Mathf.Sin(i * 2.1f), Mathf.Sin(angle)).normalized;
            }
            for (int i = 0; i < smoke.Length; i++)
            {
                smoke[i] = MakeLine("Blast smoke", material, 2);
                smoke[i].enabled = false;
                float a = i * Mathf.PI * 2f / smoke.Length;
                smokeOffsets[i] = new Vector3(Mathf.Cos(a), .12f * Mathf.Cos(i * 1.7f), Mathf.Sin(a));
            }
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = .25f;
            audioSource.minDistance = 30f;
            audioSource.maxDistance = 350f;
            if (shot != null) audioSource.PlayOneShot(shot, volume * .65f);
            startedAt = Time.time;
            launched = true;
            DrawRise(0f);
        }

        private LineRenderer MakeLine(string label, Material material, int count)
        {
            var obj = new GameObject(label);
            obj.transform.SetParent(transform, false);
            var line = obj.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.positionCount = count;
            line.numCapVertices = 6;
            line.numCornerVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private void LateUpdate()
        {
            if (!launched) return;
            if (exploded)
            {
                float age = Time.time - explodedAt;
                if (age >= BurstSeconds) { Destroy(gameObject); return; }
                DrawBlast(age);
                return;
            }
            float elapsed = Time.time - startedAt;
            if (elapsed < travel) { DrawRise(elapsed / travel); return; }
            transform.position = center;
            trail.enabled = false;
            float t = Mathf.Clamp01((elapsed - travel) / fuse);
            if (t >= 1f) { Detonate(); return; }
            DrawFuse(t);
        }

        private void DrawRise(float t)
        {
            Vector3 p = Vector3.Lerp(launchPoint, center, t);
            transform.position = p;
            Vector3 tail = Vector3.Lerp(launchPoint, center, Mathf.Max(0f, t - .13f));
            Stroke(trail, tail, p, 1.2f, new Color(1f, .58f, .16f, .8f));
            Dot(core, p, 1.7f, new Color(1f, .92f, .53f, 1f));
            Dot(halo, p, 3.3f, new Color(1f, .45f, .1f, .25f));
        }

        private void DrawFuse(float t)
        {
            // A glowing charge plus a fixed-size physical aura on the combat
            // plane. Its outline is exactly the horizontal blast radius.
            float pulse = .5f + .5f * Mathf.Sin(t * Mathf.PI * 5f);
            Dot(core, center, Mathf.Lerp(2.5f, 4.2f, t), new Color(1f, .94f, .57f, 1f));
            Dot(halo, center, Mathf.Lerp(5f, 9f, t), new Color(1f, .45f, .13f, .22f + .14f * pulse));
            DrawRing(radius, Mathf.Lerp(.45f, .85f, t), new Color(1f, .68f, .24f, .62f + .25f * t));
        }

        private void Detonate()
        {
            exploded = true; explodedAt = Time.time;
            // Exact sphere centered on the committed point; evaluate player
            // center, not a projected screen overlap or a lingering smoke hit.
            if (target != null && target.IsAlive &&
                (target.transform.position - center).sqrMagnitude <= radius * radius &&
                target.ApplyDamage(target.transform.position, damage)) onHit?.Invoke();
            if (blastClip != null) audioSource.PlayOneShot(blastClip, volume);
            DrawBlast(0f);
        }

        private void DrawBlast(float age)
        {
            float flash = Mathf.Clamp01(1f - age / .14f);
            // The detonation frame shows the entire lethal radius immediately.
            // The fading ring is the actual damage boundary, not a second hit.
            DrawRing(radius, Mathf.Lerp(.35f, 1.35f, flash), new Color(1f, .83f, .45f, Mathf.Clamp01(1f - age / .29f)));
            ring.enabled = age < .29f;
            float wave = Mathf.Clamp01(age / .16f);
            PaintRing(shockwave, Mathf.Lerp(radius * .22f, radius, wave),
                Mathf.Lerp(1.8f, .35f, wave), new Color(1f, .94f, .68f, 1f - wave));
            shockwave.enabled = age < .16f;
            core.enabled = age < .14f; halo.enabled = age < .24f;
            Dot(core, center, radius * (1.15f + .35f * (1f - flash)), new Color(1f, .96f, .77f, flash));
            Dot(halo, center, radius * 1.9f, new Color(1f, .57f, .17f, .52f * Mathf.Clamp01(1f - age / .24f)));
            for (int i = 0; i < sparks.Length; i++)
            {
                float progress = Mathf.Clamp01(age / .42f);
                sparks[i].enabled = age < .42f;
                Vector3 head = center + sparkDirs[i] * radius * Mathf.Lerp(.12f, .96f, progress);
                Vector3 tail = head - sparkDirs[i] * radius * (.15f * (1f - progress));
                Stroke(sparks[i], tail, head, Mathf.Lerp(.85f, .18f, progress), new Color(1f, .77f, .29f, 1f - progress));
            }
            float smokeAge = Mathf.Clamp01(age / BurstSeconds);
            for (int i = 0; i < smoke.Length; i++)
            {
                smoke[i].enabled = true;
                Vector3 p = center + smokeOffsets[i] * radius * Mathf.Lerp(.18f, .56f, smokeAge) + Vector3.up * smokeAge * 2f;
                float alpha = Mathf.Clamp01(age / .10f) * (1f - smokeAge) * .48f;
                Dot(smoke[i], p, radius * Mathf.Lerp(.48f, .73f, smokeAge), new Color(.22f, .20f, .18f, alpha));
            }
        }

        private void DrawRing(float r, float width, Color color)
        {
            ring.enabled = true;
            PaintRing(ring, r, width, color);
        }

        private void PaintRing(LineRenderer line, float r, float width, Color color)
        {
            line.widthMultiplier = width;
            line.startColor = line.endColor = color;
            for (int i = 0; i <= RingSegments; i++)
            {
                float a = i * Mathf.PI * 2f / RingSegments;
                line.SetPosition(i, center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
            }
        }

        private static void Dot(LineRenderer line, Vector3 p, float width, Color color) =>
            Stroke(line, p - Vector3.right * .05f, p + Vector3.right * .05f, width, color);

        private static void Stroke(LineRenderer line, Vector3 a, Vector3 b, float width, Color color)
        {
            line.widthMultiplier = width;
            line.startColor = line.endColor = color;
            line.SetPosition(0, a); line.SetPosition(1, b);
        }

        // Short procedural placeholder sounds; no external assets or persistent sources.
        public static AudioClip MakeSound(bool shot)
        {
            const int rate = 22050;
            float duration = shot ? .13f : .48f;
            var data = new float[Mathf.CeilToInt(rate * duration)];
            var random = new System.Random(shot ? 911 : 157);
            float filtered = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)rate;
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                filtered += (noise - filtered) * (shot ? .28f : .09f);
                float envelope = Mathf.Min(1f, t / .004f) * Mathf.Exp(-t * (shot ? 35f : 12f));
                float low = Mathf.Sin(2f * Mathf.PI * (shot ? 100f : 57f) * t) * Mathf.Exp(-t * 16f);
                data[i] = Mathf.Clamp((filtered * 1.15f + noise * .18f + low * .42f) * envelope, -1f, 1f);
            }
            var clip = AudioClip.Create(shot ? "Flak / launch" : "Flak / detonation", data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
