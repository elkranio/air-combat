using UnityEngine;

namespace AirCombat
{
    // One independently damageable structural zone, not a depot-wide health pool.
    [DisallowMultipleComponent]
    public sealed class GroundDepotPart : DamageableTarget
    {
        private GroundDepotTarget owner;
        private int hp;
        private Bounds localBounds;
        public override CombatTargetDomain Domain => CombatTargetDomain.Ground;
        public bool IsDestroyed => hp <= 0;
        public Bounds LocalBounds => localBounds;

        public void Configure(GroundDepotTarget parent, Vector3 center, Vector3 size, int health)
        {
            owner = parent;
            hp = Mathf.Max(1, health);
            localBounds = new Bounds(center, size);
            transform.localPosition = center;
            var box = gameObject.AddComponent<BoxCollider>();
            box.size = size;
            box.isTrigger = false;
        }

        public override bool ApplyDamage(Vector3 point, int damage)
        {
            if (hp <= 0 || damage <= 0) return false;
            hp = Mathf.Max(0, hp - damage);
            if (hp == 0)
            {
                GetComponent<Collider>().enabled = false;
                owner.OnPartDestroyed(this);
            }
            return true;
        }
    }
}
