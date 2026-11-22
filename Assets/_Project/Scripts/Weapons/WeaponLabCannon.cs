using UnityEngine;
using UnityEngine.InputSystem;

namespace AirCombat
{
    // Weapon Lab only: forward-firing projectile autocannon. Does not change Sandbox missiles.
    [DisallowMultipleComponent]
    public sealed class WeaponLabCannon : MonoBehaviour
    {
        [SerializeField] private Material tracerMaterial;
        [SerializeField] private AudioClip shotClip;
        [SerializeField, Min(1f)] private float roundsPerSecond = 14f;
        [SerializeField, Min(1f)] private float muzzleVelocity = 420f;
        [SerializeField, Min(1f)] private float maxRange = 430f;
        [SerializeField, Min(.1f)] private float tracerLength = 8f;
        [SerializeField, Range(.01f, 1f)] private float soundVolume = .44f;
        [SerializeField] private bool heatLimiterEnabled;
        [SerializeField, Range(.001f, 1f)] private float heatPerShot = .044f;
        [SerializeField, Range(.001f, 2f)] private float coolingPerSecond = .2f;
        [SerializeField, Range(.01f, .95f)] private float restartHeat = .33f;

        private AudioSource shotSource;
        private Renderer[] flashes;
        private float[] flashUntil;
        private float nextShot;
        private float heat;
        private bool overheated;
        private int nextBarrel;

        public bool HeatLimiterEnabled => heatLimiterEnabled;
        public float Heat01 => Mathf.Clamp01(heat);
        public bool IsOverheated => overheated;
        public int FiredCount { get; private set; }
        public int HitCount { get; private set; }

        public void Configure(Material tracer, AudioClip shot)
        {
            tracerMaterial = tracer;
            shotClip = shot;
        }

        private void Awake()
        {
            shotSource = GetComponent<AudioSource>();
            if (shotSource == null) shotSource = gameObject.AddComponent<AudioSource>();
            shotSource.playOnAwake = false;
            shotSource.spatialBlend = 0f;
            shotSource.dopplerLevel = 0f;
            flashes = new Renderer[2];
            flashUntil = new float[2];
            for (int i = 0; i < flashes.Length; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = i == 0 ? "Autocannon Muzzle L" : "Autocannon Muzzle R";
                go.transform.SetParent(transform, false);
                go.transform.localPosition = MuzzleOffset(i);
                go.transform.localScale = new Vector3(.85f, .85f, 1.65f);
                var collider = go.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
                flashes[i] = go.GetComponent<Renderer>();
                flashes[i].sharedMaterial = tracerMaterial;
                flashes[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                flashes[i].enabled = false;
            }
        }

        private void Update()
        {
            if (flashes != null)
                for (int i = 0; i < flashes.Length; i++)
                    if (flashes[i] != null) flashes[i].enabled = Time.time < flashUntil[i];

            var keyboard = Keyboard.current;
            if (Application.isFocused && keyboard != null && keyboard.tKey.wasPressedThisFrame)
            {
                heatLimiterEnabled = !heatLimiterEnabled;
                heat = 0f;
                overheated = false;
            }
            heat = Mathf.Max(0f, heat - coolingPerSecond * Time.deltaTime);
            if (overheated && heat <= restartHeat) overheated = false;

            var mouse = Mouse.current;
            bool held = Application.isFocused && mouse != null && mouse.leftButton.isPressed;
            if (!held || tracerMaterial == null)
            {
                nextShot = Time.time;
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
            round.transform.SetPositionAndRotation(muzzle, transform.rotation);
            var line = round.AddComponent<LineRenderer>();
            line.sharedMaterial = tracerMaterial;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.widthMultiplier = .27f;
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            var projectile = round.AddComponent<WeaponLabProjectile>();
            projectile.Configure(transform.forward, muzzleVelocity, maxRange, tracerLength, line, this);
            flashUntil[barrel] = Time.time + .052f;
            flashes[barrel].enabled = true;
            if (shotClip != null && shotSource != null)
            {
                shotSource.pitch = 0.96f + 0.025f * (FiredCount % 5);
                shotSource.PlayOneShot(shotClip, soundVolume);
            }
            FiredCount++;
        }

        public void RegisterHit() { HitCount++; }

        private static Vector3 MuzzleOffset(int barrel)
        {
            return new Vector3(barrel == 0 ? -1.35f : 1.35f, -.55f, 5.6f);
        }
    }
}
