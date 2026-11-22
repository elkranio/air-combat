using UnityEngine;
using UnityEngine.InputSystem;

namespace AirCombat
{
    // Weapon Lab only: projectile autocannon with optional bounded free aim. Sandbox missiles untouched.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(130)] // sample aim after camera follow and CameraLookAhead
    public sealed class WeaponLabCannon : MonoBehaviour
    {
        [SerializeField] private Material tracerMaterial;
        [SerializeField] private AudioClip shotClip; // synthetic fallback if imported MG02 is missing
        [SerializeField] private AudioClip[] soundVariants; // imported variants kept; only MG02 is used
        [SerializeField, Min(1f)] private float roundsPerSecond = 14f;
        [SerializeField, Range(2, 8)] private int burstSize = 4;
        [SerializeField, Min(.05f)] private float burstCooldown = .45f;
        [SerializeField, Min(.1f)] private float gaussCooldown = 1.5f;
        [SerializeField, Min(1f)] private float gaussVelocity = 300f;
        [SerializeField, Min(1f)] private float gaussRange = 250f;
        [SerializeField, Range(10f, 80f)] private float gaussSightLength = 36f;
        [SerializeField, Min(1f)] private float muzzleVelocity = 420f;
        [SerializeField, Min(1f)] private float maxRange = 430f;
        [SerializeField, Min(.1f)] private float tracerLength = 8f;
        [SerializeField, Range(.01f, 1f)] private float soundVolume = .30f;

        private const int VoiceCount = 5;
        private AudioSource[] voices;
        private int nextVoice;
        private Material haloMaterial;
        private Material gaussMaterial;
        private AudioSource gaussVoice;
        private AudioClip gaussClip;
        private LineRenderer gaussFlash;
        private LineRenderer gaussSight;
        private float gaussFlashUntil;
        private BoundedAim freeAim;
        private TargetLock missileLock;
        private Renderer[] flashes;
        private float[] flashUntil;
        private float nextShot;
        private float nextGaussReadyAt;
        private bool gaussSelected;
        private bool missileSelected;
        private int burstShotsRemaining;
        private int nextBarrel;

        public int FiredCount { get; private set; }
        public int HitCount { get; private set; }
        public string FireModeLabel => missileSelected ? "MISSILE" : gaussSelected
            ? (Time.time >= nextGaussReadyAt ? "GAUSS READY" : "GAUSS " + (nextGaussReadyAt - Time.time).ToString("F1") + "s")
            : "BURST " + Mathf.Max(2, burstSize);
        public string AimModeLabel => missileSelected ? "CURSOR" :
            gaussSelected ? "FIXED" : freeAim != null ? freeAim.ModeLabel : "FIXED";
        public string SoundModeLabel => soundVariants != null && soundVariants.Length == 4 && soundVariants[1] != null
            ? "MG 02" : "SYNTH FALLBACK";

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
        }

        public void ConfigureAim(BoundedAim component) { freeAim = component; }
        public void ConfigureMissileLock(TargetLock component) { missileLock = component; }

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
            gaussVoice = gameObject.AddComponent<AudioSource>();
            gaussVoice.playOnAwake = false;
            gaussVoice.spatialBlend = 0f;
            gaussVoice.dopplerLevel = 0f;
            gaussClip = MakeGaussClip();
            var effectShader = Shader.Find("AirCombat/SandboxEffect");
            if (effectShader != null)
            {
                haloMaterial = new Material(effectShader) { name = "Weapon Lab tracer halo (runtime)" };
                gaussMaterial = new Material(effectShader) { name = "Weapon Lab Gauss kinetic tracer (runtime)" };
            }
            else Debug.LogWarning("Weapon Lab effect shader missing; Gauss uses fallback tracer material.", this);
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
            var muzzleFlash = new GameObject("Gauss Muzzle Flash");
            muzzleFlash.transform.SetParent(transform, false);
            gaussFlash = muzzleFlash.AddComponent<LineRenderer>();
            gaussFlash.sharedMaterial = gaussMaterial != null ? gaussMaterial : tracerMaterial;
            gaussFlash.useWorldSpace = false;
            gaussFlash.positionCount = 2;
            gaussFlash.SetPosition(0, new Vector3(0f, -.55f, 5.2f));
            gaussFlash.SetPosition(1, new Vector3(0f, -.55f, 8.2f));
            gaussFlash.widthMultiplier = .65f;
            gaussFlash.numCapVertices = 0;
            gaussFlash.startColor = new Color(1f, .98f, .85f, .95f);
            gaussFlash.endColor = new Color(.95f, .72f, .47f, .18f);
            gaussFlash.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            gaussFlash.enabled = false;

