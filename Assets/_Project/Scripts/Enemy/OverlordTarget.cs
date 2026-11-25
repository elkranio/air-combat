using UnityEngine;
using UnityEngine.InputSystem;

namespace AirCombat
{
    // Mission-local optional aircraft. Terminal escape is not a kill.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    [DefaultExecutionOrder(-20)]
    public sealed class OverlordTarget : AirDamageableTarget, IRadarAirContact
    {
        public enum Phase { Patrol, Preparing, Boost, Destroyed, Escaped }
        [SerializeField] private Mission01LaunchSequence launch;
        [SerializeField] private Mission01ReconEncounter effects;
        [SerializeField] private FlightController player;
        [SerializeField] private Transform visual;
        [SerializeField] private float groundY;
        [SerializeField, Min(1)] private int maxHp = 18;
        [SerializeField, Min(10f)] private float patrolRadius = 65f;
        // New field names intentionally replace the older serialized defaults.
        [SerializeField, Min(10f)] private float patrolLegHalfLength = 55f;
        [SerializeField] private Vector2 patrolCenterOffset = new Vector2(-70f, 300f);
        [SerializeField, Min(1f)] private float visualDetectionRadius = 220f;
        [SerializeField, Range(.1f, .9f)] private float southeastTurnFraction = .65f;
        [SerializeField, Min(.1f)] private float preparationSeconds = 22f;
        [SerializeField, Min(.1f)] private float boostSeconds = 2.5f;
        [SerializeField, Min(.1f)] private float boostAccelerationSeconds = 1.6f;
        [SerializeField, Range(.1f, 1f)] private float patrolSpeedRatio = .35f;
        [SerializeField, Range(.1f, 1f)] private float escapeSpeedRatio = .5f;
        [SerializeField, Min(1f)] private float boostSpeedRatio = 4f;
        [SerializeField, Min(1f)] private float turnRate = 65f;
        [SerializeField, Min(0f)] private float edgeReserve = 310f;
#if UNITY_EDITOR
        [Header("Editor only: test near debug takeoff")]
        [SerializeField] private bool debugNearPlayer;
#endif
        private BoxCollider[] hitboxes;
        private AircraftRadar radar;
        private Afterburner playerBoost;
        private Camera viewCamera;
        private bool useMissionEscapeRoute;
        private Vector3 escapeDestination;
        private Renderer[] glow = new Renderer[2];
        private MaterialPropertyBlock properties;
        private OverlordEscapeVfx escapeVfx;
        private Vector3 authoredCenter, center, escapeHeading, deathStart, deathForward;
        private Quaternion rest, deathPose;
        private float patrolDistance, speed, timer, smokeDebt, deathSpeed;
        private float chargeDuration, chargeProgress, boostStartSpeed, boostTargetSpeed, boostRampSeconds;
        private int hp;
        private bool ready, falling, burst;
        public Phase State { get; private set; } = Phase.Patrol;
        public override bool IsAlive => ready && hp > 0 && State != Phase.Escaped;
        public override CombatTargetDomain Domain => CombatTargetDomain.Air;
        public Vector3 RadarPosition => transform.position;
        public Vector3 RadarForward => transform.forward;
        public float RadarThreatRadius => 0f;
        public bool IsRadarVisible => IsAlive && isActiveAndEnabled;
        public RadarContactType RadarKind => RadarContactType.Aircraft;
        public string ObjectiveStatus => State == Phase.Destroyed ? "DESTROYED" : State == Phase.Escaped ? "TARGET ESCAPED" :
            State == Phase.Boost ? "ESCAPING" : State == Phase.Preparing ? "ENGINES CHARGING" : "PATROLLING";

#if UNITY_EDITOR
        public void ConfigureForAuthoring(Mission01LaunchSequence sequence, Mission01ReconEncounter fx,
            FlightController controller, Transform model, float floor)
        { launch = sequence; effects = fx; player = controller; visual = model; groundY = floor; }
#endif

