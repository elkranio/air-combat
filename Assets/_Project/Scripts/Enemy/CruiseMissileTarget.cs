using UnityEngine;

namespace AirCombat
{
    // Fictional passive air target. Receives ordinary swept AC hits.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AirRouteMotor), typeof(BoxCollider))]
    public sealed class CruiseMissileTarget : AirDamageableTarget, IRadarAirContact
    {
        [SerializeField, Min(1)] private int maxHp = 1;
        private AirRouteMotor motor;
        private BoxCollider hitbox;
        private int hp;
        private Vector3 diveStart, diveForward;
        private float diveAge;
        public bool IsDiving { get; private set; }
        public override bool IsAlive => hp > 0;
        public bool Alive => IsAlive;
        public bool Finished => IsDiving ? diveAge >= DiveDuration : motor != null && motor.Finished;
        // Arcade animation values in game units, not a flight model.
        private const float DiveDuration = 1.45f, DiveTravel = 120f;
        public override CombatTargetDomain Domain => CombatTargetDomain.Air;
        public Vector3 RadarPosition => transform.position;
        public Vector3 RadarForward => transform.forward;
        public float RadarThreatRadius => 0f;
        public bool IsRadarVisible => Alive && isActiveAndEnabled;
        public RadarContactType RadarKind => RadarContactType.Aircraft;

        public void Begin(Vector3[] route)
        {
            motor = GetComponent<AirRouteMotor>();
            hitbox = GetComponent<BoxCollider>();
            hp = Mathf.Max(1, maxHp);
            IsDiving = false;
            diveAge = 0f;
            hitbox.enabled = true;
            motor.Begin(route);
        }

        public void BeginDive()
        {
            if (!Alive || IsDiving) return;
            IsDiving = true;
            diveAge = 0f;
            diveStart = transform.position;
            diveForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (diveForward.sqrMagnitude < .01f) diveForward = Vector3.forward;
            // Guns stay planar; previously launched AAM retain the target reference.
            hitbox.enabled = false;
        }

        public void Tick(float dt)
        {
            if (!Alive) return;
            if (!IsDiving) { motor.Tick(dt); return; }
            diveAge = Mathf.Min(DiveDuration, diveAge + Mathf.Max(0f, dt));
            float t = diveAge / DiveDuration;
            float advance = (2f*t + t*t)/3f;
            Vector3 next = diveStart + diveForward * (DiveTravel * advance);
            next.y = Mathf.Lerp(diveStart.y, .8f, t*t);
            Vector3 movement = next - transform.position;
            if (movement.sqrMagnitude > .000001f)
                transform.rotation = Quaternion.LookRotation(movement, Vector3.up);
            transform.position = next;
        }

        public void SetAltitude(float y) { if (!IsDiving) motor?.SetAltitude(y); }
        public override bool ApplyDamage(Vector3 point, int damage)
        {
            if (!Alive || damage <= 0) return false;
            hp = Mathf.Max(0, hp - damage);
            if (!Alive) hitbox.enabled = false;
            return true;
        }
    }
}
