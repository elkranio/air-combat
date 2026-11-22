using UnityEngine;
using UnityEngine.InputSystem;

namespace AirCombat
{
    // Weapon Lab only: projectile autocannon with optional bounded free aim. Sandbox missiles untouched.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(130)] // sample aim after camera follow and WeaponLabCameraLead
    public sealed class WeaponLabCannon : MonoBehaviour
    {
        [SerializeField] private Material tracerMaterial;
        [SerializeField] private AudioClip shotClip; // original synthetic fallback for A/B
        [SerializeField] private AudioClip[] soundVariants;
        [SerializeField, Range(0, 4)] private int soundMode = 1;
        [SerializeField, Min(1f)] private float roundsPerSecond = 14f;
        [SerializeField] private bool burstMode; // Continuous by default, A/B via M.
        [SerializeField, Range(2, 8)] private int burstSize = 4;
        [SerializeField, Min(.05f)] private float burstCooldown = .45f;
        [SerializeField, Min(1f)] private float muzzleVelocity = 420f;
        [SerializeField, Min(1f)] private float maxRange = 430f;
        [SerializeField, Min(.1f)] private float tracerLength = 8f;
        [SerializeField, Range(.01f, 1f)] private float soundVolume = .30f;
        [SerializeField] private bool heatLimiterEnabled;
        [SerializeField, Range(.001f, 1f)] private float heatPerShot = .044f;
        [SerializeField, Range(.001f, 2f)] private float coolingPerSecond = .2f;
        [SerializeField, Range(.01f, .95f)] private float restartHeat = .33f;

        private const int VoiceCount = 5;
        private AudioSource[] voices;
        private int nextVoice;
        private Material haloMaterial;
        private WeaponLabFreeAim freeAim;
        private Renderer[] flashes;
        private float[] flashUntil;
        private float nextShot;
        private int burstShotsRemaining;
        private float heat;
        private bool overheated;
        private int nextBarrel;

        public bool HeatLimiterEnabled => heatLimiterEnabled;
        public float Heat01 => Mathf.Clamp01(heat);
        public bool IsOverheated => overheated;
        public int FiredCount { get; private set; }
        public int HitCount { get; private set; }
        public string FireModeLabel => burstMode ? "BURST " + Mathf.Max(2, burstSize) : "CONTINUOUS";
        public string AimModeLabel => freeAim != null ? freeAim.ModeLabel : "FIXED";
        public string SoundModeLabel => soundVariants != null && soundVariants.Length == 4 && soundMode > 0
            ? "MG " + soundMode.ToString("00") : "SYNTH";

        public void Configure(Material tracer, AudioClip shot)
        {
            tracerMaterial = tracer;
            shotClip = shot;
        }

        // Editor-side importer sets four short CC0 variants; raw source stays in Resources/Audio.
        public void ConfigureSoundVariants(AudioClip[] clips)
        {
            if (clips == null || clips.Length != 4 || System.Array.Exists(clips, c => c == null))
                throw new System.ArgumentException("Expected four imported machine gun clips.");
            soundVariants = (AudioClip[])clips.Clone();
            soundMode = 1;
        }

        public void ConfigureAim(WeaponLabFreeAim component) { freeAim = component; }

