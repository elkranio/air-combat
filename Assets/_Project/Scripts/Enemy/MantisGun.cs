using System.Collections.Generic;
using UnityEngine;

namespace AirCombat
{
    // Mobile arcade AA: quick servos between bursts, a committed world-space
    // firing corridor during each burst. Independent of the patrol motor.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(180)]
    public sealed class MantisGun : MonoBehaviour, IRadarContact
    {
        public enum AttackMode { Flak, Tracers, VerticalUnguided }
        [Header("Attack mode (AA-01e1)")]
        [SerializeField] private AttackMode attackMode = AttackMode.VerticalUnguided;
        [Header("Vertical unguided / cold launch")]
        [SerializeField, Min(.1f)] private float verticalEjectionSeconds = .48f;
        [SerializeField, Min(.1f)] private float verticalTurnSeconds = .32f;
        [SerializeField, Min(.05f)] private float verticalIgnitionSeconds = .22f;
        [SerializeField, Min(1f)] private float verticalCruiseSpeed = 240f;
        [SerializeField, Min(1f)] private float verticalRange = 310f;
        [SerializeField, Min(.1f)] private float verticalPause = 2.5f;
        [SerializeField, Min(1)] private int verticalDamage = 3;
        [Header("Flak experiment")]
        [SerializeField, Min(.02f)] private float flakShotInterval = .15f;
        [SerializeField, Min(.05f)] private float flakTravelSeconds = .25f;
        [SerializeField, Min(.1f)] private float flakFuseSeconds = .5f;
        [SerializeField, Min(1f)] private float flakRadius = 12f;
        [SerializeField, Min(0f)] private float flakCenterSeparation = 18f;
        [SerializeField, Min(.1f)] private float flakPause = 2.5f;
        [SerializeField, Min(1)] private int flakDamage = 2;
        [SerializeField, Range(0f, 1f)] private float flakSoundVolume = .45f;
        [Header("Tracking")]
        [SerializeField] private Transform turretYaw, gunPitch, muzzleL, muzzleR;
        [SerializeField, Min(1f)] private float detectionRange = 210f;
        [SerializeField, Min(1f)] private float yawSpeed = 240f;
        [SerializeField, Min(1f)] private float pitchSpeed = 180f;
        [SerializeField, Range(1f, 30f)] private float firingTolerance = 5f;
        [SerializeField, Range(1f, 89f)] private float maxElevation = 85f;
        [Header("Paired bursts")]
        [SerializeField, Range(1, 20)] private int pairsPerBurst = 7;
        [SerializeField, Min(.02f)] private float pairInterval = .085f;
        [SerializeField, Min(.1f)] private float pauseMin = 1.25f;
        [SerializeField, Min(.1f)] private float pauseMax = 1.8f;
        [SerializeField, Range(0f, 10f)] private float burstDrift = 3f;
        [SerializeField, Range(0f, 5f)] private float roundSpread = .65f;
        [Header("Arcade projectile")]
        [SerializeField, Min(1f)] private float projectileSpeed = 190f;
        [SerializeField, Min(1f)] private float projectileRange = 340f;
        [SerializeField, Min(1f)] private float tracerLength = 13f;
        [SerializeField, Min(1f)] private float dangerHeight = 13f;
        [SerializeField, Min(3f)] private float airPuffAbove = 5f;
        [SerializeField, Min(1)] private int damage = 1;

        private PlayerHitPoints target;
        private AircraftRadar radar;
        private Material tracer;
        private readonly List<MantisRound> rounds = new List<MantisRound>();
        private readonly List<MantisFlakCharge> flakCharges = new List<MantisFlakCharge>();
        private readonly List<MantisVerticalRocket> verticalRockets = new List<MantisVerticalRocket>();
        private GameObject verticalPod;
        private Transform verticalSocket;
        private Renderer[] barrelRenderers;
        private bool[] barrelVisibility;
        private Material rocketHull;
        private Vector3 flakSecondPoint;
        private AttackMode activeMode;
        private AudioClip flakBlastClip, flakShotClip;
        private Vector3 previousPosition, targetVelocity, burstDirection;
        private bool sampled, bound;
        private int pending;
        private float nextPair, nextBurst, driftYaw, driftPitch;
        public int FiredCount { get; private set; }
        public int HitCount { get; private set; }
        public bool IsBursting => pending > 0;
        public RadarContactType RadarKind => RadarContactType.GroundDefense;
        public Vector3 RadarPosition => transform.position;
        public float RadarThreatRadius => detectionRange;
        public bool IsRadarVisible => bound && isActiveAndEnabled;

