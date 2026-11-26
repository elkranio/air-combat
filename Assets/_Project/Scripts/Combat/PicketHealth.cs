using System;
using UnityEngine;

namespace AirCombat
{
    // Ground-domain receiver for the mobile Picket only. Death is immediate;
    // presentation can subscribe to Killed before the intact hull is hidden.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class PicketHealth : DamageableTarget
    {
        [SerializeField, Min(1)] private int maxHp = 3;
        public int Hp { get; private set; }
        public int MaxHp => Mathf.Max(1, maxHp);
        public bool IsAlive => Hp > 0;
        public override CombatTargetDomain Domain => CombatTargetDomain.Ground;
        public event Action<PicketHealth> Killed;

        private void Awake() => ResetHealth();

        public void ResetHealth() => Hp = MaxHp;

        public override bool ApplyDamage(Vector3 point, int damage)
        {
            if (!isActiveAndEnabled || !IsAlive || damage <= 0) return false;
            Hp = Mathf.Max(0, Hp - damage);
            if (!IsAlive)
            {
                Killed?.Invoke(this);
                gameObject.SetActive(false);
            }
            return true;
        }
    }
}
