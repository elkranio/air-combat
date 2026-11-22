using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace AirCombat
{
    // BURST shot presentation and physical projectile creation. Selection and
    // firing cadence belong to WeaponSelection and Autocannon respectively.
    [DisallowMultipleComponent]
    public sealed class AutocannonEmitter : MonoBehaviour
    {
        private const int VoiceCount = 5;
        private Material tracerMaterial;
        private Material haloMaterial;
        private AudioClip shotClip;
        private AudioClip[] soundVariants;
        private float soundVolume;
        private float muzzleVelocity;
        private float maxRange;
        private float tracerLength;
        private AudioSource[] voices;
        private Renderer[] flashes;
        private float[] flashUntil;
        private int nextVoice;
        private int nextBarrel;
        private bool configured;
        private JetChassis chassis;

        public void BindChassis(JetChassis visualChassis) { chassis = visualChassis; }

        public void Configure(Material tracer, AudioClip fallbackShot, AudioClip[] clips,
            float volume, float velocity, float range, float length)
        {
            if (configured) return; // Idempotent for future prefab/scene composition.
            configured = true;
            tracerMaterial = tracer;
            shotClip = fallbackShot;
            soundVariants = clips;
            soundVolume = volume;
            muzzleVelocity = velocity;
            maxRange = range;
            tracerLength = length;

            // Original bounded voice pool, shot variation and muzzle positions.
            voices = new AudioSource[VoiceCount];
            for (int i = 0; i < voices.Length; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.dopplerLevel = 0f;
                voices[i] = source;
            }
            var shader = Shader.Find("AirCombat/SandboxEffect");
            if (shader != null)
                haloMaterial = new Material(shader) { name = "Weapon Lab tracer halo (runtime)" };
            else Debug.LogWarning("Weapon Lab effect shader missing; Gauss uses fallback tracer material.", this);

            flashes = new Renderer[2];
            flashUntil = new float[2];
            for (int i = 0; i < flashes.Length; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = i == 0 ? "Autocannon Muzzle L" : "Autocannon Muzzle R";
                var socket = chassis != null ? (i == 0 ? chassis.AutocannonLeft : chassis.AutocannonRight) : null;
                go.transform.SetParent(socket != null ? socket : transform, false);
                go.transform.localPosition = socket != null ? Vector3.zero : MuzzleOffset(i);
                go.transform.localScale = new Vector3(1.02f, 1.02f, 1.85f);
                var collider = go.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
                flashes[i] = go.GetComponent<Renderer>();
                flashes[i].sharedMaterial = tracerMaterial;
                flashes[i].shadowCastingMode = ShadowCastingMode.Off;
                flashes[i].enabled = false;
            }
        }

        public void SetSoundVariants(AudioClip[] clips) { soundVariants = clips; }

        // Called at the same point in WeaponController.LateUpdate as before.
        public void TickVisuals()
        {
            if (flashes == null) return;
            for (int i = 0; i < flashes.Length; i++)
                if (flashes[i] != null) flashes[i].enabled = Time.time < flashUntil[i];
        }

        public void Emit(Vector3 direction, Action onHit, int firedCount)
        {
            int barrel = nextBarrel;
            nextBarrel = 1 - nextBarrel;
            var socket = chassis != null ? (barrel == 0 ? chassis.AutocannonLeft : chassis.AutocannonRight) : null;
            var muzzle = socket != null ? socket.position : transform.TransformPoint(MuzzleOffset(barrel));
            var round = new GameObject("Autocannon Round");
            round.transform.SetPositionAndRotation(muzzle, Quaternion.LookRotation(direction, Vector3.up));
            var line = round.AddComponent<LineRenderer>();
            line.sharedMaterial = tracerMaterial;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.widthMultiplier = .50f;
            line.numCapVertices = 2;
            line.startColor = new Color(1f, .69f, .33f, 1f);
            line.endColor = new Color(1f, 1f, .90f, 1f);
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            LineRenderer halo = null;
            if (haloMaterial != null)
            {
                // LineRenderer halo remains on a child; never duplicate on one object.
                var haloObject = new GameObject("Tracer Halo");
                haloObject.transform.SetParent(round.transform, false);
                halo = haloObject.AddComponent<LineRenderer>();
                halo.sharedMaterial = haloMaterial;
                halo.useWorldSpace = true;
                halo.positionCount = 2;
                halo.widthMultiplier = 1.10f;
                halo.numCapVertices = 2;
                halo.startColor = new Color(1f, .55f, .17f, .13f);
                halo.endColor = new Color(1f, .91f, .50f, .22f);
                halo.shadowCastingMode = ShadowCastingMode.Off;
                halo.receiveShadows = false;
            }
            var projectile = round.AddComponent<BallisticProjectile>();
            projectile.Configure(direction, muzzleVelocity, maxRange, tracerLength, line, halo, onHit);
            flashUntil[barrel] = Time.time + .052f;
            flashes[barrel].enabled = true;
            var clip = soundVariants != null && soundVariants.Length == 4 && soundVariants[1] != null
                ? soundVariants[1] : shotClip;
            if (clip != null && voices != null)
            {
                var source = voices[nextVoice++ % voices.Length];
                source.Stop();
                source.clip = clip;
                source.volume = soundVolume;
                source.pitch = 0.97f + .014f * (firedCount % 5);
                source.Play();
            }
        }

        private static Vector3 MuzzleOffset(int barrel)
        {
            return new Vector3(barrel == 0 ? -1.35f : 1.35f, -.55f, 5.6f);
        }

        private void OnDestroy()
        {
            if (haloMaterial != null) Destroy(haloMaterial);
        }
    }
}