        private void Start()
        {
            hitboxes = GetComponents<BoxCollider>();
            if (launch == null || effects == null || player == null || visual == null || hitboxes.Length == 0)
            { Debug.LogError("AE01: missing encounter references.", this); enabled = false; return; }
            authoredCenter = transform.position;
            rest = visual.localRotation;
            radar = player.GetComponent<AircraftRadar>();
            playerBoost = player.GetComponent<Afterburner>();
            // Resolve the same fixed-world flight camera used by the combat HUD.
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                var follow = root.GetComponentInChildren<ArcadeCameraFollow>(true);
                if (follow == null) continue;
                viewCamera = follow.GetComponent<Camera>();
                if (viewCamera != null) break;
            }
            if (viewCamera == null)
                Debug.LogWarning("AE01D: flight camera missing; damage can still trigger escape.", this);
            var l = visual.Find("GlowL"); var r = visual.Find("GlowR");
            glow[0] = l != null ? l.GetComponent<Renderer>() : null;
            glow[1] = r != null ? r.GetComponent<Renderer>() : null;
            properties = new MaterialPropertyBlock();
            escapeVfx = new OverlordEscapeVfx(visual);
            ready = true;
            ResetTarget();
            radar?.Register(this);
        }

        private void ResetTarget()
        {
            // Move the existing marker's runtime patrol north/west to leave
            // enough room for the full charge and the diagonal boost.
            center = authoredCenter + new Vector3(patrolCenterOffset.x, 0f, patrolCenterOffset.y);
            useMissionEscapeRoute = true;
            escapeHeading = Vector3.back;
            Rect operation = launch.OperationBounds;
            float inset = Mathf.Max(0f, edgeReserve) + 30f;
            escapeDestination = new Vector3(operation.xMax - inset, center.y, operation.yMin + inset);
#if UNITY_EDITOR
            if (debugNearPlayer)
            {
                // In-place combat trial independent of the distant objective sector.
                Vector3 forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
                center = player.transform.position + forward * 165f;
                center.y = authoredCenter.y;
                escapeHeading = forward;
                useMissionEscapeRoute = false;
            }
#endif
            hp = Mathf.Max(1, maxHp);
            State = Phase.Patrol;
            patrolDistance = Mathf.Max(10f, patrolLegHalfLength);
            timer = smokeDebt = 0f;
            falling = burst = false;
            speed = Mathf.Max(1f, player.CruiseSpeed * patrolSpeedRatio);
            visual.gameObject.SetActive(true);
            visual.localRotation = rest;
            var pivot = visual.Find("RadarPivot");
            if (pivot != null) pivot.localRotation = Quaternion.identity;
            foreach (var box in hitboxes) box.enabled = true;
            SetGlow(0f);
            escapeVfx?.Clear();
            PlaceOnPatrol(0f);
        }

        // Equal-speed racetrack: long east/west straights joined by tangent
        // semicircles. No teleport or heading reversal at either endpoint.
        private void PlaceOnPatrol(float dt)
        {
            float h = Mathf.Max(10f, patrolLegHalfLength);
            float r = Mathf.Max(10f, patrolRadius);
            float straight = 2f * h, arc = Mathf.PI * r;
            patrolDistance = Mathf.Repeat(patrolDistance + speed * dt, 2f * (straight + arc));
            float d = patrolDistance;
            Vector3 offset, tangent;
            bool turning = false;
            if (d < straight)
            {
                offset = new Vector3(-h + d, 0f, r);
                tangent = Vector3.right;
            }
            else if ((d -= straight) < arc)
            {
                float a = d / r;
                offset = new Vector3(h + r * Mathf.Sin(a), 0f, r * Mathf.Cos(a));
                tangent = new Vector3(Mathf.Cos(a), 0f, -Mathf.Sin(a));
                turning = true;
            }
            else if ((d -= arc) < straight)
            {
                offset = new Vector3(h - d, 0f, -r);
                tangent = Vector3.left;
            }
            else
            {
                float a = (d - straight) / r;
                offset = new Vector3(-h - r * Mathf.Sin(a), 0f, -r * Mathf.Cos(a));
                tangent = new Vector3(-Mathf.Cos(a), 0f, Mathf.Sin(a));
                turning = true;
            }
            transform.SetPositionAndRotation(center + offset, Quaternion.LookRotation(tangent, Vector3.up));
            visual.localRotation = Quaternion.RotateTowards(visual.localRotation,
                rest * Quaternion.Euler(0f, 0f, turning ? -8f : 0f), 35f * dt);
        }

