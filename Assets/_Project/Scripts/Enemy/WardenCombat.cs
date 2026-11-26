using UnityEngine;
using UnityEngine.Rendering;

namespace AirCombat
{
    // Weapon/health composition only. The encounter supplies the target; the
    // movement controller remains independent (preview patrol for this atom).
    [DisallowMultipleComponent]
    public sealed class WardenCombat : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform yawLeft, pitchLeft, muzzleLeft;
        [SerializeField] private Transform yawRight, pitchRight, muzzleRight;
        [SerializeField] private Material tracerCore, tracerGlow;
        [SerializeField] private AudioClip shotClip;
        [Header("Arcade firing rhythm")]
        [SerializeField, Min(1f)] private float fireRange = 135f;
        [SerializeField, Min(1f)] private float projectileSpeed = 185f;
        [SerializeField, Min(1f)] private float projectileRange = 220f;
        [SerializeField, Min(.1f)] private float roundsPerSecond = 11f;
        [SerializeField, Min(.1f)] private float burstSeconds = 1.7f;
        [SerializeField, Min(.1f)] private float pauseSeconds = 1.15f;
        [SerializeField, Min(0f)] private float leadSeconds = .10f;
        [SerializeField, Min(0f)] private float spreadDegrees = 5.5f;
        [SerializeField, Min(1f)] private float turretDegreesPerSecond = 250f;
        [Header("Hostile tracer / world units")]
        [SerializeField, Min(.05f)] private float tracerWidth = .34f;
        [SerializeField, Min(.05f)] private float glowWidth = .95f;
        [SerializeField, Min(.1f)] private float tracerLength = 5.5f;
        [SerializeField, Range(0f, 1f)] private float shotVolume = .18f;

        private PlayerHitPoints target;
        private FlightController targetFlight;
        private WardenPatrolPreview patrol;
        private CombatHitFeedback feedback;
        private AudioSource audioSource;
        private Transform[] yaw, pitch, muzzle;
        private LineRenderer[] flashes;
        private readonly float[] flashUntil = new float[2];
        private bool bound, firing, stopped;
        private float nextShot, phaseUntil;
        private int nextBarrel;
        public PlayerHitPoints Health { get; private set; }
        public RadarAirContact Contact { get; private set; }
        public int FiredCount { get; private set; }
        public int HitCount { get; private set; }
        public bool Alive => Health != null && Health.IsAlive;
        public string DebugStatus => $"HP {Health?.Hp ?? 0} | AC {HitCount}/{FiredCount} | {(firing ? "BURST" : "WAIT")}";

        public bool Bind(PlayerHitPoints player, FlightController flight, CombatHitFeedback fx)
        {
            if (bound) return true;
            Health = GetComponent<PlayerHitPoints>();
            Contact = GetComponent<RadarAirContact>();
            patrol = GetComponent<WardenPatrolPreview>();
            var team = GetComponent<AircraftTeam>();
            yaw = new[] { yawLeft, yawRight };
            pitch = new[] { pitchLeft, pitchRight };
            muzzle = new[] { muzzleLeft, muzzleRight };
            if (Health == null || Contact == null || team == null || patrol == null ||
                visualRoot == null || tracerCore == null || tracerGlow == null || player == null ||
                yawLeft == null || yawRight == null || pitchLeft == null || pitchRight == null ||
                muzzleLeft == null || muzzleRight == null)
            {
                Debug.LogError("ED01 Warden: incomplete combat prefab.", this);
                return false;
            }
            target = player;
            targetFlight = flight;
            feedback = fx;
            team.SetSide(AircraftSide.Hostile);
            Health.InstallHitbox();
            var hitbox = GetComponent<BoxCollider>();
            hitbox.center = new Vector3(0f, .2f, .2f);
            hitbox.size = new Vector3(16.6f, 3.2f, 12.4f);
            feedback?.Track(Health, false, visualRoot);
            flashes = new LineRenderer[2];
            for (int i = 0; i < 2; i++)
            {
                flashes[i] = MakeLine(muzzle[i], "Warden muzzle flash", tracerGlow, .9f,
                    new Color(1f, .86f, .42f, .95f), new Color(1f, .18f, .035f, 0f));
                flashes[i].useWorldSpace = false;
                flashes[i].SetPosition(0, Vector3.zero);
                flashes[i].SetPosition(1, Vector3.forward * 1.8f);
                flashes[i].enabled = false;
            }
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            audioSource.dopplerLevel = 0f;
            bound = true;
            return true;
        }

        public void ResetCombat(float now)
        {
            if (!bound) return;
            gameObject.SetActive(true);
            Health.ResetHealth();
            patrol.enabled = true;
            patrol.RestartPreview();
            for (int i = 0; i < 2; i++)
            {
                yaw[i].localRotation = pitch[i].localRotation = Quaternion.identity;
                flashUntil[i] = 0f;
                flashes[i].enabled = false;
            }
            audioSource.Stop();
            nextBarrel = FiredCount = HitCount = 0;
            firing = stopped = false;
            nextShot = phaseUntil = now + .45f;
        }

