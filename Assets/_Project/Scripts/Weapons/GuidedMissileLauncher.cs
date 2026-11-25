using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AirCombat
{
    // Locked anti-air missile launcher. Scene/AI notifications are event consumers.
    // MissileProjectile and MissileImpact are the unchanged shared implementations.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(210)] // TargetLock evaluates on the camera at order 200.
    public sealed class GuidedMissileLauncher : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float launchOffset = 6f;
        [SerializeField, Min(1)] private int hitDamage = 5;
        private JetChassis chassis;

        private void Awake()
        {
            var assembly = GetComponent<JetAssembly>();
            chassis = assembly != null ? assembly.Chassis : null;
        }

        private TargetLock targetLock;
        private MissileProjectile missilePrefab;
        private Material impactMaterial;
        private MissileProjectile activeMissile;

        public int FiredCount { get; private set; }
        public event Action<MissileProjectile> MissileLaunched;

        public void Configure(TargetLock lockComponent, MissileProjectile prefab,
            Material hitEffect, int damage)
        {
            targetLock = lockComponent;
            missilePrefab = prefab;
            impactMaterial = hitEffect;
            hitDamage = Mathf.Max(1, damage);
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
            if (target == null || !target.gameObject.activeInHierarchy) return;

            // Same prefab, speed, turn budget and collision as the current Lab rocket.
            var launchPoint = chassis != null ? chassis.MissileMount : null;
            activeMissile = Instantiate(missilePrefab,
                launchPoint != null ? launchPoint.position : transform.position + transform.forward * launchOffset,
                transform.rotation);
            activeMissile.Launch(target, OnMissileHit, terrainPoint => MissileImpact.Spawn(terrainPoint, impactMaterial));
            MissileLaunched?.Invoke(activeMissile);
            FiredCount++;
            targetLock.ResetLock(); // Every shot requires a fresh acquisition.
        }

        private void OnMissileHit(Vector3 point, Transform target)
        {
            MissileImpact.Spawn(point, impactMaterial);
            if (target != null)
            {
                var damageable = target.GetComponentInParent<DamageableTarget>();
                if (damageable != null && damageable.Domain == CombatTargetDomain.Air)
                    damageable.ApplyDamage(point, hitDamage);
            }
            if (targetLock != null) targetLock.ResetLock();
        }
    }
}
