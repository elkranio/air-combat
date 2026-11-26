using UnityEngine;

namespace AirCombat
{
    // Passive arcade recon craft. The composition supplies flight availability and effects.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    [DefaultExecutionOrder(-25)] // After shared retry/FX (-30), before Overlord (-20).
    public sealed class WatcherTarget : AirDamageableTarget, IRadarAirContact, IObjectiveTarget
    {
        [SerializeField] private Transform visual;
        [SerializeField, Min(1)] private int maxHp = 10;
        [SerializeField, Min(10f)] private float patrolRadius = 45f;
        [SerializeField] private float initialAngle;
        [SerializeField] private bool clockwise;
        private BoxCollider[] hitboxes;
        private Transform rotor;
        private System.Func<bool> canRun;
        private IAirTargetEffects effects;
        private Vector3 center, fallStart, fallForward;
        private Quaternion visualRest, fallStartRotation;
        private float angle, speed, fallTime, smokeDebt, groundY;
        private int hp;
        private bool initialized, falling, detonated;
        public override bool IsAlive => initialized && hp > 0;
        public ObjectiveTargetState ObjectiveState => !initialized ? ObjectiveTargetState.Pending :
            hp <= 0 ? ObjectiveTargetState.Destroyed : ObjectiveTargetState.Patrolling;
        public override CombatTargetDomain Domain => CombatTargetDomain.Air;
        public Vector3 RadarPosition => transform.position;
        public Vector3 RadarForward => transform.forward;
        public float RadarThreatRadius => 0f;
        public bool IsRadarVisible => IsAlive && isActiveAndEnabled;
        public RadarContactType RadarKind => RadarContactType.Aircraft;

#if UNITY_EDITOR
        public void ConfigureForAuthoring(Transform model, float radius, float phase, bool reverse)
        { visual = model; patrolRadius = radius; initialAngle = phase; clockwise = reverse; }
#endif
        internal bool Initialize(System.Func<bool> canRun, IAirTargetEffects effects, float patrolSpeed, float floor)
        {
            hitboxes = GetComponents<BoxCollider>();
            if (visual == null || hitboxes.Length == 0 || canRun == null || effects == null) return false;
            rotor = visual.Find("PusherRotor");
            if (!initialized)
            {
                center = transform.position;
                visualRest = visual.localRotation;
            }
            this.canRun = canRun;
            this.effects = effects;
            speed = Mathf.Max(1f, patrolSpeed);
            groundY = floor;
            initialized = true;
            ResetTarget();
            return true;
        }

        private void Update()
        {
            if (!initialized || MissionPause.IsTacticalMapPausedFor(gameObject.scene)) return;
            if (MissionRetry.WasRequestedThisFrame(gameObject.scene))
            {
                ResetTarget();
                return;
            }
            if (canRun()) Tick(Time.deltaTime);
        }

        private void ResetTarget()
        {
            if (!initialized) return;
            hp = Mathf.Max(1, maxHp);
            angle = initialAngle * Mathf.Deg2Rad;
            fallTime = smokeDebt = 0f;
            falling = detonated = false;
            visual.gameObject.SetActive(true);
            visual.localRotation = visualRest;
            foreach (var box in hitboxes) box.enabled = true;
            if (rotor != null) rotor.localRotation = Quaternion.identity;
            PlaceOnPatrol();
        }

        private void PlaceOnPatrol()
        {
            float sign = clockwise ? -1f : 1f;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 tangent = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * sign;
            transform.SetPositionAndRotation(center + offset * patrolRadius, Quaternion.LookRotation(tangent, Vector3.up));
        }

        public override bool ApplyDamage(Vector3 point, int damage)
        {
            if (!IsAlive || damage <= 0 || !canRun()) return false;
            hp = Mathf.Max(0, hp - damage);
            if (hp == 0)
            {
                // Count this kill immediately; the wreck is no longer a target.
                foreach (var box in hitboxes) box.enabled = false;
                falling = true;
                fallTime = smokeDebt = 0f;
                fallStart = transform.position;
                fallForward = transform.forward;
                fallStartRotation = visual.localRotation;
                effects.AirBurst(point, fallForward);
            }
            return true;
        }

        private void Tick(float dt)
        {
            if (!initialized || dt <= 0f) return;
            if (IsAlive)
            {
                if (rotor != null) rotor.Rotate(0f, 0f, 780f * dt, Space.Self);
                angle = Mathf.Repeat(angle + (clockwise ? -1f : 1f) * speed / Mathf.Max(10f, patrolRadius) * dt, 2f * Mathf.PI);
                PlaceOnPatrol();
                float damage = 1f - (float)hp / Mathf.Max(1, maxHp);
                visual.localRotation = visualRest * Quaternion.Euler(0f, 0f, (clockwise ? 1f : -1f) * (4f + damage * 7f));
                if (damage >= .4f) Trail(dt, 5f + damage * 7f, false);
                return;
            }
            if (!falling) return;
            fallTime += dt;
            if (rotor != null) rotor.Rotate(0f, 0f, Mathf.Lerp(780f, 0f, Mathf.Clamp01(fallTime / 2f)) * dt, Space.Self);
            // Short burning glide followed by an accelerating nose-down descent.
            float u = Mathf.Clamp01((fallTime - .65f) / 3.15f);
            float sink = u * u;
            Vector3 next = fallStart + fallForward * (speed * (fallTime + .12f * fallTime * fallTime));
            next.y = Mathf.Lerp(fallStart.y, groundY + 8f, sink);
            transform.position = next;
            // Blend from the actual hit-frame pose, continuing the patrol bank.
            float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(fallTime / .65f));
            float roll = (clockwise ? 1f : -1f) * Mathf.Lerp(14f, 65f, u);
            Quaternion deathPose = visualRest * Quaternion.Euler(Mathf.Lerp(0f, 58f, u), 0f, roll);
            visual.localRotation = Quaternion.Slerp(fallStartRotation, deathPose, blend);
            Trail(dt, 23f, true);
            if (!detonated && fallTime >= 1.05f)
            {
                detonated = true;
                effects.AirBurst(visual.TransformPoint(new Vector3(-2f, .2f, -.8f)), fallForward);
                effects.AirBurst(visual.TransformPoint(new Vector3(.3f, .1f, -5.5f)), fallForward);
            }
            if (u >= 1f)
            {
                falling = false;
                visual.gameObject.SetActive(false);
                Vector3 nose = visual.TransformPoint(new Vector3(0f, .25f, 9f));
                effects.GroundBurst(new Vector3(nose.x, groundY + 1f, nose.z), fallForward);
            }
        }

        private void Trail(float dt, float rate, bool fire)
        {
            smokeDebt = Mathf.Min(8f, smokeDebt + dt * rate);
            while (smokeDebt >= 1f)
            {
                smokeDebt -= 1f;
                Vector3 p = visual.TransformPoint(new Vector3(Random.Range(-.65f, .65f), Random.Range(-.45f, .65f), Random.Range(-7f, -4f)));
                effects.EmitSmoke(p, fire);
            }
        }
    }
}
