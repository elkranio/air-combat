using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace AirCombat
{
    // GAUSS owns its physical slug, fixed sight, muzzle flash and sound.
    // The single-click trigger and cooldown are owned by GaussCannon.
    [DisallowMultipleComponent]
    public sealed class GaussEmitter : MonoBehaviour
    {
        private Material gaussMaterial;
        private AudioSource voice;
        private AudioClip clip;
        private LineRenderer flash;
        private LineRenderer sight;
        private float flashUntil;
        private float soundVolume;
        private float velocity;
        private float range;
        private float sightLength;
        private Material tracerMaterial;
        private bool configured;
        private JetChassis chassis;

        public void BindChassis(JetChassis visualChassis) { chassis = visualChassis; }

        public void Configure(Material tracer, float volume, float shotVelocity,
            float shotRange, float guideLength)
        {
            if (configured) return;
            configured = true;
            tracerMaterial = tracer;
            soundVolume = volume;
            velocity = shotVelocity;
            range = shotRange;
            sightLength = guideLength;

            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;
            voice.dopplerLevel = 0f;
            clip = MakeGaussClip();
            var shader = Shader.Find("AirCombat/SandboxEffect");
            if (shader != null)
                gaussMaterial = new Material(shader) { name = "Weapon Lab Gauss kinetic tracer (runtime)" };

            var muzzle = chassis != null ? chassis.GaussMuzzle : null;
            var muzzleFlash = new GameObject("Gauss Muzzle Flash");
            muzzleFlash.transform.SetParent(muzzle != null ? muzzle : transform, false);
            flash = muzzleFlash.AddComponent<LineRenderer>();
            flash.sharedMaterial = gaussMaterial != null ? gaussMaterial : tracerMaterial;
            flash.useWorldSpace = false;
            flash.positionCount = 2;
            flash.SetPosition(0, muzzle != null ? new Vector3(0f, 0f, -.4f) : new Vector3(0f, -.55f, 5.2f));
            flash.SetPosition(1, muzzle != null ? new Vector3(0f, 0f, 2.6f) : new Vector3(0f, -.55f, 8.2f));
            flash.widthMultiplier = .65f;
            flash.numCapVertices = 0;
            flash.startColor = new Color(1f, .98f, .85f, .95f);
            flash.endColor = new Color(.95f, .72f, .47f, .18f);
            flash.shadowCastingMode = ShadowCastingMode.Off;
            flash.enabled = false;

            var sightObject = new GameObject("Gauss Forward Sight");
            sightObject.transform.SetParent(muzzle != null ? muzzle : transform, false);
            sight = sightObject.AddComponent<LineRenderer>();
            sight.sharedMaterial = gaussMaterial != null ? gaussMaterial : tracerMaterial;
            sight.useWorldSpace = false;
            sight.positionCount = 2;
            sight.SetPosition(0, muzzle != null ? new Vector3(0f, 0f, .6f) : new Vector3(0f, -.55f, 6.2f));
            sight.widthMultiplier = .16f;
            sight.numCapVertices = 0;
            sight.startColor = new Color(.87f, .91f, .94f, .86f);
            sight.endColor = new Color(.87f, .91f, .94f, .15f);
            sight.shadowCastingMode = ShadowCastingMode.Off;
            sight.receiveShadows = false;
            sight.enabled = false;
        }

        public void TickVisuals(bool selected)
        {
            if (flash != null) flash.enabled = Time.time < flashUntil;
            if (sight == null) return;
            sight.SetPosition(1, chassis != null && chassis.GaussMuzzle != null
                ? new Vector3(0f, 0f, .6f + sightLength)
                : new Vector3(0f, -.55f, 6.2f + sightLength));
            sight.enabled = selected;
        }

        public void Emit(Action onHit)
        {
            Vector3 direction = transform.forward.normalized;
            var socket = chassis != null ? chassis.GaussMuzzle : null;
            Vector3 muzzlePosition = socket != null ? socket.position :
                transform.TransformPoint(new Vector3(0f, -.55f, 5.6f));
            var round = new GameObject("Gauss Slug");
            round.transform.SetPositionAndRotation(muzzlePosition, Quaternion.LookRotation(direction, Vector3.up));
            var core = round.AddComponent<LineRenderer>();
            core.sharedMaterial = gaussMaterial != null ? gaussMaterial : tracerMaterial;
            core.useWorldSpace = true;
            core.positionCount = 2;
            core.widthMultiplier = .42f;
            core.numCapVertices = 0;
            core.startColor = new Color(.67f, .75f, .76f, .38f);
            core.endColor = new Color(1f, .98f, .87f, 1f);
            core.shadowCastingMode = ShadowCastingMode.Off;
            core.receiveShadows = false;
            var projectile = round.AddComponent<BallisticProjectile>();
            projectile.Configure(direction, Mathf.Max(1f, velocity), Mathf.Max(1f, range),
                22f, core, null, onHit, 5);
            flashUntil = Time.time + .045f;
            if (flash != null) flash.enabled = true;
            if (voice != null && clip != null)
            {
                voice.Stop();
                voice.clip = clip;
                voice.volume = Mathf.Clamp01(soundVolume * 1.5f);
                voice.pitch = 1f;
                voice.Play();
            }
        }

        // Identical deterministic mechanical crack/thump from the original cannon.
        private static AudioClip MakeGaussClip()
        {
            const int sampleRate = 24000;
            int frames = Mathf.RoundToInt(.19f * sampleRate);
            var samples = new float[frames];
            uint noiseState = 0xA511E9B3u;
            for (int i = 0; i < frames; i++)
            {
                float t = i / (float)sampleRate;
                noiseState = unchecked(noiseState * 1664525u + 1013904223u);
                float noise = ((noiseState >> 9) / 8388608f) * 2f - 1f;
                float attack = Mathf.Min(1f, t * 1200f);
                float crack = noise * Mathf.Exp(-105f * t);
                float thump = Mathf.Sin(2f * Mathf.PI * (155f * t - 30f * t * t)) * Mathf.Exp(-27f * t);
                float metal = Mathf.Sin(2f * Mathf.PI * 920f * t) * Mathf.Exp(-55f * t);
                samples[i] = Mathf.Clamp(attack * (.63f * crack + .6f * thump + .16f * metal), -1f, 1f);
            }
            var created = AudioClip.Create("Weapon Lab Gauss mechanical (runtime)", frames, 1, sampleRate, false);
            created.SetData(samples, 0);
            return created;
        }

        private void OnDestroy()
        {
            if (gaussMaterial != null) Destroy(gaussMaterial);
            if (clip != null) Destroy(clip);
        }
    }
}
