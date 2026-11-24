using UnityEngine;

namespace AirCombat
{
    // Fictional passive air target. Receives ordinary swept AC hits.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AirRouteMotor), typeof(BoxCollider))]
    public sealed class CruiseMissileTarget : DamageableTarget, IRadarAirContact
    {
        [SerializeField, Min(1)] private int maxHp = 1;
        private AirRouteMotor motor;
        private BoxCollider hitbox;
        private int hp;
        public bool Alive => hp > 0;
        public bool Finished => motor != null && motor.Finished;
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
            hitbox.enabled = true;
            motor.Begin(route);
        }

        public void Tick(float dt) { if (Alive) motor.Tick(dt); }
        public void SetAltitude(float y) { motor?.SetAltitude(y); }
        public override bool ApplyDamage(Vector3 point, int damage)
        {
            if (!Alive || damage <= 0) return false;
            hp = Mathf.Max(0, hp - damage);
            if (!Alive) hitbox.enabled = false;
            return true;
        }
    }
}