        public void ConfigureArt(Transform yaw, Transform pitch, Transform left, Transform right)
        { turretYaw = yaw; gunPitch = pitch; muzzleL = left; muzzleR = right; }

        public void Bind(PlayerHitPoints player, AircraftRadar sensor)
        {
            Unbind();
            if (player == null || sensor == null || turretYaw == null || gunPitch == null || muzzleL == null || muzzleR == null)
            { Debug.LogError("Mantis: missing player, radar or gun sockets.", this); return; }
            var shader = Shader.Find("AirCombat/SandboxEffect");
            if (shader == null) { Debug.LogError("Mantis: tracer shader missing.", this); return; }
            if (tracer == null) tracer = new Material(shader) { name = "Mantis tracer / runtime" };
            target = player;
            radar = sensor;
            bound = true;
            if (isActiveAndEnabled) radar.Register(this);
            ResetCombat();
        }

        public void ResetCombat()
        {
            ClearRounds();
            pending = 0;
            activeMode = attackMode;
            sampled = false;
            targetVelocity = Vector3.zero;
            FiredCount = HitCount = 0;
            nextBurst = Time.time + .5f;
            if (turretYaw != null) turretYaw.localRotation = Quaternion.identity;
            if (gunPitch != null) gunPitch.localRotation = Quaternion.Euler(-18f, 0f, 0f);
            SetVerticalVisual(attackMode == AttackMode.VerticalUnguided);
        }

        public void Unbind()
        {
            if (radar != null) radar.Unregister(this);
            ClearRounds();
            pending = 0;
            bound = false;
            SetVerticalVisual(false);
            target = null;
            radar = null;
        }

        private void OnEnable() { if (bound && radar != null) { radar.Register(this); ResetCombat(); } }
        private void OnDisable() { if (radar != null) radar.Unregister(this); pending = 0; ClearRounds(); SetVerticalVisual(false); }
        private void OnDestroy()
        {
            Unbind();
            if (tracer != null) Destroy(tracer);
            if (verticalPod != null) Destroy(verticalPod);
            if (rocketHull != null) Destroy(rocketHull);
            if (flakBlastClip != null) Destroy(flakBlastClip);
            if (flakShotClip != null) Destroy(flakShotClip);
        }

        private void ClearRounds()
        {
            foreach (var round in rounds)
                if (round != null) { round.gameObject.SetActive(false); Destroy(round.gameObject); }
            rounds.Clear();
            foreach (var charge in flakCharges)
                if (charge != null) { charge.gameObject.SetActive(false); Destroy(charge.gameObject); }
            flakCharges.Clear();
            foreach (var rocket in verticalRockets)
                if (rocket != null) { rocket.gameObject.SetActive(false); Destroy(rocket.gameObject); }
            verticalRockets.Clear();
        }

