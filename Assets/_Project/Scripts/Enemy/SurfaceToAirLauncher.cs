using UnityEngine;

namespace AirCombat
{
    // M1: independent, stationary SAM. The authored prefab owns turret geometry;
    // the scene binds its target, sensor and health receiver at runtime.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(180)]
    public sealed class SurfaceToAirLauncher : DamageableTarget, IRadarContact
    {
        [Header("Acquisition")]
        [SerializeField, Min(1f)] private float engagementRange = 350f;
        [SerializeField, Min(.1f)] private float lockSeconds = 1.4f;
        [SerializeField, Min(1f)] private float yawSpeed = 65f;
        [SerializeField, Min(1f)] private float pitchSpeed = 40f;
        [SerializeField, Range(1f, 30f)] private float lockTolerance = 11f;
        [Header("Fire")]
        [SerializeField, Min(.1f)] private float reloadSeconds = 7f;
        [SerializeField, Min(1)] private int maxHp = 6;
        [SerializeField] private Transform yawPivot;
        [SerializeField] private Transform pitchPivot;
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
        public override CombatTargetDomain Domain => CombatTargetDomain.Ground;
        public RadarContactType RadarKind => RadarContactType.GroundDefense;
        public Vector3 RadarPosition => transform.position;
        public float RadarThreatRadius => engagementRange;
        public bool IsRadarVisible => isActiveAndEnabled && !IsDown;

        // Editor installer supplies scene-independent prefab references.
        public void ConfigureArt(Transform yaw, Transform pitch, Transform muzzle,
            SurfaceToAirMissile projectile)
        {
            yawPivot = yaw;
            pitchPivot = pitch;
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
            if (yawPivot != null) yawPivot.localRotation = Quaternion.Euler(0f, 180f, 0f);
            if (pitchPivot != null) pitchPivot.localRotation = Quaternion.identity;
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
                missilePrefab == null || launchSocket == null || yawPivot == null || pitchPivot == null)
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

            float dt = Time.deltaTime;
            Vector3 toTarget = target.position - yawPivot.position;
            Vector3 flat = transform.InverseTransformDirection(toTarget);
            flat.y = 0f;
            if (flat.sqrMagnitude < .0001f) return;
            float yaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
            float nextYaw = Mathf.MoveTowardsAngle(yawPivot.localEulerAngles.y, yaw, yawSpeed * dt);
            yawPivot.localRotation = Quaternion.Euler(0f, nextYaw, 0f);
            Vector3 local = yawPivot.InverseTransformPoint(target.position);
            float horizontal = new Vector2(local.x, local.z).magnitude;
            float desiredPitch = Mathf.Clamp(Mathf.Atan2(local.y - pitchPivot.localPosition.y,
                horizontal) * Mathf.Rad2Deg, -5f, 75f);
            float currentPitch = Mathf.DeltaAngle(0f, pitchPivot.localEulerAngles.x);
            pitchPivot.localRotation = Quaternion.Euler(
                Mathf.MoveTowardsAngle(currentPitch, -desiredPitch, pitchSpeed * dt), 0f, 0f);

            if (Time.time < nextLaunchAt)
            {
                lockProgress = 0f;
                return;
            }
            float angle = Vector3.Angle(launchSocket.forward, target.position - launchSocket.position);
            if (angle > lockTolerance)
            {
                lockProgress = 0f;
                return;
            }
            lockProgress += dt;
            WarningBeep(lockProgress >= lockSeconds * .65f ? .22f : .38f);
            if (lockProgress < lockSeconds) return;
            lockProgress = 0f;
            nextLaunchAt = Time.time + reloadSeconds;
            missile = Instantiate(missilePrefab, launchSocket.position, launchSocket.rotation);
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
