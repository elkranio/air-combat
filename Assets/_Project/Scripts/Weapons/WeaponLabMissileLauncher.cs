using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace AirCombat
{
    // Weapon Lab only. Reuses Sandbox missile flight/prefab, but damages the Lab HP target.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(210)] // TargetLock evaluates on the camera at order 200.
    public sealed class WeaponLabMissileLauncher : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float launchOffset = 6f;
        [SerializeField, Range(32, 128)] private int ringSegments = 80;
        [SerializeField, Min(0.05f)] private float ringWidth = .45f;

        private TargetLock targetLock;
        private MissileProjectile missilePrefab;
        private Material impactMaterial;
        private WeaponLabEnemy fighter;
        private MissileProjectile activeMissile;
        private Material ringMaterial;
        private LineRenderer safeZoneRing;

        public int FiredCount { get; private set; }

        public void Configure(TargetLock lockComponent, MissileProjectile prefab,
            Material hitEffect, WeaponLabEnemy opponent)
        {
            targetLock = lockComponent;
            missilePrefab = prefab;
            impactMaterial = hitEffect;
            fighter = opponent;
            if (safeZoneRing != null) return;

            var shader = Shader.Find("AirCombat/SandboxEffect");
            if (shader == null)
            {
                Debug.LogWarning("Weapon Lab safe-zone shader not found.", this);
                return;
            }
            ringMaterial = new Material(shader) { name = "Weapon Lab missile minimum range (runtime)" };
            var ringObject = new GameObject("Missile / Minimum Lock Distance");
            ringObject.transform.SetParent(transform, false);
            safeZoneRing = ringObject.AddComponent<LineRenderer>();
            safeZoneRing.sharedMaterial = ringMaterial;
            safeZoneRing.useWorldSpace = true; // Never tilt with aircraft banking.
            safeZoneRing.loop = true;
            safeZoneRing.positionCount = Mathf.Clamp(ringSegments, 32, 128);
            safeZoneRing.widthMultiplier = Mathf.Max(.05f, ringWidth);
            safeZoneRing.numCapVertices = 0;
            safeZoneRing.startColor = safeZoneRing.endColor = new Color(.87f, .91f, .94f, .54f);
            safeZoneRing.shadowCastingMode = ShadowCastingMode.Off;
            safeZoneRing.receiveShadows = false;
            safeZoneRing.enabled = false;
        }

        private void LateUpdate()
        {
            bool selected = targetLock != null && targetLock.LockingEnabled;
            if (safeZoneRing != null)
            {
                safeZoneRing.enabled = selected;
                if (selected) DrawSafeZone();
            }
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
            FiredCount++;
            targetLock.ResetLock(); // Each missile requires a new acquisition.
        }

        private void DrawSafeZone()
        {
            float radius = targetLock.MinimumLockDistance;
            int count = safeZoneRing.positionCount;
            Vector3 center = transform.position;
            center.y -= .5f;
            for (int i = 0; i < count; i++)
            {
                float a = i * (2f * Mathf.PI / count);
                safeZoneRing.SetPosition(i, center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius);
            }
        }

        private void OnMissileHit(Vector3 point, Transform target)
        {
            MissileImpact.Spawn(point, impactMaterial);
            if (fighter != null && target == fighter.transform && !fighter.IsDown)
                fighter.Hit(point, fighter.MaxHp); // Same one-hit kill as Sandbox, via Lab death/respawn.
            if (targetLock != null) targetLock.ResetLock();
        }

        private void OnDestroy()
        {
            if (safeZoneRing != null) Destroy(safeZoneRing.gameObject);
            if (ringMaterial != null) Destroy(ringMaterial);
        }
    }
}