        private void LateUpdate()
        {
            if (!bound || target == null) return;
            rounds.RemoveAll(r => r == null);
            flakCharges.RemoveAll(r => r == null);
            verticalRockets.RemoveAll(r => r == null);
            if (activeMode != attackMode) ResetCombat();
            if (!target.IsAlive) { pending = 0; return; }
            float dt = Time.deltaTime;
            Vector3 pos = target.transform.position;
            if (sampled && dt > 0f)
            {
                Vector3 measured = (pos - previousPosition) / dt;
                targetVelocity = measured.sqrMagnitude > 250f * 250f ? Vector3.zero :
                    Vector3.Lerp(targetVelocity, measured, 1f - Mathf.Exp(-12f * dt));
            }
            previousPosition = pos;
            sampled = true;
            Vector3 horizontal = pos - transform.position;
            horizontal.y = 0f;
            bool inRange = horizontal.sqrMagnitude <= detectionRange * detectionRange;
            if (!inRange && pending == 0) return;

            if (attackMode == AttackMode.VerticalUnguided)
            {
                TickVertical(inRange);
                return;
            }
            Vector3 origin = (muzzleL.position + muzzleR.position) * .5f;
            if (attackMode == AttackMode.Flak)
            {
                TickFlak(pos, origin, inRange, dt);
                return;
            }
            // Short constant-velocity arcade prediction; never update a round after launch.
            Vector3 delta = pos - origin;
            float t = delta.magnitude / projectileSpeed;
            for (int i = 0; i < 4; i++)
                t = Mathf.Min(projectileRange / projectileSpeed, (delta + targetVelocity * t).magnitude / projectileSpeed);
            Vector3 aim = delta + targetVelocity * t;
            if (aim.sqrMagnitude < .001f) return;
            aim.Normalize();
            bool continuing = pending > 0;
            if (continuing)
            {
                float phase = (pairsPerBurst - pending) / (float)Mathf.Max(1, pairsPerBurst - 1);
                Vector3 right = Vector3.Cross(Vector3.up, burstDirection).normalized;
                aim = Quaternion.AngleAxis(driftYaw * phase, Vector3.up) *
                    (Quaternion.AngleAxis(driftPitch * phase, right) * burstDirection);
            }
            AimServos(aim, dt);
            if (!continuing)
            {
                if (!inRange || Time.time < nextBurst ||
                    Vector3.Angle(muzzleL.forward, aim) > firingTolerance ||
                    Vector3.Angle(muzzleR.forward, aim) > firingTolerance) return;
                // Snapshot the actual barrel axis, not the live target direction.
                burstDirection = (muzzleL.forward + muzzleR.forward).normalized;
                driftYaw = Random.Range(-burstDrift, burstDrift);
                driftPitch = Random.Range(-burstDrift * .35f, burstDrift * .35f);
                pending = pairsPerBurst;
                nextPair = Time.time;
            }
            if (Time.time < nextPair) return;
            // No catch-up cluster after a frame hitch. A rotating chassis can
            // briefly delay a pair rather than fling it far outside its corridor.
            if (Vector3.Angle(muzzleL.forward, burstDirection) > burstDrift + firingTolerance + 3f ||
                Vector3.Angle(muzzleR.forward, burstDirection) > burstDrift + firingTolerance + 3f)
            { pending = 0; SchedulePause(); return; }
            Fire(muzzleL);
            Fire(muzzleR);
            pending--;
            nextPair = Time.time + pairInterval;
            if (pending == 0) SchedulePause();
        }

        // Runtime-only placeholder pod; keep the authored Mantis prefab and both
        // earlier attack modes intact. The actual launch sockets sit on the tubes.
        private void SetVerticalVisual(bool visible)
        {
            if (gunPitch == null || turretYaw == null) return;
            if (visible && verticalPod == null)
            {
                barrelRenderers = gunPitch.GetComponentsInChildren<Renderer>(true);
                barrelVisibility = new bool[barrelRenderers.Length];
                for (int i = 0; i < barrelRenderers.Length; i++) barrelVisibility[i] = barrelRenderers[i].enabled;
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) { Debug.LogError("Mantis: URP Lit missing.", this); return; }
                rocketHull = new Material(shader) { name = "Mantis / vertical missile hull" };
                rocketHull.SetColor("_BaseColor", new Color(.52f,.59f,.57f));
                rocketHull.SetFloat("_Smoothness", .08f);
                verticalPod = new GameObject("AA-01e / temporary vertical pod");
                verticalPod.transform.SetParent(turretYaw, false);
                verticalSocket = BuildTube(0f);
            }
            if (verticalPod != null) verticalPod.SetActive(visible);
            if (verticalPod != null && barrelRenderers != null)
                for (int i = 0; i < barrelRenderers.Length; i++)
                    if (barrelRenderers[i] != null) barrelRenderers[i].enabled = !visible && barrelVisibility[i];
        }

        private Transform BuildTube(float x)
        {
            var tube = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tube.name = "Launch canister";
            tube.transform.SetParent(verticalPod.transform, false);
            tube.transform.localPosition = new Vector3(x, 1.5f, 1.0f);
            tube.transform.localScale = new Vector3(1.65f, 1.55f, 1.65f);
            var collider = tube.GetComponent<Collider>();
            collider.enabled = false; Destroy(collider);
            tube.GetComponent<MeshRenderer>().sharedMaterial = rocketHull;
            var socket = new GameObject("Vertical launch socket").transform;
            socket.SetParent(verticalPod.transform, false);
            socket.localPosition = new Vector3(x, 3.05f, 1.0f);
            return socket;
        }

        private void TickVertical(bool inRange)
        {
            if (verticalSocket == null || !inRange || Time.time < nextBurst) return;
            FireVertical(verticalSocket);
            nextBurst = Time.time + verticalPause;
        }

