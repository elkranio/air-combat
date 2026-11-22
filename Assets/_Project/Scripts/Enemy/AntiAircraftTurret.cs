using UnityEngine;

namespace AirCombat
{
    // AA1 stationary target. Real yaw/pitch servos track a continuously refreshed
    // ballistic lead point; the gun fires along the CURRENT barrel orientation.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(180)]
    public sealed class AntiAircraftTurret : DamageableTarget
    {
        [Header("Tracking")]
        [SerializeField, Min(1f)] private float detectionRange = 350f;
        [SerializeField, Min(1f)] private float yawSpeed = 85f;
        [SerializeField, Min(1f)] private float pitchSpeed = 55f;
        [SerializeField, Range(1f, 30f)] private float firingTolerance = 9f;
        [SerializeField, Range(0f, 85f)] private float maxElevation = 70f;

        [Header("Burst")]
        [SerializeField, Range(1, 40)] private int roundsPerBurst = 18;
        [SerializeField, Min(1f)] private float roundsPerSecond = 18f;
        [SerializeField, Min(0f)] private float pauseBetweenBursts = 2.5f;
        [SerializeField, Min(1f)] private float muzzleSpeed = 240f;
        [SerializeField, Min(1f)] private float projectileRange = 470f;
        [SerializeField, Min(0f)] private float gravity = 12f;
        [SerializeField, Range(0f, 5f)] private float spreadDegrees = 1.0f;

        [Header("Durability")]
        [SerializeField, Min(1)] private int maxHp = 6;
        [SerializeField] private Transform yawPivot;
        [SerializeField] private Transform pitchPivot;
        [SerializeField] private Transform leftMuzzle;
        [SerializeField] private Transform rightMuzzle;
        [SerializeField] private Material tracerMaterial;
        [SerializeField] private Material sparkMaterial;

        private Transform target;
        private PlayerHitPoints targetHp;
        private Vector3 previousTargetPosition;
        private Vector3 targetVelocity;
        private bool hasTargetSample;
        private float nextShotAt;
        private float nextBurstAt;
        private int shotsRemaining;
        private int hp;

        public int FiredCount { get; private set; }
        public int HitCount { get; private set; }
        public int Hp => hp;
        public int MaxHp => maxHp;
        public bool IsDown => hp <= 0;
        public override CombatTargetDomain Domain => CombatTargetDomain.Ground;

        // Used by the one-time AA1 prefab authoring tool; prefab has no player link.
        public void ConfigureArt(Transform yaw, Transform elevation, Transform left, Transform right,
            Material tracer, Material impactSpark)
        {
            yawPivot = yaw;
            pitchPivot = elevation;
            leftMuzzle = left;
            rightMuzzle = right;
            tracerMaterial = tracer;
            sparkMaterial = impactSpark;
        }

        public void BindTarget(Transform aircraft, PlayerHitPoints hitPoints)
        {
            target = aircraft;
            targetHp = hitPoints;
            ResetEncounter();
        }

        public void ResetEncounter()
        {
            hp = maxHp;
            shotsRemaining = FiredCount = HitCount = 0;
            nextShotAt = nextBurstAt = Time.time;
            targetVelocity = Vector3.zero;
            hasTargetSample = false;
            if (yawPivot != null) yawPivot.localRotation = Quaternion.Euler(0f, 180f, 0f);
            if (pitchPivot != null) pitchPivot.localRotation = Quaternion.identity;
        }

        private void Awake() { hp = maxHp; }

