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
        private SamWarningAudio warningAudio;
        private float lockProgress;
        private float nextLaunchAt;
        private float nextBeepAt;
        private int launchBeepsRemaining;
        private float nextLaunchBeepAt;
        private int hp;
        private enum LaunchProfile { Legacy, Picket, Bastion }
        private LaunchProfile profile;
        private bool armed;
        private bool IsBastion => profile == LaunchProfile.Bastion;
        private bool UsesPicketTiming => profile != LaunchProfile.Legacy;
        private Transform bastionYaw, bastionRightSocket;
        private float bastionGap, secondShotAt = -1f;
        private PicketRailPresentation railPresentation;
        private Transform mobileRoot;
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
                if (!UsesPicketTiming && HasIncomingMissile) return "LAUNCHED / " + missile.PhaseLabel;
                if (missilePrefab == null || launchSocket == null) return "UNCONFIGURED";
                if (target == null || hitPoints == null || !hitPoints.IsAlive) return "NO TARGET";
                if (IsBastion && secondShotAt >= 0f) return "SALVO / SECOND PENDING";
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
        public bool IsRadarVisible => isActiveAndEnabled && !IsDown && (mobileRoot == null || armed);

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

        // The mobile root owns the only collider and damage receiver (PicketHealth).
        // This collider-free child only owns weapon timing, audio and missiles.
        internal bool BindMobilePicket(SurfaceToAirLauncher template, Transform vehicle,
            Transform aircraft, PlayerHitPoints health, AircraftRadar sensor)
        {
            if (template == null || !template.IsConfigured || vehicle == null ||
                aircraft == null || health == null || sensor == null) return false;
            var rail = vehicle.Find("LauncherYaw/RailPitch");
            var presentation = PicketRailPresentation.TryBindMobile(vehicle);
            if (rail == null || presentation == null) return false;
            mobileRoot = vehicle;
            launchSocket = rail;
            missilePrefab = template.missilePrefab;
            engagementRange = template.engagementRange;
            lockSeconds = template.lockSeconds;
            reloadSeconds = template.reloadSeconds;
            prelaunchWarningSeconds = template.prelaunchWarningSeconds;
            railPresentation = presentation;
            profile = LaunchProfile.Picket;
            armed = false;
            BindTarget(aircraft, health, sensor);
            return true;
        }

        // Bastion shares the Picket projectile and acquisition timing, but commits
        // two shots from the two visual pods before starting a common reload.
        internal bool BindBastion(SurfaceToAirLauncher template, Transform aircraft,
            PlayerHitPoints health, AircraftRadar sensor, float gap)
        {
            if (template == null || !template.IsConfigured || aircraft == null ||
                health == null || sensor == null) return false;
            var yaw = transform.Find("Visual/Yaw");
            var left = yaw != null ? yaw.Find("Left") : null;
            var right = yaw != null ? yaw.Find("Right") : null;
            if (left == null || right == null) return false;
            bastionYaw = yaw;
            launchSocket = BastionSocket(left, -3.58f);
            bastionRightSocket = BastionSocket(right, 3.58f);
            missilePrefab = template.missilePrefab;
            engagementRange = template.engagementRange;
            lockSeconds = template.lockSeconds;
            reloadSeconds = template.reloadSeconds;
            prelaunchWarningSeconds = template.prelaunchWarningSeconds;
            bastionGap = Mathf.Max(.1f, gap);
            profile = LaunchProfile.Bastion;
            armed = false;
            BindTarget(aircraft, health, sensor);
            return true;
        }

        private static Transform BastionSocket(Transform pod, float x)
        {
            // V3 artwork is baked at the root origin; this is the visible pod tip.
            var socket = pod.Find("LaunchSocket");
            if (socket == null)
            {
                socket = new GameObject("LaunchSocket").transform;
                socket.SetParent(pod, false);
            }
            socket.localPosition = new Vector3(x, 6.18f, 3.40f);
            socket.localRotation = Quaternion.Euler(-29f, 0f, 0f);
            return socket;
        }

        private void TrackBastion(Vector3 position)
        {
            if (bastionYaw == null) return;
            Vector3 local = bastionYaw.parent.InverseTransformPoint(position) - bastionYaw.localPosition;
            if (local.x * local.x + local.z * local.z < .001f) return;
            var desired = Quaternion.Euler(0f, Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, 0f);
            bastionYaw.localRotation = Quaternion.RotateTowards(bastionYaw.localRotation, desired, 180f * Time.deltaTime);
        }

        public void SetArmed(bool armed)
        {
            this.armed = armed;
            if (!armed)
            {
                secondShotAt = -1f;
                ClearAcquisition();
            }
        }

        private void ClearAcquisition()
        {
            lockProgress = 0f;
            if (UsesPicketTiming && prelaunchWarningStarted >= 0f)
            {
                prelaunchWarningStarted = -1f;
                launchBeepsRemaining = 0;
                warningAudio?.Stop();
            }
        }

        public void BindTarget(Transform aircraft, PlayerHitPoints health, AircraftRadar sensor)
        {
            target = aircraft;
            hitPoints = health;
            radar = sensor;
            if (warningAudio == null || !warningAudio.HasSource)
            {
                warningAudio?.Dispose();
                warningAudio = new SamWarningAudio(gameObject);
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
            warningAudio?.Stop();
            LaunchedCount = 0;
            secondShotAt = -1f;
            if (bastionYaw != null) bastionYaw.localRotation = Quaternion.identity;
            railPresentation?.Reset();
            LastFlightReport = "NO LAUNCH";
        }

        private void LateUpdate()
        {
            if (Time.deltaTime <= 0f || (IsBastion && MissionPause.IsTacticalMapPausedFor(gameObject.scene))) return;
            ownedMissiles.RemoveAll(shot => shot == null || !shot.IsFlying);
            if (UsesPicketTiming && !armed) { ClearAcquisition(); return; }
            if (UsesPicketTiming && !IsDown) railPresentation?.TickReload(Time.time);
            if (!UsesPicketTiming) PlayLaunchSignal();
            // Picket fires independently of previous shots; preserve the old lab rule.
            if (!UsesPicketTiming && HasIncomingMissile)
            {
                ClearAcquisition();
                return;
            }
            if (IsDown || target == null || hitPoints == null || !hitPoints.IsAlive ||
                missilePrefab == null || launchSocket == null)
            {
                secondShotAt = -1f;
                ClearAcquisition();
                return;
            }
            // Once the first shot is away, finish the salvo even if the player
            // exits the acquisition circle. Disarm/death still cancels it.
            if (IsBastion && secondShotAt >= 0f)
            {
                TrackBastion(target.position);
                if (Time.time >= secondShotAt)
                {
                    secondShotAt = -1f;
                    nextLaunchAt = Time.time + reloadSeconds;
                    FireShot(bastionRightSocket);
                }
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
            if (IsBastion) TrackBastion(target.position);

            // Picket reload is the minimum launch-to-launch interval. Acquisition
            // may overlap its tail, but neither launch nor warning can occur early.
            float acquisitionWindow = UsesPicketTiming ? Mathf.Max(lockSeconds, Mathf.Max(.5f, prelaunchWarningSeconds)) : 0f;
            if (Time.time < nextLaunchAt - acquisitionWindow)
            {
                ClearAcquisition();
                return;
            }
            lockProgress += Time.deltaTime;
            if (UsesPicketTiming)
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
            if (IsBastion) secondShotAt = Time.time + bastionGap;
            nextLaunchAt = Time.time + reloadSeconds + (IsBastion ? bastionGap : 0f);
            FireShot(launchSocket);
        }

        private void FireShot(Transform socket)
        {
            // Legacy/Picket retain their original ejection/rail presentation.
            Vector3 launchPosition = socket.position;
            missile = Instantiate(missilePrefab, launchPosition,
                Quaternion.LookRotation(Vector3.up, Vector3.forward));
            ownedMissiles.Add(missile);
            missile.Launch(target, hitPoints, radar, mobileRoot != null ? mobileRoot : transform, OnMissileFinished);
            if (IsBastion) missile.BeginAngledLaunch(socket.rotation);
            else railPresentation?.PresentLaunch(missile, Time.time, reloadSeconds);
            // Picket already played its three pre-launch beeps.
            if (!UsesPicketTiming)
            {
                warningAudio?.Stop();
                launchBeepsRemaining = 3;
                nextLaunchBeepAt = Time.time;
                PlayLaunchSignal();
            }
            LaunchedCount++;
            LastFlightReport = $"SPAWNED #{LaunchedCount} at Y={launchPosition.y:F1}";
            if (!UsesPicketTiming) Debug.Log($"SAM1: {LastFlightReport}; target={target.position}", this);
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
            if (warningAudio == null || !warningAudio.CanPlay || Time.time < nextBeepAt) return;
            warningAudio.Play();
            nextBeepAt = Time.time + interval;
        }

        private void PlayLaunchSignal()
        {
            if (launchBeepsRemaining <= 0 || warningAudio == null || !warningAudio.CanPlay ||
                Time.time < nextLaunchBeepAt) return;
            warningAudio.Play();
            launchBeepsRemaining--;
            nextLaunchBeepAt = Time.time + (UsesPicketTiming ? MissileWarningTiming.PulsePeriod : .15f);
        }

        public override bool ApplyDamage(Vector3 point, int damage)
        {
            if (IsDown || damage <= 0) return false;
            hp = Mathf.Max(0, hp - damage);
            if (IsDown)
            {
                secondShotAt = -1f;
                ClearAcquisition();
            }
            return true;
        }

        private void OnDestroy()
        {
            warningAudio?.Dispose();
            warningAudio = null;
        }
    }
}
