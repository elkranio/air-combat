using UnityEngine;

namespace AirCombat
{
    // Mission01's passive arcade recon craft. No weapon or aircraft AI dependency.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class WatcherTarget : AirDamageableTarget, IRadarAirContact
    {
        [SerializeField] private Transform visual;
        [SerializeField, Min(1)] private int maxHp = 10;
        [SerializeField, Min(10f)] private float patrolRadius = 45f;
        [SerializeField] private float initialAngle;
        [SerializeField] private bool clockwise;
        private BoxCollider hitbox;
        private Mission01ReconEncounter encounter;
        private Vector3 center, fallStart, fallForward;
        private Quaternion visualRest;
        private float angle, speed, fallTime, smokeDebt, groundY;
        private int hp;
        private bool initialized, falling, detonated;
        public override bool IsAlive => initialized && hp > 0;
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
        public bool Initialize(Mission01ReconEncounter owner, float cruiseSpeed, float floor)
        {
            hitbox = GetComponent<BoxCollider>();
            if (visual == null || hitbox == null) return false;
            if (!initialized)
            {
                center = transform.position;
                visualRest = visual.localRotation;
            }
            encounter = owner;
            speed = Mathf.Max(1f, cruiseSpeed * .23f);
            groundY = floor;
            initialized = true;
            ResetTarget();
            return true;
        }

        public void ResetTarget()
        {
            if (!initialized) return;
            hp = Mathf.Max(1, maxHp);
            angle = initialAngle * Mathf.Deg2Rad;
            fallTime = smokeDebt = 0f;
            falling = detonated = false;
            visual.gameObject.SetActive(true);
            visual.localRotation = visualRest;
            hitbox.enabled = true;
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
            if (!IsAlive || damage <= 0 || encounter == null || !encounter.InFlight) return false;
            hp = Mathf.Max(0, hp - damage);
            if (hp == 0)
            {
                // Count this kill immediately; the wreck is no longer a target.
                hitbox.enabled = false;
                falling = true;
                fallTime = smokeDebt = 0f;
                fallStart = transform.position;
                fallForward = transform.forward;
                encounter.AirBurst(point, fallForward);
            }
            return true;
        }

        public void Tick(float dt)
        {
            if (!initialized || dt <= 0f) return;
            if (IsAlive)
            {
                angle = Mathf.Repeat(angle + (clockwise ? -1f : 1f) * speed / Mathf.Max(10f, patrolRadius) * dt, 2f * Mathf.PI);
                PlaceOnPatrol();
                float damage = 1f - (float)hp / Mathf.Max(1, maxHp);
                visual.localRotation = visualRest * Quaternion.Euler(0f, 0f, (clockwise ? 1f : -1f) * (4f + damage * 7f));
                if (damage >= .4f) Trail(dt, 5f + damage * 7f, false);
                return;
            }
            if (!falling) return;
            fallTime += dt;
            // Short burning glide followed by an accelerating nose-down descent.
            float u = Mathf.Clamp01((fallTime - .65f) / 3.15f);
            float sink = u * u;
            Vector3 next = fallStart + fallForward * (speed * (fallTime + .12f * fallTime * fallTime));
            next.y = Mathf.Lerp(fallStart.y, groundY + 16f, sink);
            transform.position = next;
            float roll = (clockwise ? -1f : 1f) * Mathf.Lerp(8f, 65f, u);
            visual.localRotation = visualRest * Quaternion.Euler(Mathf.Lerp(0f, 58f, u), 0f, roll);
            Trail(dt, 23f, true);
            if (!detonated && fallTime >= 1.05f)
            {
                detonated = true;
                encounter.AirBurst(visual.TransformPoint(new Vector3(-5f, 1f, -4f)), fallForward);
                encounter.AirBurst(visual.TransformPoint(new Vector3(5f, 0f, -7f)), fallForward);
            }
            if (u >= 1f)
            {
                falling = false;
                visual.gameObject.SetActive(false);
                Vector3 nose = visual.TransformPoint(new Vector3(0f, .25f, 17f));
                encounter.GroundBurst(new Vector3(nose.x, groundY + 1f, nose.z), fallForward);
            }
        }

        private void Trail(float dt, float rate, bool fire)
        {
            smokeDebt = Mathf.Min(8f, smokeDebt + dt * rate);
            while (smokeDebt >= 1f)
            {
                smokeDebt -= 1f;
                Vector3 p = visual.TransformPoint(new Vector3(Random.Range(-4f, 4f), Random.Range(-1f, 2f), Random.Range(-10f, -4f)));
                encounter.EmitSmoke(p, fire);
            }
        }
    }
}
