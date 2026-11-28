using UnityEngine;

namespace AirCombat
{
    // Shared weapon-system policy for one enemy archetype; no runtime state.
    [CreateAssetMenu(menuName = "Air Combat/Weapons/Enemy AAM Loadout")]
    public sealed class EnemyAamLoadout : ScriptableObject
    {
        [SerializeField] private MissileProjectile projectile;
        [SerializeField] private EnemyMissileStressTest.Settings policy = new EnemyMissileStressTest.Settings();

        public MissileProjectile Projectile => projectile;
        public EnemyMissileStressTest.Settings Policy => policy;
        public bool IsConfigured => projectile != null && policy != null;
    }
}
