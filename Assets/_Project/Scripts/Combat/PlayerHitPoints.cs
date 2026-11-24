using UnityEngine;

namespace AirCombat
{
    // Generic aircraft damage receiver. AA1 equips it only on GroundAttackLab's
    // player instance; the shared PlayerJet prefab and Sandbox stay untouched.
    [DisallowMultipleComponent]
    public sealed class PlayerHitPoints : DamageableTarget
    {
        [SerializeField, Min(1)] private int maxHp = 20;
        private BoxCollider hitbox;

        // Only accepted damage is published, after HP has been updated.
        public event System.Action<PlayerHitPoints, Vector3> Damaged;

        public int Hp { get; private set; }
        public int MaxHp => maxHp;
        public int HitCount { get; private set; }
        public bool IsAlive => Hp > 0;
        public float LastHitTime { get; private set; }
        public override CombatTargetDomain Domain => CombatTargetDomain.Air;

        public void InstallHitbox()
        {
            hitbox = GetComponent<BoxCollider>();
            if (hitbox == null) hitbox = gameObject.AddComponent<BoxCollider>();
            hitbox.center = new Vector3(0f, -.2f, 0f);
            hitbox.size = new Vector3(9.5f, 2.8f, 11f);
            hitbox.isTrigger = false;
            ResetHealth();
        }

        public void ResetHealth()
        {
            Hp = maxHp;
            HitCount = 0;
            LastHitTime = -10f;
        }

        public override bool ApplyDamage(Vector3 point, int damage)
        {
            if (!IsAlive || damage <= 0) return false;
            Hp = Mathf.Max(0, Hp - damage);
            HitCount++;
            LastHitTime = Time.time;
            Damaged?.Invoke(this, point);
            return true;
        }
    }
}