        public void StopCombat()
        {
            stopped = true;
            if (patrol != null) patrol.enabled = false;
            firing = false;
            if (flashes != null)
                foreach (var flash in flashes) if (flash != null) flash.enabled = false;
            if (audioSource != null) audioSource.Stop();
        }

        public void HideDestroyed()
        {
            StopCombat();
            feedback?.SpawnWreck(Health, patrol.Speed);
            gameObject.SetActive(false);
        }

        public void SetAltitude(float altitude) => patrol.SetPreviewAltitude(altitude);

        // Sample after flight motion; only actual emitted projectiles can hit.
        public void TickFire(float now, float dt)
        {
            if (!bound || stopped || !Alive || target == null || !target.IsAlive || dt <= 0f) return;
            for (int i = 0; i < 2; i++) flashes[i].enabled = now < flashUntil[i];
            Vector3 offset = target.transform.position - transform.position;
            float distance = offset.magnitude;
            bool inRange = distance <= fireRange;
            float targetSpeed = targetFlight != null ? targetFlight.CurrentSpeed : 0f;
            Vector3 aim = target.transform.position + target.transform.forward * (targetSpeed * leadSeconds);
            for (int i = 0; i < 2; i++) AimTurret(i, aim, dt, inRange);
            if (!inRange)
            {
                if (firing) phaseUntil = now + pauseSeconds;
                firing = false;
                return;
            }
            if (now >= phaseUntil)
            {
                firing = !firing;
                phaseUntil = now + (firing ? burstSeconds : pauseSeconds);
                if (firing) nextShot = now;
            }
            if (!firing || now < nextShot) return;
            int barrel = nextBarrel;
            Vector3 wanted = (aim - muzzle[barrel].position).normalized;
            // Don't fire through a visibly unaligned turret while it slews.
            if (Vector3.Angle(muzzle[barrel].forward, wanted) > 12f) return;
            nextBarrel = 1 - barrel;
            nextShot = now + 1f / Mathf.Max(.1f, roundsPerSecond);
            Emit(barrel, now, distance);
        }

        private void AimTurret(int i, Vector3 aim, float dt, bool tracking)
        {
            Quaternion yawGoal = Quaternion.identity;
            Quaternion pitchGoal = Quaternion.identity;
            if (tracking)
            {
                Vector3 local = yaw[i].parent.InverseTransformDirection(aim - yaw[i].position);
                yawGoal = Quaternion.Euler(0f, Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, 0f);
            }
            yaw[i].localRotation = Quaternion.RotateTowards(yaw[i].localRotation, yawGoal, turretDegreesPerSecond * dt);
            if (tracking)
            {
                Vector3 local = yaw[i].InverseTransformDirection(aim - pitch[i].position);
                float angle = -Mathf.Atan2(local.y, Mathf.Max(.01f, Mathf.Abs(local.z))) * Mathf.Rad2Deg;
                pitchGoal = Quaternion.Euler(Mathf.Clamp(angle, -18f, 18f), 0f, 0f);
            }
            pitch[i].localRotation = Quaternion.RotateTowards(pitch[i].localRotation, pitchGoal, turretDegreesPerSecond * dt);
        }

        private void Emit(int i, float now, float distance)
        {
            Vector3 direction = Quaternion.AngleAxis(Random.Range(-spreadDegrees, spreadDegrees), Vector3.up) * muzzle[i].forward;
            var round = new GameObject("Warden Autocannon Round");
            round.transform.position = muzzle[i].position;
            var core = MakeLine(round.transform, "Tracer core", tracerCore, tracerWidth,
                new Color(1f, .12f, .035f, .9f), new Color(1f, .83f, .43f, 1f));
            var glow = MakeLine(round.transform, "Tracer glow", tracerGlow, glowWidth,
                new Color(1f, .035f, .01f, .17f), new Color(1f, .16f, .03f, .60f));
            var shot = round.AddComponent<BallisticProjectile>();
            shot.Configure(direction, projectileSpeed, projectileRange, tracerLength, core, glow,
                () => HitCount++, 1);
            shot.BindOwner(transform);
            FiredCount++;
            flashUntil[i] = now + .045f;
            flashes[i].enabled = true;
            if (shotClip != null) audioSource.PlayOneShot(shotClip,
                shotVolume * Mathf.Lerp(1f, .35f, Mathf.Clamp01(distance / fireRange)));
        }

        private static LineRenderer MakeLine(Transform parent, string name, Material material,
            float width, Color tail, Color head)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            var line = child.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.widthMultiplier = width;
            line.startColor = tail;
            line.endColor = head;
            line.numCapVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }
    }
}