        private void LateUpdate()
        {
            if (target == null || targetHp == null || !targetHp.IsAlive || IsDown ||
                yawPivot == null || pitchPivot == null || leftMuzzle == null || rightMuzzle == null)
            {
                shotsRemaining = 0;
                return;
            }

            Vector3 aircraftPosition = target.position;
            float dt = Time.deltaTime;
            if (hasTargetSample && dt > 0f)
            {
                Vector3 measuredVelocity = (aircraftPosition - previousTargetPosition) / dt;
                // Reset by R or a scene teleport should not fool ballistic lead.
                if (measuredVelocity.sqrMagnitude > 160f * 160f)
                    targetVelocity = Vector3.zero;
                else
                    targetVelocity = Vector3.Lerp(targetVelocity, measuredVelocity,
                        1f - Mathf.Exp(-8f * dt));
            }
            previousTargetPosition = aircraftPosition;
            hasTargetSample = true;

            if ((aircraftPosition - transform.position).sqrMagnitude > detectionRange * detectionRange)
            {
                shotsRemaining = 0;
                return;
            }

            Vector3 lead = PredictImpactTarget(leftMuzzle.position, aircraftPosition);
            Vector3 toLead = lead - yawPivot.position;
            Vector3 flat = transform.InverseTransformDirection(toLead);
            flat.y = 0f;
            if (flat.sqrMagnitude <= 0.0001f) return;

            float yaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
            float currentYaw = yawPivot.localEulerAngles.y;
            float nextYaw = Mathf.MoveTowardsAngle(currentYaw, yaw, yawSpeed * dt);
            yawPivot.localRotation = Quaternion.Euler(0f, nextYaw, 0f);

            Vector3 localLead = yawPivot.InverseTransformPoint(lead);
            float horizontal = new Vector2(localLead.x, localLead.z).magnitude;
            float elevation = Mathf.Atan2(localLead.y - pitchPivot.localPosition.y, horizontal) * Mathf.Rad2Deg;
            elevation = Mathf.Clamp(elevation, -10f, maxElevation);
            float currentPitch = Mathf.DeltaAngle(0f, pitchPivot.localEulerAngles.x);
            float nextPitch = Mathf.MoveTowardsAngle(currentPitch, -elevation, pitchSpeed * dt);
            pitchPivot.localRotation = Quaternion.Euler(nextPitch, 0f, 0f);

            // A running burst keeps tracking; it never locks onto a snapshot.
            if (shotsRemaining == 0)
            {
                if (Time.time < nextBurstAt ||
                    Vector3.Angle(leftMuzzle.forward, lead - leftMuzzle.position) > firingTolerance)
                    return;
                shotsRemaining = roundsPerBurst;
                nextShotAt = Time.time;
            }
            while (shotsRemaining > 0 && Time.time >= nextShotAt)
            {
                FireRound();
                shotsRemaining--;
                nextShotAt += 1f / roundsPerSecond;
                if (shotsRemaining == 0) nextBurstAt = Time.time + pauseBetweenBursts;
            }
        }

        private Vector3 PredictImpactTarget(Vector3 muzzle, Vector3 aircraftPosition)
        {
            // Constant-velocity intercept + gravity compensation; no hitscan.
            float flightTime = Vector3.Distance(muzzle, aircraftPosition) / muzzleSpeed;
            Vector3 result = aircraftPosition;
            for (int i = 0; i < 3; i++)
            {
                result = aircraftPosition + targetVelocity * flightTime +
                    Vector3.up * (.5f * gravity * flightTime * flightTime);
                flightTime = Mathf.Clamp(Vector3.Distance(muzzle, result) / muzzleSpeed,
                    0f, projectileRange / muzzleSpeed);
            }
            return result;
        }

        private void FireRound()
        {
            Transform muzzle = (FiredCount & 1) == 0 ? leftMuzzle : rightMuzzle;
            Vector3 direction = Quaternion.AngleAxis(Random.Range(-spreadDegrees, spreadDegrees), muzzle.up) *
                Quaternion.AngleAxis(Random.Range(-spreadDegrees, spreadDegrees), muzzle.right) * muzzle.forward;
            var round = new GameObject("AA tracer");
            round.transform.position = muzzle.position;
            round.AddComponent<AntiAircraftProjectile>().Launch(direction, muzzleSpeed, gravity,
                projectileRange, 11f, transform, tracerMaterial, sparkMaterial, OnHitAircraft);
            FiredCount++;
        }

        private void OnHitAircraft() { HitCount++; }

        public override bool ApplyDamage(Vector3 point, int damage)
        {
            if (IsDown || damage <= 0) return false;
            hp = Mathf.Max(0, hp - damage);
            if (IsDown) shotsRemaining = 0;
            return true;
        }
    }
}
