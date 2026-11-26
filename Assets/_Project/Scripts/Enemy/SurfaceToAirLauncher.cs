using System.Collections.Generic;
using UnityEngine;

namespace AirCombat
{
    // Reusable SAM unit. Binding chooses the launch profile; the missile owns flight.
    // Legacy single-shot and Picket overlapping-shot behavior are independent of mission identity.
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
        private readonly List<SurfaceToAirMissile> ownedMissiles = new List<SurfaceToAirMissile>();
        private AudioSource warningAudio;
        private AudioClip warningClip;
        private float lockProgress;
        private float nextLaunchAt;
        private float nextBeepAt;
        private int launchBeepsRemaining;
        private float nextLaunchBeepAt;
        private int hp;
        private enum LaunchProfile { Legacy, Picket }
        private LaunchProfile profile;
        private bool armed;
        private bool IsPicket => profile == LaunchProfile.Picket;
        private PicketRailPresentation railPresentation;
        private float prelaunchWarningStarted = -1f;
        [SerializeField, Min(.5f)] private float prelaunchWarningSeconds = .65f;

        public bool IsConfigured => launchSocket != null && missilePrefab != null;
        public float PrelaunchWarningStarted => armed && !IsDown ? prelaunchWarningStarted : -1f;
        public int Hp => hp;
        public int MaxHp => maxHp;
        public bool IsDown => hp <= 0;
        public int LaunchedCount { get; private set; }
        public string LastFlightReport { get; private set; } = "NO LAUNCH";
        public bool HasIncomingMissile
        {
            get
            {
                foreach (var shot in ownedMissiles)
                    if (shot != null && shot.IsFlying) return true;
                return false;
            }
        }
        public float LockProgress01 => Mathf.Clamp01(lockProgress / Mathf.Max(.1f, lockSeconds));
        public float ReloadLeft => Mathf.Max(0f, nextLaunchAt - Time.time);
        public string StatusLabel
        {
            get
            {
                if (IsDown) return "DESTROYED";
                if (!IsPicket && HasIncomingMissile) return "LAUNCHED / " + missile.PhaseLabel;
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

        // Any composition may opt into the Picket launch profile. Start disarmed.
        public void BindPicketTarget(Transform aircraft, PlayerHitPoints health, AircraftRadar sensor)
        {
            profile = LaunchProfile.Picket;
            armed = false;
            if (railPresentation == null) railPresentation = PicketRailPresentation.TryBind(transform);
            BindTarget(aircraft, health, sensor);
        }

        public void SetArmed(bool armed)
        {
            this.armed = armed;
            if (!armed) ClearAcquisition();
        }

        private void ClearAcquisition()
        {
            lockProgress = 0f;
            if (IsPicket && prelaunchWarningStarted >= 0f)
            {
                prelaunchWarningStarted = -1f;
                launchBeepsRemaining = 0;
                if (warningAudio != null) warningAudio.Stop();
            }
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

        public void CancelProjectiles()
        {
            // All overlapping shots belong to this launcher, not just the latest.
            // Disable immediately: Destroy alone is deferred until end of frame.
            foreach (var shot in ownedMissiles)
            {
                if (shot == null) continue;
                radar?.Unregister(shot);
                shot.gameObject.SetActive(false);
                Destroy(shot.gameObject);
            }
            ownedMissiles.Clear();
            missile = null;
        }

        public void ResetEncounter()
        {
            CancelProjectiles();
            hp = maxHp;
            lockProgress = 0f;
            prelaunchWarningStarted = -1f;
            nextLaunchAt = Time.time;
            nextBeepAt = 0f;
            launchBeepsRemaining = 0;
            nextLaunchBeepAt = 0f;
            if (warningAudio != null) warningAudio.Stop();
            LaunchedCount = 0;
            railPresentation?.Reset();
            LastFlightReport = "NO LAUNCH";
        }

        private void LateUpdate()
        {
            if (Time.deltaTime <= 0f) return;
            ownedMissiles.RemoveAll(shot => shot == null || !shot.IsFlying);
            if (IsPicket && !armed) { ClearAcquisition(); return; }
            if (IsPicket && !IsDown) railPresentation?.TickReload(Time.time);
            if (!IsPicket) PlayLaunchSignal();
            // Picket fires independently of previous shots; preserve the old lab rule.
            if (!IsPicket && HasIncomingMissile)
            {
                ClearAcquisition();
                return;
            }
            if (IsDown || target == null || hitPoints == null || !hitPoints.IsAlive ||
                missilePrefab == null || launchSocket == null)
            {
                ClearAcquisition();
                return;
            }
            Vector3 delta = target.position - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude > engagementRange * engagementRange)
            {
                ClearAcquisition();
                return;
            }

            // Cosmetic tracking only: the accepted acquisition/reload clock is unchanged.
            railPresentation?.Track(target.position, Time.deltaTime);

            // Picket reload is the minimum launch-to-launch interval. Acquisition
            // may overlap its tail, but neither launch nor warning can occur early.
            float acquisitionWindow = IsPicket ? Mathf.Max(lockSeconds, Mathf.Max(.5f, prelaunchWarningSeconds)) : 0f;
            if (Time.time < nextLaunchAt - acquisitionWindow)
            {
                ClearAcquisition();
                return;
            }
            lockProgress += Time.deltaTime;
            if (IsPicket)
            {
                // Silent acquisition, then a cancelable pre-launch cue.
                float lead = Mathf.Max(.5f, prelaunchWarningSeconds);
                if (prelaunchWarningStarted < 0f && Time.time >= nextLaunchAt - lead &&
                    lockProgress >= Mathf.Max(0f, lockSeconds - lead))
                {
                    prelaunchWarningStarted = Time.time;
                    launchBeepsRemaining = 3;
                    nextLaunchBeepAt = Time.time;
                }
                if (prelaunchWarningStarted >= 0f) PlayLaunchSignal();
                if (Time.time < nextLaunchAt || lockProgress < lockSeconds || prelaunchWarningStarted < 0f ||
                    Time.time - prelaunchWarningStarted < lead || launchBeepsRemaining > 0) return;
            }
            else if (lockProgress < lockSeconds)
            {
                WarningBeep(lockProgress >= lockSeconds * .65f ? .22f : .38f);
                return;
            }
            lockProgress = 0f;
            prelaunchWarningStarted = -1f;
            nextLaunchAt = Time.time + reloadSeconds;
            // Force +Y ejection even when upgrading a legacy angled prefab.
            // The missile independently handles the pitch-over and pursuit.
            Vector3 launchPosition = launchSocket.position;
            missile = Instantiate(missilePrefab, launchPosition,
                Quaternion.LookRotation(Vector3.up, Vector3.forward));
            ownedMissiles.Add(missile);
            missile.Launch(target, hitPoints, radar, transform, OnMissileFinished);
            railPresentation?.PresentLaunch(missile, Time.time, reloadSeconds);
            // Picket already played its three pre-launch beeps.
            if (!IsPicket)
            {
                if (warningAudio != null) warningAudio.Stop();
                launchBeepsRemaining = 3;
                nextLaunchBeepAt = Time.time;
                PlayLaunchSignal();
            }
            LaunchedCount++;
            LastFlightReport = $"SPAWNED #{LaunchedCount} at Y={launchPosition.y:F1}";
            if (!IsPicket) Debug.Log($"SAM1: {LastFlightReport}; target={target.position}", this);
        }

        private void OnMissileFinished(string report)
        {
            LastFlightReport = report;
            // An immediate missile disappearance is invisible at gameplay zoom;
            // report the actual reason instead of guessing from the warning beep.
            if (report.StartsWith("BLOCKED")) Debug.LogWarning("SAM1: " + report, this);
        }

        private void WarningBeep(float interval)
        {
            if (warningAudio == null || warningClip == null || Time.time < nextBeepAt) return;
            warningAudio.PlayOneShot(warningClip);
            nextBeepAt = Time.time + interval;
        }

        private void PlayLaunchSignal()
        {
            if (launchBeepsRemaining <= 0 || warningAudio == null || warningClip == null ||
                Time.time < nextLaunchBeepAt) return;
            warningAudio.PlayOneShot(warningClip);
            launchBeepsRemaining--;
            nextLaunchBeepAt = Time.time + (IsPicket ? MissileWarningTiming.PulsePeriod : .15f);
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
            if (IsDown) ClearAcquisition();
            return true;
        }

        private void OnDestroy()
        {
            if (warningClip != null) Destroy(warningClip);
        }
    }
}
