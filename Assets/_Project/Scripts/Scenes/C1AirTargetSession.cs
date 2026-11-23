using UnityEngine;
using UnityEngine.InputSystem;

namespace AirCombat
{
    // C1b-only bridge from GroundAttackLab's six-weapon setup to the existing
    // WeaponLab fighter. Keeps both lab sessions and their prefab ownership intact.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-90)] // Assign target links before enemy Start() runs.
    public sealed class C1AirTargetSession : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private WeaponLabEnemy fighter;
        [SerializeField] private CombatCameraRig cameraRig;
        [SerializeField] private MissileProjectile missilePrefab;
        [SerializeField] private Material missileImpactMaterial;

        private GuidedMissileLauncher launcher;
        private WeaponLabEvasion evasion;

        public void Configure(Transform aircraft, WeaponLabEnemy enemy, CombatCameraRig rig,
            MissileProjectile missile, Material impact)
        {
            player = aircraft;
            fighter = enemy;
            cameraRig = rig;
            missilePrefab = missile;
            missileImpactMaterial = impact;
        }

        private void Start()
        {
            if (player == null || fighter == null || cameraRig == null || missilePrefab == null)
            {
                Debug.LogError("C1b: fighter, player, camera or A2A missile missing.", this);
                enabled = false;
                return;
            }

            var flight = fighter.GetComponent<EnemyFlightController>();
            var chassis = fighter.GetComponent<JetAssembly>();
            var hitbox = fighter.GetComponentInChildren<Collider>(true);
            var renderers = fighter.GetComponentsInChildren<Renderer>(true);
            var enemyBoost = fighter.GetComponent<Afterburner>();
            if (flight == null || chassis == null || hitbox == null || enemyBoost == null)
            {
                Debug.LogError("C1b: EnemyJet prefab contract incomplete.", fighter);
                enabled = false;
                return;
            }

            // The prefab has no scene links by design. Reuse the same enemy
            // chase/evasion/respawn setup as WeaponLab, without starting that lab.
            flight.Configure(player, chassis.VisualRoot);
            fighter.Configure(player, flight, hitbox, renderers);
            evasion = fighter.GetComponent<WeaponLabEvasion>();
            if (evasion != null)
            {
                evasion.Configure(player);
                fighter.ConfigureEvasion(evasion, enemyBoost);
                flight.ConfigureLabEvasion(evasion, enemyBoost);
            }
            var maneuver = fighter.GetComponent<BreakTurn>();
            if (maneuver != null)
            {
                maneuver.Configure(enemyBoost, flight.Visual);
                flight.ConfigureBreakTurn(maneuver);
                if (evasion != null) evasion.ConfigureBreakTurn(maneuver);
            }
            fighter.ResetEncounter();

            // GroundAttackLabSession intentionally clears air lock in Awake.
            // All Awake calls finish before this Start; opt into a real air target
            // only here and only in C1b, without changing the ground lab itself.
            cameraRig.BindLockTarget(fighter.transform, 1f, .5f, fighter);
            var lockHud = cameraRig.GetComponent<TargetLockHud>();
            if (lockHud == null) cameraRig.gameObject.AddComponent<TargetLockHud>();
            launcher = player.GetComponent<GuidedMissileLauncher>();
            if (launcher == null) launcher = player.gameObject.AddComponent<GuidedMissileLauncher>();
            launcher.Configure(cameraRig.Lock, missilePrefab, missileImpactMaterial, fighter.MaxHp);
            if (evasion != null) launcher.MissileLaunched += evasion.NotifyIncomingMissile;
        }

        private void Update()
        {
            var keys = Keyboard.current;
            if (keys != null && Application.isFocused && keys.rKey.wasPressedThisFrame)
                fighter?.ResetEncounter();
        }

        private void OnDisable()
        {
            if (launcher != null && evasion != null)
                launcher.MissileLaunched -= evasion.NotifyIncomingMissile;
        }
    }
}
