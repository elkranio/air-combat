using UnityEngine;

namespace AirCombat
{
    public enum CombatTargetDomain { Air, Ground }

    // Common target for physical projectiles. Air/Ground classification is a
    // target property, not a projectile-specific dependency on a scene class.
    public abstract class DamageableTarget : MonoBehaviour
    {
        public abstract CombatTargetDomain Domain { get; }
        // True if the target accepted the hit (for hit counters/feedback).
        public abstract bool ApplyDamage(Vector3 point, int damage);
    }
}