            // Short pale forward sight, attached to the aircraft (not the mouse or the enemy).
            var sightObject = new GameObject("Gauss Forward Sight");
            sightObject.transform.SetParent(transform, false);
            gaussSight = sightObject.AddComponent<LineRenderer>();
            gaussSight.sharedMaterial = gaussMaterial != null ? gaussMaterial : tracerMaterial;
            gaussSight.useWorldSpace = false;
            gaussSight.positionCount = 2;
            gaussSight.SetPosition(0, new Vector3(0f, -.55f, 6.2f));
            gaussSight.widthMultiplier = .16f;
            gaussSight.numCapVertices = 0;
            gaussSight.startColor = new Color(.87f, .91f, .94f, .86f);
            gaussSight.endColor = new Color(.87f, .91f, .94f, .15f);
            gaussSight.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            gaussSight.receiveShadows = false;
            gaussSight.enabled = false;
        }

        private void LateUpdate()
        {
            if (flashes != null)
                for (int i = 0; i < flashes.Length; i++)
                    if (flashes[i] != null) flashes[i].enabled = Time.time < flashUntil[i];
            if (gaussFlash != null) gaussFlash.enabled = Time.time < gaussFlashUntil;

            var keyboard = Keyboard.current;
            if (Application.isFocused && keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame)
                {
                    // Changing weapons must not reset an in-progress burst's cooldown.
                    if (burstShotsRemaining > 0)
                        nextShot = Mathf.Max(nextShot, Time.time + Mathf.Max(.05f, burstCooldown));
                    gaussSelected = false;
                    missileSelected = false;
                    burstShotsRemaining = 0;
                }
                if (keyboard.digit2Key.wasPressedThisFrame)
                {
                    if (burstShotsRemaining > 0)
                        nextShot = Mathf.Max(nextShot, Time.time + Mathf.Max(.05f, burstCooldown));
                    gaussSelected = true;
                    missileSelected = false;
                    burstShotsRemaining = 0; // Switching weapons cancels the unfinished burst.
                }
                if (keyboard.digit3Key.wasPressedThisFrame)
                {
                    if (burstShotsRemaining > 0)
                        nextShot = Mathf.Max(nextShot, Time.time + Mathf.Max(.05f, burstCooldown));
                    gaussSelected = false;
                    missileSelected = true;
                    burstShotsRemaining = 0;
                }
            }
            // TargetLock runs later in LateUpdate (order 200), so mode changes are immediate.
            if (missileLock != null) missileLock.SetLockingEnabled(missileSelected);
            if (gaussSight != null)
            {
                gaussSight.SetPosition(1, new Vector3(0f, -.55f, 6.2f + gaussSightLength));
                gaussSight.enabled = gaussSelected;
            }

            var mouse = Mouse.current;
            bool held = Application.isFocused && mouse != null && mouse.leftButton.isPressed;
            bool burstActive = !gaussSelected && !missileSelected && burstShotsRemaining > 0;
            if (freeAim != null) freeAim.Sample(!gaussSelected && !missileSelected, held || burstActive);
            if (missileSelected) return; // Lock evaluation only; launcher is a later atom.
            if (gaussSelected)
            {
                // One shot per new click. Holding/reselecting never repeats or skips cooldown.
                if (mouse != null && Application.isFocused && mouse.leftButton.wasPressedThisFrame &&
                    Time.time >= nextGaussReadyAt)
                {
                    FireGauss();
                    nextGaussReadyAt = Time.time + Mathf.Max(.1f, gaussCooldown);
                }
                return;
            }
            if (tracerMaterial == null)
            {
                burstShotsRemaining = 0;
                nextShot = Time.time;
                return;
            }
            // Releasing/re-pressing cannot bypass the interval between bursts.
            if (burstShotsRemaining == 0 && held && Time.time >= nextShot)
            {
                burstShotsRemaining = Mathf.Max(2, burstSize);
                nextShot = Time.time; // No backlog of instant shots after a long pause.
            }
            if (burstShotsRemaining == 0) return;

            float interval = 1f / Mathf.Max(1f, roundsPerSecond);
            // Limit catch-up after a frame hitch; never dump dozens of rounds at once.
            int emitted = 0;
            while (Time.time >= nextShot && emitted < 3)
            {
                nextShot += interval;
                emitted++;
                Fire();
                if (--burstShotsRemaining == 0)
                {
                    nextShot = Time.time + Mathf.Max(.05f, burstCooldown);
                    break;
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
            var clip = soundVariants != null && soundVariants.Length == 4 && soundVariants[1] != null
                ? soundVariants[1] : shotClip; // MG02 approved; no G-mode cycling.
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

        // Heavy fixed-forward cannon. A real moving projectile, not hitscan and not free aim.
        private void FireGauss()
        {
            Vector3 direction = transform.forward.normalized;
            Vector3 muzzle = transform.TransformPoint(new Vector3(0f, -.55f, 5.6f));
            var round = new GameObject("Gauss Slug");
            round.transform.SetPositionAndRotation(muzzle, Quaternion.LookRotation(direction, Vector3.up));
            var core = round.AddComponent<LineRenderer>();
            core.sharedMaterial = gaussMaterial != null ? gaussMaterial : tracerMaterial;
            core.useWorldSpace = true;
            core.positionCount = 2;
            core.widthMultiplier = .42f;
            core.numCapVertices = 0; // Sharp, elongated kinetic streak rather than a glowing orb.
            core.startColor = new Color(.67f, .75f, .76f, .38f);
            core.endColor = new Color(1f, .98f, .87f, 1f);
            core.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            core.receiveShadows = false;
            var projectile = round.AddComponent<WeaponLabProjectile>();
            projectile.Configure(direction, Mathf.Max(1f, gaussVelocity), Mathf.Max(1f, gaussRange),
                22f, core, null, this, 5);
            gaussFlashUntil = Time.time + .045f;
            if (gaussFlash != null) gaussFlash.enabled = true;
            if (gaussVoice != null && gaussClip != null)
            {
                gaussVoice.Stop();
                gaussVoice.clip = gaussClip;
                gaussVoice.volume = Mathf.Clamp01(soundVolume * 1.5f);
                gaussVoice.pitch = 1f;
                gaussVoice.Play();
            }
            FiredCount++;
        }

        // Short mechanical crack and low impact: no descending synth chirp/"pew".
        // Deterministic noise, generated once at runtime; no audio imports required.
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
            var clip = AudioClip.Create("Weapon Lab Gauss mechanical (runtime)", frames, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public void RegisterHit() { HitCount++; }

        private void OnDestroy()
        {
            if (haloMaterial != null) Destroy(haloMaterial);
            if (gaussMaterial != null) Destroy(gaussMaterial);
            if (gaussClip != null) Destroy(gaussClip);
        }

        private static Vector3 MuzzleOffset(int barrel)
        {
            return new Vector3(barrel == 0 ? -1.35f : 1.35f, -.55f, 5.6f);
        }
    }
}
