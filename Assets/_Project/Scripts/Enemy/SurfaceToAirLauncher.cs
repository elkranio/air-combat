using UnityEngine;

namespace AirCombat
{
    // M1b: fixed vertical-launch SAM. Radar acquisition is independent of
    // launcher orientation; the missile owns the pitch-over and pursuit phases.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(180)]
    public sealed class SurfaceToAirLauncher : DamageableTarget, IRadarContact
    {
        [Header("Acquisition")]
        [SerializeField, Min(1f)] private float engagementRange = 350f;
        [SerializeField, Min(.1f)] private float lockSeconds = 1.4f;
        [Header("Fire")]
        [SerializeField, Min(.1f)] private float reloadSeconds = 7f;
        [SerializeField, Min(1)] private int maxHp = 6;
        [SerializeField] private Transform launchSocket;
        [SerializeField] private SurfaceToAirMissile missilePrefab;

        private Transform target;
        private PlayerHitPoints hitPoints;
        private AircraftRadar radar;
        private SurfaceToAirMissile missile;
        private AudioSource warningAudio;
        private AudioClip warningClip;
        private float lockProgress;
        private float nextLaunchAt;
        private float nextBeepAt;
        private int hp;

        public int Hp => hp;
        public int MaxHp => maxHp;
        public bool IsDown => hp <= 0;
        public int LaunchedCount { get; private set; }
        public bool HasIncomingMissile => missile != null && missile.IsFlying;
        public float LockProgress01 => Mathf.Clamp01(lockProgress / Mathf.Max(.1f, lockSeconds));
        public float ReloadLeft => Mathf.Max(0f, nextLaunchAt - Time.time);
        public string StatusLabel
        {
            get
            {
                if (IsDown) return "DESTROYED";
                if (HasIncomingMissile) return "LAUNCHED / " + missile.PhaseLabel;
                if (missilePrefab == null || launchSocket == null) return "UNCONFIGURED";
                if (target == null || hitPoints == null || !hitPoints.IsAlive) return "NO TARGET";
                Vector3 flat = target.position - transform.position;
                flat.y = 0f;
                if (flat.sqrMagnitude > engagementRange * engagementRange) return "OUT OF RANGE";
                if (Time.time < nextLaunchAt) return "RELOADING";
                return "ACQUIRING";
            }
        }
        public override CombatTargetDomain Domain => CombatTargetDomain.Ground;
        public RadarContactType RadarKind => RadarContactType.GroundDefense;
        public Vector3 RadarPosition => transform.position;
        public float RadarThreatRadius => engagementRange;
        public bool IsRadarVisible => isActiveAndEnabled && !IsDown;

        // Editor installer/upgrade supplies scene-independent prefab references.
        public void ConfigureArt(Transform muzzle, SurfaceToAirMissile projectile)
        {
            launchSocket = muzzle;
            missilePrefab = projectile;
        }

        public void BindTarget(Transform aircraft, PlayerHitPoints health, AircraftRadar sensor)
        {
            target = aircraft;
            hitPoints = health;
            radar = sensor;
            if (warningAudio == null)
            {
                warningAudio = gameObject.AddComponent<AudioSource>();
                warningAudio.playOnAwake = false;
                warningAudio.spatialBlend = 0f; // Cockpit receiver, not world audio.
                warningAudio.volume = .46f;
                warningAudio.dopplerLevel = 0f;
                warningClip = CreateWarningTone();
            }
            ResetEncounter();
        }

        private void Awake() { hp = maxHp; }

        public void ResetEncounter()
        {
            if (missile != null) Destroy(missile.gameObject);
            missile = null;
            hp = maxHp;
            lockProgress = 0f;
            nextLaunchAt = Time.time;
            nextBeepAt = 0f;
            LaunchedCount = 0;
        }

        private void LateUpdate()
        {
            // An already-launched missile is still a threat when its site is destroyed.
            if (HasIncomingMissile)
            {
                WarningBeep(.18f);
                lockProgress = 0f;
                return;
            }
            if (IsDown || target == null || hitPoints == null || !hitPoints.IsAlive ||
                missilePrefab == null || launchSocket == null)
            {
                lockProgress = 0f;
                return;
            }
            Vector3 delta = target.position - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude > engagementRange * engagementRange)
            {
                lockProgress = 0f;
                return;
            }

            // Range + uninterrupted dwell time are the ONLY lock requirements.
            // The site has no traverse servo and can fire at a maneuvering jet.
            if (Time.time < nextLaunchAt)
            {
                lockProgress = 0f;
                return;
            }
            lockProgress += Time.deltaTime;
            WarningBeep(lockProgress >= lockSeconds * .65f ? .22f : .38f);
            if (lockProgress < lockSeconds) return;
            lockProgress = 0f;
            nextLaunchAt = Time.time + reloadSeconds;
            // Force +Y ejection even when upgrading a legacy angled prefab.
            // The missile independently handles the pitch-over and pursuit.
            missile = Instantiate(missilePrefab, launchSocket.position,
                Quaternion.LookRotation(Vector3.up, Vector3.forward));
            missile.Launch(target, hitPoints, radar, transform);
            LaunchedCount++;
        }

        private void WarningBeep(float interval)
        {
            if (warningAudio == null || warningClip == null || Time.time < nextBeepAt) return;
            warningAudio.PlayOneShot(warningClip);
            nextBeepAt = Time.time + interval;
        }

        private static AudioClip CreateWarningTone()
        {
            const int sampleRate = 22050;
            const int samples = 2646; // 120ms: audible chirp with short release.
            float[] wave = new float[samples];
            for (int i = 0; i < wave.Length; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Min(1f, t * 90f) * Mathf.Min(1f, (samples - i) / 350f);
                wave[i] = Mathf.Sin(2f * Mathf.PI * 880f * t) * envelope * .46f;
            }
            var clip = AudioClip.Create("SAM threat receiver / beep", samples, 1, sampleRate, false);
            clip.SetData(wave, 0);
            return clip;
        }

        public override bool ApplyDamage(Vector3 point, int damage)
        {
            if (IsDown || damage <= 0) return false;
            hp = Mathf.Max(0, hp - damage);
            if (IsDown) lockProgress = 0f;
            return true;
        }

        private void OnDestroy()
        {
            if (warningClip != null) Destroy(warningClip);
        }
    }
}
