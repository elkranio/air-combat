using UnityEngine;
using UnityEngine.InputSystem;

namespace AirCombat
{
    // Weapon Lab only. Reuses Sandbox missile flight/prefab, but damages the Lab HP target.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(210)] // TargetLock evaluates on the camera at order 200.
    public sealed class WeaponLabMissileLauncher : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float launchOffset = 6f;

        private TargetLock targetLock;
        private MissileProjectile missilePrefab;
        private Material impactMaterial;
        private WeaponLabEnemy fighter;
        private MissileProjectile activeMissile;
        private WeaponLabEvasion enemyEvasion;

        public int FiredCount { get; private set; }

        public void Configure(TargetLock lockComponent, MissileProjectile prefab,
            Material hitEffect, WeaponLabEnemy opponent)
        {
            targetLock = lockComponent;
            missilePrefab = prefab;
            impactMaterial = hitEffect;
            fighter = opponent;
            enemyEvasion = fighter != null ? fighter.GetComponent<WeaponLabEvasion>() : null;
        }

        private void LateUpdate()
        {
            bool selected = targetLock != null && targetLock.LockingEnabled;
            if (!selected || missilePrefab == null || activeMissile != null || !targetLock.CanFire)
                return;

            var mouse = Mouse.current;
            if (mouse == null || !Application.isFocused || !mouse.leftButton.wasPressedThisFrame)
                return;
            var target = targetLock.Target;
            if (target == null || !target.gameObject.activeInHierarchy ||
                (fighter != null && fighter.IsDown)) return;

            // Same prefab, speed, turn limits and swept collision as the Sandbox rocket.
            activeMissile = Instantiate(missilePrefab,
                transform.position + transform.forward * launchOffset, transform.rotation);
            activeMissile.Launch(target, OnMissileHit);
            if (enemyEvasion != null) enemyEvasion.NotifyIncomingMissile(activeMissile);
            FiredCount++;
            targetLock.ResetLock(); // Each missile requires a new acquisition.
        }

        private void OnMissileHit(Vector3 point, Transform target)
        {
            MissileImpact.Spawn(point, impactMaterial);
            if (fighter != null && target == fighter.transform && !fighter.IsDown)
                fighter.Hit(point, fighter.MaxHp); // Same one-hit kill as Sandbox, via Lab death/respawn.
            if (targetLock != null) targetLock.ResetLock();
        }
    }
}