        private void Awake()
        {
            // A bounded voice pool prevents long overlapping tails from becoming a wall of noise.
            voices = new AudioSource[VoiceCount];
            for (int i = 0; i < voices.Length; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.dopplerLevel = 0f;
                voices[i] = source;
            }
            var effectShader = Shader.Find("AirCombat/SandboxEffect");
            if (effectShader != null) haloMaterial = new Material(effectShader) { name = "Weapon Lab tracer halo (runtime)" };
            else Debug.LogWarning("Weapon Lab halo shader missing; using core tracers only.", this);
            flashes = new Renderer[2];
            flashUntil = new float[2];
            for (int i = 0; i < flashes.Length; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = i == 0 ? "Autocannon Muzzle L" : "Autocannon Muzzle R";
                go.transform.SetParent(transform, false);
                go.transform.localPosition = MuzzleOffset(i);
                go.transform.localScale = new Vector3(1.02f, 1.02f, 1.85f);
                var collider = go.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
                flashes[i] = go.GetComponent<Renderer>();
                flashes[i].sharedMaterial = tracerMaterial;
                flashes[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                flashes[i].enabled = false;
            }
        }

        private void LateUpdate()
        {
            if (flashes != null)
                for (int i = 0; i < flashes.Length; i++)
                    if (flashes[i] != null) flashes[i].enabled = Time.time < flashUntil[i];

            var keyboard = Keyboard.current;
            if (Application.isFocused && keyboard != null)
            {
                if (keyboard.tKey.wasPressedThisFrame)
                {
                    heatLimiterEnabled = !heatLimiterEnabled;
                    heat = 0f;
                    overheated = false;
                }
                if (keyboard.gKey.wasPressedThisFrame && soundVariants != null && soundVariants.Length == 4)
                    soundMode = (soundMode + 1) % 5; // Synth / MG 01..04; no reload required.
                if (keyboard.mKey.wasPressedThisFrame)
                {
                    burstMode = !burstMode;
                    burstShotsRemaining = 0;
                    nextShot = Time.time;
                }
            }
            heat = Mathf.Max(0f, heat - coolingPerSecond * Time.deltaTime);
            if (overheated && heat <= restartHeat) overheated = false;

            var mouse = Mouse.current;
            bool held = Application.isFocused && mouse != null && mouse.leftButton.isPressed;
            // A click commits all four shots, even if the trigger is released early.
            bool burstActive = burstMode && burstShotsRemaining > 0;
            if (freeAim != null) freeAim.Sample(held || burstActive);
            if (tracerMaterial == null)
            {
                burstShotsRemaining = 0;
                nextShot = Time.time;
                return;
            }
            if (burstMode)
            {
                // Releasing/re-pressing cannot bypass the interval between bursts.
                if (burstShotsRemaining == 0 && held && Time.time >= nextShot)
                {
                    burstShotsRemaining = Mathf.Max(2, burstSize);
                    nextShot = Time.time; // No backlog of instant shots after a long pause.
                }
                if (burstShotsRemaining == 0) return;
            }
            else if (!held)
            {
                nextShot = Time.time; // Preserve existing continuous-fire behavior.
                return;
            }

            float interval = 1f / Mathf.Max(1f, roundsPerSecond);
            // Limit catch-up after a frame hitch; never dump dozens of rounds at once.
            int emitted = 0;
            while (Time.time >= nextShot && emitted < 3)
            {
                nextShot += interval;
                emitted++;
                if (heatLimiterEnabled && overheated) continue;
                Fire();
                if (burstMode && --burstShotsRemaining == 0)
                    nextShot = Time.time + Mathf.Max(.05f, burstCooldown);
                if (heatLimiterEnabled)
                {
                    heat = Mathf.Min(1f, heat + heatPerShot);
                    if (heat >= 1f) overheated = true;
                }
            }
            if (emitted >= 3 && nextShot < Time.time) nextShot = Time.time + interval;
        }

        private void Fire()
        {
            int barrel = nextBarrel;
            nextBarrel = 1 - nextBarrel;
            var muzzle = transform.TransformPoint(MuzzleOffset(barrel));
            var round = new GameObject("Autocannon Round");
            Vector3 direction = freeAim != null ? freeAim.FireDirection : transform.forward;
            round.transform.SetPositionAndRotation(muzzle, Quaternion.LookRotation(direction, Vector3.up));
            var line = round.AddComponent<LineRenderer>();
            line.sharedMaterial = tracerMaterial;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.widthMultiplier = .50f; // Formerly .27: legible at normal orthographic zoom.
            line.numCapVertices = 2;
            line.startColor = new Color(1f, .69f, .33f, 1f);
            line.endColor = new Color(1f, 1f, .90f, 1f);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            LineRenderer halo = null;
            if (haloMaterial != null)
            {
                // Unity allows only one LineRenderer per GameObject. Keep the soft
                // halo on a child; the moving projectile owns both renderers.
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
                halo.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                halo.receiveShadows = false;
            }
            var projectile = round.AddComponent<WeaponLabProjectile>();
            projectile.Configure(direction, muzzleVelocity, maxRange, tracerLength, line, halo, this);
            flashUntil[barrel] = Time.time + .052f;
            flashes[barrel].enabled = true;
            var clip = soundVariants != null && soundVariants.Length == 4 && soundMode > 0
                ? soundVariants[soundMode - 1] : shotClip;
            if (clip != null && voices != null)
            {
                var source = voices[nextVoice++ % voices.Length];
                source.Stop(); // limit polyphony; replace the oldest shot if needed
                source.clip = clip;
                source.volume = soundVolume;
                source.pitch = 0.97f + .014f * (FiredCount % 5);
                source.Play();
            }
            FiredCount++;
        }

        public void RegisterHit() { HitCount++; }

        private void OnDestroy()
        {
            if (haloMaterial != null) Destroy(haloMaterial);
        }

        private static Vector3 MuzzleOffset(int barrel)
        {
            return new Vector3(barrel == 0 ? -1.35f : 1.35f, -.55f, 5.6f);
        }
    }
}
