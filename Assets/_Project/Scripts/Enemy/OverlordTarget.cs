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
        [SerializeField, Min(1f)] private float detectionRadius = 450f;
        [SerializeField, Min(.1f)] private float preparationSeconds = 22f;
        [SerializeField, Min(.1f)] private float boostSeconds = 2.5f;
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
        private Renderer[] glow = new Renderer[2];
        private MaterialPropertyBlock properties;
        private Vector3 authoredCenter, center, escapeHeading, deathStart, deathForward;
        private Quaternion rest, deathPose;
        private float angle, speed, timer, smokeDebt, deathSpeed;
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
            var l = visual.Find("GlowL"); var r = visual.Find("GlowR");
            glow[0] = l != null ? l.GetComponent<Renderer>() : null;
            glow[1] = r != null ? r.GetComponent<Renderer>() : null;
            properties = new MaterialPropertyBlock();
            ready = true;
            ResetTarget();
            radar?.Register(this);
        }

        private void ResetTarget()
        {
            center = authoredCenter;
            escapeHeading = Vector3.back; // Authored east-sector outbound direction.
#if UNITY_EDITOR
            if (debugNearPlayer)
            {
                // In-place combat trial independent of the distant objective sector.
                Vector3 forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
                center = player.transform.position + forward * 165f;
                center.y = authoredCenter.y;
                escapeHeading = forward;
            }
#endif
            hp = Mathf.Max(1, maxHp);
            State = Phase.Patrol;
            angle = .25f; timer = smokeDebt = 0f;
            falling = burst = false;
            speed = Mathf.Max(1f, player.CruiseSpeed * patrolSpeedRatio);
            visual.gameObject.SetActive(true);
            visual.localRotation = rest;
            var pivot = visual.Find("RadarPivot");
            if (pivot != null) pivot.localRotation = Quaternion.identity;
            foreach (var box in hitboxes) box.enabled = true;
            SetGlow(0f);
            PlaceOnPatrol();
        }

        private void PlaceOnPatrol()
        {
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 tangent = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            transform.SetPositionAndRotation(center + offset * Mathf.Max(10f, patrolRadius), Quaternion.LookRotation(tangent, Vector3.up));
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
            if (State == Phase.Patrol)
            {
                if (toPlayer.sqrMagnitude <= detectionRadius * detectionRadius)
                { State = Phase.Preparing; timer = 0f; }
                else
                {
                    angle = Mathf.Repeat(angle + speed / Mathf.Max(10f, patrolRadius) * dt, 2f * Mathf.PI);
                    PlaceOnPatrol();
                    visual.localRotation = rest * Quaternion.Euler(0f, 0f, -5f);
                }
            }
            if (State == Phase.Preparing)
            {
                timer += dt;
                float turn = Vector3.SignedAngle(transform.forward, escapeHeading, Vector3.up);
                float yaw = Mathf.Clamp(turn, -turnRate * dt, turnRate * dt);
                transform.Rotate(Vector3.up, yaw, Space.World);
                speed = Mathf.MoveTowards(speed, player.CruiseSpeed * escapeSpeedRatio, player.CruiseSpeed * dt);
                visual.localRotation = Quaternion.RotateTowards(visual.localRotation,
                    rest * Quaternion.Euler(0f, 0f, -Mathf.Clamp(turn, -15f, 15f)), 70f * dt);
                // Do not start a chase toward the player's boundary warning.
                // If the available outbound space is tight, ignite sooner.
                float charge = Mathf.Clamp01(timer / Mathf.Max(.1f, preparationSeconds));
                SetGlow(charge * (.8f + .2f * Mathf.Sin(timer * (5f + 9f * charge))));
                if (timer >= preparationSeconds || DistanceToEdge(transform.position, escapeHeading) <= edgeReserve + 420f)
                { State = Phase.Boost; timer = 0f; }
                else MoveForward(speed * dt);
            }
            if (State == Phase.Boost)
            {
                timer += dt;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(escapeHeading, Vector3.up), turnRate * 2f * dt);
                float maxPlayer = playerBoost != null ? playerBoost.BoostSpeed : player.CruiseSpeed;
                float boostSpeed = Mathf.Max(player.CruiseSpeed * boostSpeedRatio, maxPlayer * 2.5f);
                speed = Mathf.MoveTowards(speed, boostSpeed, boostSpeed * dt / .9f);
                visual.localRotation = Quaternion.RotateTowards(visual.localRotation, rest, 100f * dt);
                SetGlow(2.5f);
                // Finish before entering the boundary guard. The visible acceleration
                // is the reason the contact is lost, never a player boundary failure.
                float room = DistanceToEdge(transform.position, transform.forward) - edgeReserve;
                MoveForward(Mathf.Min(speed * dt, Mathf.Max(0f, room)));
                if (timer >= boostSeconds || room <= speed * dt) Escape();
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
            if (State == Phase.Patrol && hp > 0) { State = Phase.Preparing; timer = 0f; }
            if (hp == 0)
            {
                State = Phase.Destroyed; falling = true; timer = smokeDebt = 0f;
                deathStart = transform.position; deathForward = transform.forward; deathPose = visual.localRotation;
                deathSpeed = Mathf.Min(speed, player.CruiseSpeed * 1.2f);
                foreach (var box in hitboxes) box.enabled = false;
                SetGlow(0f);
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

        private void OnDestroy() { if (radar != null) radar.Unregister(this); }
    }
}
