using System;
using UnityEngine;

namespace AirCombat
{
    // Scene-instance test receiver. Existing projectiles own all collision/damage rules.
    [DisallowMultipleComponent]
    public sealed class CarryallRangeTarget : DamageableTarget
    {
        private BoxCollider hitbox;
        private CombatTargetDomain domain;
        private int hp;
        private Action destroyed;
        public override CombatTargetDomain Domain => domain;
        internal int Hp => hp;

        internal void Configure(CombatTargetDomain targetDomain, Vector3 center, Vector3 size, Action onDestroyed)
        {
            domain = targetDomain;
            destroyed = onDestroyed;
            hitbox = gameObject.AddComponent<BoxCollider>();
            hitbox.center = center;
            hitbox.size = size;
            hitbox.isTrigger = false;
            Deactivate();
        }

        internal void ResetTarget(int health)
        {
            hp = Mathf.Max(1, health);
            hitbox.enabled = true;
        }

        internal void Deactivate()
        {
            hp = 0;
            if (hitbox != null) hitbox.enabled = false;
        }

        public override bool ApplyDamage(Vector3 point, int damage)
        {
            if (hp <= 0 || damage <= 0 || !isActiveAndEnabled || !hitbox.enabled) return false;
            hp = Mathf.Max(0, hp - damage);
            if (hp == 0)
            {
                hitbox.enabled = false;
                destroyed?.Invoke();
            }
            return true;
        }
    }
}