        private float EscapeSpeed => Mathf.Max(1f, player.CruiseSpeed * escapeSpeedRatio);
        private float BoostTargetSpeed => Mathf.Max(player.CruiseSpeed * boostSpeedRatio,
            (playerBoost != null ? playerBoost.BoostSpeed : player.CruiseSpeed) * 2.5f);
        private float BoostDuration => Mathf.Max(.1f, boostSeconds);
        private float BoostRamp => Mathf.Clamp(boostAccelerationSeconds, .05f, BoostDuration * .8f);

        // Integral of v0 + (v1-v0) * smoothstep(t/ramp). Continuous speed
        // and acceleration at ignition and end of ramp, independent of FPS.
        private static float BoostDistance(float t, float initial, float target, float ramp)
        {
            float u = Mathf.Clamp01(t / ramp);
            return initial * t + (target - initial) *
                (ramp * (u * u * u - .5f * u * u * u * u) + Mathf.Max(0f, t - ramp));
        }

        private float RequiredBoostRoom => BoostDistance(BoostDuration, EscapeSpeed, BoostTargetSpeed, BoostRamp) + 12f;

        private bool VisibleToPlayer()
        {
            if (viewCamera == null || !viewCamera.isActiveAndEnabled) return false;
            Vector3 p = viewCamera.WorldToViewportPoint(transform.position);
            return p.z >= viewCamera.nearClipPlane && p.z <= viewCamera.farClipPlane &&
                p.x >= 0f && p.x <= 1f && p.y >= 0f && p.y <= 1f;
        }

        private void BeginPreparation()
        {
            State = Phase.Preparing;
            timer = chargeProgress = 0f;
            // Budget charge time before the chase, so the glow reaches full
            // intensity even when the authored outbound reserve is tight.
            float room = DistanceToEdge(transform.position, escapeHeading) - edgeReserve;
            chargeDuration = Mathf.Clamp((room - RequiredBoostRoom) / EscapeSpeed,
                .1f, Mathf.Max(.1f, preparationSeconds));
        }

        private void BeginBoost()
        {
            State = Phase.Boost;
            timer = 0f;
            boostStartSpeed = speed;
            boostTargetSpeed = Mathf.Max(speed, BoostTargetSpeed);
            boostRampSeconds = BoostRamp;
        }