        private void FireVertical(Transform socket)
        {
            var go = new GameObject("Mantis / cold-launch unguided rocket");
            go.transform.position = socket.position;
            var rocket = go.AddComponent<MantisVerticalRocket>();
            rocket.Launch(transform, target, verticalEjectionSeconds, verticalTurnSeconds,
                verticalIgnitionSeconds, verticalCruiseSpeed, verticalRange,
                verticalDamage, rocketHull, tracer, () => HitCount++);
            verticalRockets.Add(rocket);
            FiredCount++;
        }

        // A paired world-space snapshot: the second charge never retargets,
        // even if the player turns or the chassis moves between shots.
        private void TickFlak(Vector3 pos, Vector3 origin, bool inRange, float dt)
        {
            if (pending > 0)
            {
                Vector3 secondAim = flakSecondPoint - origin;
                if (secondAim.sqrMagnitude > .001f) AimServos(secondAim.normalized, dt);
                if (Time.time < nextPair) return;
                FireFlak(muzzleR, flakSecondPoint);
                pending = 0;
                nextBurst = Time.time + flakPause;
                return;
            }
            // Predict only once for the pair, at the player's current flight height.
            Vector3 travel = Vector3.ProjectOnPlane(targetVelocity, Vector3.up);
            Vector3 course = travel.sqrMagnitude > 1f ? travel.normalized :
                Vector3.ProjectOnPlane(target.transform.forward, Vector3.up).normalized;
            if (course.sqrMagnitude < .001f) course = Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, course);
            Vector3 center = pos + travel * (flakTravelSeconds + flakFuseSeconds);
            Vector3 first = center - side * (flakCenterSeparation * .5f);
            Vector3 second = center + side * (flakCenterSeparation * .5f);
            Vector3 aim = first - origin;
            if (aim.sqrMagnitude < .001f) return;
            AimServos(aim.normalized, dt);
            if (!inRange || Time.time < nextBurst ||
                Vector3.Angle(muzzleL.forward, first - muzzleL.position) > firingTolerance) return;
            flakSecondPoint = second;
            FireFlak(muzzleL, first);
            pending = 1;
            nextPair = Time.time + flakShotInterval;
        }

        private void FireFlak(Transform muzzle, Vector3 point)
        {
            if (flakBlastClip == null) flakBlastClip = MantisFlakCharge.MakeSound(false);
            if (flakShotClip == null) flakShotClip = MantisFlakCharge.MakeSound(true);
            var go = new GameObject("Mantis / Flak charge");
            go.transform.position = muzzle.position;
            var charge = go.AddComponent<MantisFlakCharge>();
            charge.Launch(target, point, flakTravelSeconds, flakFuseSeconds, flakRadius,
                flakDamage, tracer, flakShotClip, flakBlastClip, flakSoundVolume, () => HitCount++);
            flakCharges.Add(charge);
            FiredCount++;
        }

        private void SchedulePause() => nextBurst = Time.time + Random.Range(pauseMin, Mathf.Max(pauseMin, pauseMax));

        private void AimServos(Vector3 aim, float dt)
        {
            Vector3 local = turretYaw.parent.InverseTransformDirection(aim);
            float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float y = Mathf.MoveTowardsAngle(turretYaw.localEulerAngles.y, yaw, yawSpeed * dt);
            turretYaw.localRotation = Quaternion.Euler(0f, y, 0f);
            local = turretYaw.InverseTransformDirection(aim);
            float elevation = Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
            elevation = Mathf.Clamp(elevation, -10f, maxElevation);
            float pitch = Mathf.MoveTowardsAngle(gunPitch.localEulerAngles.x, -elevation, pitchSpeed * dt);
            gunPitch.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void Fire(Transform muzzle)
        {
            Vector3 direction = Quaternion.AngleAxis(Random.Range(-roundSpread, roundSpread), muzzle.up) *
                (Quaternion.AngleAxis(Random.Range(-roundSpread, roundSpread), muzzle.right) * muzzle.forward);
            var go = new GameObject("Mantis / AA tracer");
            go.transform.position = muzzle.position;
            var round = go.AddComponent<MantisRound>();
            round.Launch(transform, target, direction, projectileSpeed, projectileRange, tracerLength,
                dangerHeight, airPuffAbove, damage, tracer, () => HitCount++);
            rounds.Add(round);
            FiredCount++;
        }
    }
}
