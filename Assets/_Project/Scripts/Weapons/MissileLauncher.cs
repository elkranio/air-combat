using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

namespace AirCombat
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(210)] // after lock evaluation
    public sealed class MissileLauncher : MonoBehaviour
    {
        [SerializeField] private TargetLock targetLock;
        [SerializeField] private MissileProjectile missilePrefab;
        [SerializeField] private Material effectMaterial;
        [SerializeField, Min(0f)] private float launchOffset = 6f;
        [SerializeField, Min(0f)] private float respawnDelay = 1f;
        private MissileProjectile activeMissile;

        private void LateUpdate()
        {
            var mouse = Mouse.current;
            if (mouse == null || !Application.isFocused || !mouse.leftButton.wasPressedThisFrame ||
                activeMissile != null || missilePrefab == null || targetLock == null || !targetLock.CanFire)
                return;
            var target = targetLock.Target;
            if (target == null || !target.gameObject.activeInHierarchy) return;

            activeMissile = Instantiate(missilePrefab, transform.position + transform.forward * launchOffset,
                transform.rotation);
            activeMissile.Launch(target, OnHit, terrainPoint => MissileImpact.Spawn(terrainPoint, effectMaterial));
            targetLock.ResetLock();
        }

        private void OnHit(Vector3 position, Transform target)
        {
            MissileImpact.Spawn(position, effectMaterial);
            if (target == null) return;
            var enemy = target.GetComponent<EnemyFlightController>();
            if (enemy == null) return;
            target.gameObject.SetActive(false);
            targetLock.ResetLock();
            StartCoroutine(Respawn(enemy));
        }

        private IEnumerator Respawn(EnemyFlightController enemy)
        {
            yield return new WaitForSeconds(respawnDelay);
            if (enemy != null)
                enemy.Respawn(transform.position + transform.forward * 44f + transform.right * 20f,
                    transform.rotation);
        }
    }
}