        private void Update()
        {
            if (!ready || TacticalMapView.IsOpenFor(gameObject.scene)) return;
            var keys = Keyboard.current;
            if (Application.isFocused && keys != null && keys.rKey.wasPressedThisFrame)
            { ResetTarget(); return; }
            if (launch.State != Mission01LaunchSequence.Phase.Flight || Time.deltaTime <= 0f) return;
            float dt = Time.deltaTime;
            if (State == Phase.Destroyed) { if (falling) TickWreck(dt); return; }
            if (!IsAlive) return;
            Vector3 toPlayer = player.transform.position - transform.position; toPlayer.y = 0f;
            if (State == Phase.Patrol && toPlayer.sqrMagnitude <= visualDetectionRadius * visualDetectionRadius &&
                VisibleToPlayer()) BeginPreparation();
            if (State == Phase.Patrol) PlaceOnPatrol(dt);
            else if (State == Phase.Preparing)
            {
                timer += dt;
                // First break south, then bank onto the safe southeast bearing.
                // Freeze this bearing on ignition; the boost stays a straight run.
                if (useMissionEscapeRoute && timer >= chargeDuration * southeastTurnFraction)
                {
                    Vector3 outbound = escapeDestination - transform.position;
                    outbound.y = 0f;
                    if (outbound.sqrMagnitude > 1f && outbound.z < 0f)
                        escapeHeading = outbound.normalized;
                }
                float turn = Vector3.SignedAngle(transform.forward, escapeHeading, Vector3.up);
                float yaw = Mathf.Clamp(turn, -turnRate * dt, turnRate * dt);
                transform.Rotate(Vector3.up, yaw, Space.World);
                speed = Mathf.MoveTowards(speed, EscapeSpeed, player.CruiseSpeed * dt);
                visual.localRotation = Quaternion.RotateTowards(visual.localRotation,
                    rest * Quaternion.Euler(0f, 0f, -Mathf.Clamp(turn, -15f, 15f)), 70f * dt);
                float room = DistanceToEdge(transform.position, escapeHeading) - edgeReserve;
                float charge = Mathf.Clamp01(timer / chargeDuration);
                // Approach the end of the charge if clearance shrinks earlier
                // than predicted. Keep the reserve; never force a boundary chase.
                float remaining = Mathf.Max(0f, room - RequiredBoostRoom);
                chargeProgress = Mathf.Max(chargeProgress, Mathf.Max(charge,
                    1f - remaining / Mathf.Max(1f, EscapeSpeed * chargeDuration)));
                charge = chargeProgress;
                float pulse = .82f + .18f * Mathf.Sin(2f * Mathf.PI * timer * (1f + 1.5f * charge));
                SetGlow(charge * pulse * 1.8f);
                escapeVfx?.Charge(charge, pulse);
                // This frame belongs to preparation; move once at the current
                // speed, then start the boost clock on the next frame.
                float travelRoom = DistanceToEdge(transform.position, transform.forward) - edgeReserve;
                MoveForward(Mathf.Min(speed * dt, Mathf.Max(0f, travelRoom)));
                if (timer >= chargeDuration || room <= RequiredBoostRoom + speed * dt)
                    BeginBoost();
            }
            else if (State == Phase.Boost)
            {
                float previous = timer;
                timer = Mathf.Min(BoostDuration, timer + dt);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(escapeHeading, Vector3.up), turnRate * 2f * dt);
                float thrust = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(timer / boostRampSeconds));
                speed = Mathf.Lerp(boostStartSpeed, boostTargetSpeed, thrust);
                visual.localRotation = Quaternion.RotateTowards(visual.localRotation, rest, 100f * dt);
                SetGlow(Mathf.Lerp(1.8f, 3.1f, thrust));
                escapeVfx?.Boost(thrust, timer);
                float distance = BoostDistance(timer, boostStartSpeed, boostTargetSpeed, boostRampSeconds)
                    - BoostDistance(previous, boostStartSpeed, boostTargetSpeed, boostRampSeconds);
                // Terminal state is still a successful escape, not a kill.
                float room = DistanceToEdge(transform.position, transform.forward) - edgeReserve;
                MoveForward(Mathf.Min(distance, Mathf.Max(0f, room)));
                if (timer >= BoostDuration || room <= distance) Escape();
            }
            if (IsAlive && hp <= maxHp * .6f) Trail(dt, 7f, false);
        }

        private float DistanceToEdge(Vector3 p, Vector3 direction)
        {
            Rect b = launch.OperationBounds;
            float tx = direction.x > .0001f ? (b.xMax - p.x) / direction.x :
                direction.x < -.0001f ? (b.xMin - p.x) / direction.x : float.PositiveInfinity;
            float tz = direction.z > .0001f ? (b.yMax - p.z) / direction.z :
                direction.z < -.0001f ? (b.yMin - p.z) / direction.z : float.PositiveInfinity;
            return Mathf.Max(0f, Mathf.Min(tx, tz));
        }

        private void MoveForward(float distance) { transform.position += transform.forward * distance; }

        private void Escape()
        {
            State = Phase.Escaped;
            SetGlow(0f);
            escapeVfx?.Clear();
            foreach (var box in hitboxes) box.enabled = false;
            visual.gameObject.SetActive(false);
        }

        private void SetGlow(float strength)
        {
            foreach (var renderer in glow)
            {
                if (renderer == null) continue;
                renderer.GetPropertyBlock(properties);
                properties.SetColor("_EmissionColor", new Color(.035f, .12f, .16f) + new Color(.1f, .8f, 1f) * strength);
                renderer.SetPropertyBlock(properties);
            }
        }

        public override bool ApplyDamage(Vector3 point, int damage)
        {
            if (!IsAlive || damage <= 0 || launch.State != Mission01LaunchSequence.Phase.Flight) return false;
            hp = Mathf.Max(0, hp - damage);
            if (State == Phase.Patrol && hp > 0) BeginPreparation();
            if (hp == 0)
            {
                State = Phase.Destroyed; falling = true; timer = smokeDebt = 0f;
                deathStart = transform.position; deathForward = transform.forward; deathPose = visual.localRotation;
                deathSpeed = Mathf.Min(speed, player.CruiseSpeed * 1.2f);
                foreach (var box in hitboxes) box.enabled = false;
                SetGlow(0f);
                escapeVfx?.Clear();
                effects.AirBurst(point, deathForward);
            }
            return true;
        }

        private void TickWreck(float dt)
        {
            timer += dt;
            float u = Mathf.Clamp01((timer - .5f) / 3.3f);
            Vector3 p = deathStart + deathForward * (deathSpeed * (timer + .08f * timer * timer));
            p.y = Mathf.Lerp(deathStart.y, groundY + 8f, u * u);
            transform.position = p;
            var pose = rest * Quaternion.Euler(Mathf.Lerp(0f, 52f, u), 0f, Mathf.Lerp(-12f, -62f, u));
            visual.localRotation = Quaternion.Slerp(deathPose, pose, Mathf.SmoothStep(0f, 1f, timer / .55f));
            Trail(dt, 26f, true);
            if (!burst && timer >= 1f)
            {
                burst = true;
                effects.AirBurst(visual.TransformPoint(new Vector3(-5f, .2f, -6f)), deathForward);
                effects.AirBurst(visual.TransformPoint(new Vector3(5f, .2f, -6f)), deathForward);
            }
            if (u >= 1f)
            { falling = false; visual.gameObject.SetActive(false); effects.GroundBurst(new Vector3(p.x, groundY + 1f, p.z), deathForward); }
        }

        private void Trail(float dt, float rate, bool burning)
        {
            smokeDebt = Mathf.Min(8f, smokeDebt + dt * rate);
            while (smokeDebt >= 1f)
            {
                smokeDebt -= 1f;
                float x = Random.value < .5f ? -5.15f : 5.15f;
                effects.EmitSmoke(visual.TransformPoint(new Vector3(x, .28f, -10.5f)), burning);
            }
        }

        private void OnGUI()
        {
            if (!ready || launch.State != Mission01LaunchSequence.Phase.Flight || TacticalMapView.IsOpenFor(gameObject.scene) || Event.current.type != EventType.Repaint) return;
            var style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * .019f, 13, 22)), fontStyle = FontStyle.Bold };
            string message = "SECONDARY / OVERLORD / " + ObjectiveStatus;
            style.normal.textColor = new Color(.05f,.07f,.08f,.9f);
            GUI.Label(new Rect(21, 56, 620, 35), message, style);
            style.normal.textColor = State == Phase.Destroyed ? new Color(.58f,.94f,.76f) : State == Phase.Escaped ? new Color(1f,.52f,.4f) : new Color(.97f,.74f,.36f);
            GUI.Label(new Rect(20, 55, 620, 35), message, style);
        }

        private void OnDisable() { escapeVfx?.Clear(); }

        private void OnDestroy()
        {
            escapeVfx?.Dispose();
            if (radar != null) radar.Unregister(this);
        }
    }
}
