using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace AirCombat
{
    // Isolated large-map test arena. No changes to Sandbox or the art-study scenes.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public sealed class WeaponLabSession : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private FlightController flight;
        [SerializeField] private Afterburner boost;
        [SerializeField] private WeaponController cannon;
        [SerializeField] private Transform[] passiveTargets; // V0 generator compatibility only
        [SerializeField] private WeaponLabEnemy fighter;
        [SerializeField] private MissileProjectile missilePrefab; // Same asset as Sandbox, linked in WeaponLab scene.
        [SerializeField] private Material missileImpactMaterial;
        [SerializeField, Min(1000f)] private float mapHalfExtent = 6000f;

        private bool showUI = true;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private GUIStyle text;
        private CameraLookAhead cameraLead;
        private TopDownCameraFollow cameraFollow;
        private BoundedAim aim;
        private TargetLock missileLock;
        private TargetLockHud missileHud;
        private GuidedMissileLauncher labMissileLauncher;
        private WeaponSelection weaponSelection;
        private bool ownsWeaponSelection;
        private BreakTurn playerBreakTurn;
        private BreakTurn enemyBreakTurn;
        private bool ownsPlayerBreakTurn;
        private bool ownsEnemyBreakTurn;
        private bool ownsMissileLauncher;
        private bool ownsMissileLock;
        private bool ownsMissileHud;
        private static readonly float[] SideLanes = { 0f, -16f, 19f, -30f, 29f, -8f, 9f };

        public void Configure(UniversalRenderPipelineAsset pipeline, Transform aircraft,
            FlightController controller, Afterburner afterburner,
            WeaponController weapon, Transform[] targets, float halfExtent)
        {
            // Retired editor generators still pass this argument; CombatLightingRig
            // now owns the shared URP profile and scene illumination.
            _ = pipeline;
            player = aircraft;
            flight = controller;
            boost = afterburner;
            cannon = weapon;
            passiveTargets = targets;
            mapHalfExtent = halfExtent;
        }

        // Upgrade existing range without recreating it or discarding imported audio.
        public void ConfigureFighter(WeaponLabEnemy opponent)
        {
            fighter = opponent;
            passiveTargets = System.Array.Empty<Transform>();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (player == null || flight == null || boost == null || cannon == null)
            { Debug.LogError("Weapon Lab missing flight/weapon references.", this); enabled = false; return; }
            // Runtime-only input remapping; other scenes keep their existing pitch hotkeys.
            cameraFollow = FindFirstObjectByType<TopDownCameraFollow>();
            var viewCamera = cameraFollow != null ? cameraFollow.GetComponent<Camera>() : null;
            if (viewCamera == null)
            { Debug.LogError("Weapon Lab needs a TopDownCameraFollow camera.", this); enabled = false; return; }
            // A4c prefab scene keeps look-ahead on the reusable camera rig.
            // Legacy WeaponLab has no rig and keeps its original session-owned lead.
            var prefabRig = viewCamera.GetComponent<CombatCameraRig>();
            if (prefabRig != null)
            {
                prefabRig.BindPlayer(player, boost, false);
                cameraLead = viewCamera.GetComponent<CameraLookAhead>();
            }
            else
            {
                cameraFollow.SetPitchHotkeysEnabled(false);
                cameraLead = GetComponent<CameraLookAhead>();
                if (cameraLead == null) cameraLead = gameObject.AddComponent<CameraLookAhead>();
            }
            if (cameraLead != null)
            {
                cameraLead.Configure(viewCamera, player, boost);
                cameraLead.enabled = true;
            }
            aim = player.GetComponent<BoundedAim>();
            if (aim == null) aim = player.gameObject.AddComponent<BoundedAim>();
            aim.Configure(viewCamera, player);
            aim.enabled = true;
            cannon.ConfigureAim(aim);
            weaponSelection = player.GetComponent<WeaponSelection>();
            if (weaponSelection == null)
            {
                weaponSelection = player.gameObject.AddComponent<WeaponSelection>();
                ownsWeaponSelection = true;
            }
            cannon.ConfigureSelection(weaponSelection);
            playerBreakTurn = player.GetComponent<BreakTurn>();
            if (playerBreakTurn == null)
            {
                playerBreakTurn = player.gameObject.AddComponent<BreakTurn>();
                ownsPlayerBreakTurn = true;
            }
            playerBreakTurn.Configure(boost, flight.Visual);
            flight.ConfigureBreakTurn(playerBreakTurn);
            if (fighter != null)
            {
                var enemyFlight = fighter.GetComponent<EnemyFlightController>();
                var enemyBoost = fighter.GetComponent<Afterburner>();
                enemyBreakTurn = fighter.GetComponent<BreakTurn>();
                if (enemyBreakTurn == null)
                {
                    enemyBreakTurn = fighter.gameObject.AddComponent<BreakTurn>();
                    ownsEnemyBreakTurn = true;
                }
                enemyBreakTurn.Configure(enemyBoost, enemyFlight != null ? enemyFlight.Visual : null);
                if (enemyFlight != null) enemyFlight.ConfigureBreakTurn(enemyBreakTurn);
                var enemyEvasion = fighter.GetComponent<WeaponLabEvasion>();
                if (enemyEvasion != null) enemyEvasion.ConfigureBreakTurn(enemyBreakTurn);
                ResolveMissileAssetsForEditor();
                // Reuse Sandbox lock UI and geometry, but attach only at runtime in Weapon Lab.
                missileLock = viewCamera.GetComponent<TargetLock>();
                if (missileLock == null)
                {
                    missileLock = viewCamera.gameObject.AddComponent<TargetLock>();
                    ownsMissileLock = true;
                }
                missileLock.ConfigureTarget(fighter.transform, 1f, .5f, fighter);
                missileHud = viewCamera.GetComponent<TargetLockHud>();
                if (missileHud == null)
                {
                    missileHud = viewCamera.gameObject.AddComponent<TargetLockHud>();
                    ownsMissileHud = true;
                }
                cannon.ConfigureMissileLock(missileLock);
                if (missilePrefab != null)
                {
                    labMissileLauncher = player.GetComponent<GuidedMissileLauncher>();
                    if (labMissileLauncher == null)
                    {
                        labMissileLauncher = player.gameObject.AddComponent<GuidedMissileLauncher>();
                        ownsMissileLauncher = true;
                    }
                    // The old Lab missile killed this opponent in one hit, irrespective
                    // of Inspector maxHp. The reusable launcher receives that damage
                    // through configuration, not through a Lab-only target type.
                    labMissileLauncher.Configure(missileLock, missilePrefab, missileImpactMaterial, fighter.MaxHp);
                    labMissileLauncher.MissileLaunched += NotifyIncomingMissile;
                }
                else Debug.LogError("Weapon Lab missile prefab missing. Reopen WeaponLab.unity and check the Missile Prefab reference.", this);
            }
            spawnPosition = player.position;
            spawnRotation = player.rotation;
            if (fighter == null) PlaceAllTargets();
        }

        private void NotifyIncomingMissile(MissileProjectile missile)
        {
            // Only the Lab AI consumes this event. Weapon code stays scene-neutral.
            if (fighter == null) return;
            var evasion = fighter.GetComponent<WeaponLabEvasion>();
            if (evasion != null) evasion.NotifyIncomingMissile(missile);
        }

        // git apply can update the .unity YAML while Unity still holds the old scene in memory.
        // WeaponLab is an editor-only feel test: recover references from the existing assets
        // without rebuilding the scene or copying the missile prefab.
        private void ResolveMissileAssetsForEditor()
        {
#if UNITY_EDITOR
            if (missilePrefab == null)
            {
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Art/Weapons/Missile.prefab");
                if (prefab != null) missilePrefab = prefab.GetComponent<MissileProjectile>();
            }
            if (missileImpactMaterial == null)
                missileImpactMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/_Project/Art/VFX/MissileEffect.mat");
#endif
        }

        private void OnDisable()
        {
            if (cameraFollow != null) cameraFollow.SetPitchHotkeysEnabled(true);
            if (cameraLead != null) cameraLead.enabled = false;
            if (aim != null) aim.enabled = false;
            cannon?.ConfigureMissileLock(null);
            cannon?.ConfigureSelection(null);
            if (ownsWeaponSelection && weaponSelection != null) Destroy(weaponSelection);
            weaponSelection = null;
            ownsWeaponSelection = false;
            flight?.ConfigureBreakTurn(null);
            if (fighter != null)
            {
                var enemyFlight = fighter.GetComponent<EnemyFlightController>();
                if (enemyFlight != null) enemyFlight.ConfigureBreakTurn(null);
            }
            if (fighter != null)
            {
                var evasion = fighter.GetComponent<WeaponLabEvasion>();
                if (evasion != null) evasion.ConfigureBreakTurn(null);
            }
            if (ownsPlayerBreakTurn && playerBreakTurn != null) Destroy(playerBreakTurn);
            if (ownsEnemyBreakTurn && enemyBreakTurn != null) Destroy(enemyBreakTurn);
            playerBreakTurn = enemyBreakTurn = null;
            ownsPlayerBreakTurn = ownsEnemyBreakTurn = false;
            if (labMissileLauncher != null) labMissileLauncher.MissileLaunched -= NotifyIncomingMissile;
            if (ownsMissileLauncher && labMissileLauncher != null) Destroy(labMissileLauncher);
            labMissileLauncher = null;
            ownsMissileLauncher = false;
            if (ownsMissileHud && missileHud != null) Destroy(missileHud);
            if (ownsMissileLock && missileLock != null) Destroy(missileLock);
            ownsMissileHud = ownsMissileLock = false;
            missileHud = null;
            missileLock = null;
        }

        private void Update()
        {
            var k = Keyboard.current;
            if (k != null && Application.isFocused)
            {
                if (k.hKey.wasPressedThisFrame) showUI = !showUI;
                if (k.rKey.wasPressedThisFrame) ResetToStart();
            }
            if (Mathf.Abs(player.position.x) > mapHalfExtent - 350f ||
                Mathf.Abs(player.position.z) > mapHalfExtent - 350f)
                ResetToStart();
        }

        private void LateUpdate()
        {
            if (fighter != null || player == null || passiveTargets == null) return;
            for (int i = 0; i < passiveTargets.Length; i++)
            {
                var target = passiveTargets[i];
                if (target == null) continue;
                Vector3 displacement = target.position - player.position;
                if (Vector3.Dot(displacement, player.forward) < -35f || displacement.sqrMagnitude > 360f * 360f)
                    PlaceTarget(target, i);
            }
        }

        private void ResetToStart()
        {
            if (playerBreakTurn != null) playerBreakTurn.CancelBreak();
            if (enemyBreakTurn != null) enemyBreakTurn.CancelBreak();
            if (missileLock != null) missileLock.ResetLock();
            player.SetPositionAndRotation(spawnPosition, spawnRotation);
            if (fighter != null) fighter.ResetEncounter();
            else PlaceAllTargets();
        }

        private void PlaceAllTargets()
        {
            if (passiveTargets == null) return;
            for (int i = 0; i < passiveTargets.Length; i++)
                if (passiveTargets[i] != null) PlaceTarget(passiveTargets[i], i);
        }

        private void PlaceTarget(Transform target, int index)
        {
            float ahead = 85f + 31f * index;
            float side = SideLanes[index % SideLanes.Length];
            Vector3 where = player.position + player.forward * ahead + player.right * side;
            where.y = player.position.y;
            target.SetPositionAndRotation(where, Quaternion.identity);
        }

        private void OnGUI()
        {
            if (!showUI || Event.current.type != EventType.Repaint) return;
            if (text == null)
                text = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };
            GUI.Box(new Rect(12, 12, 500, 129), GUIContent.none);
            string enemyStatus = fighter != null
                ? $"FIGHTER {fighter.Hp}/{fighter.MaxHp} HP   Kills {fighter.Kills}" : "NO ENEMY";
            GUI.Label(new Rect(22, 17, 475, 22), "WEAPON LAB / " + enemyStatus +
                " / B EVADE: " + (fighter != null ? fighter.EvasionLabel : "OFF"), text);
            GUI.Label(new Rect(22, 40, 475, 22), "A/D turn   W boost   Q/E break   LMB fire   1/2/3 guns", text);
            GUI.Label(new Rect(22, 63, 450, 22), "Gun " + cannon.FireModeLabel + "   Aim " + cannon.AimModeLabel +
                "   SFX " + cannon.SoundModeLabel, text);
            GUI.Label(new Rect(22, 86, 450, 22), "Look-ahead ON   R reset   H hide HUD   B enemy evasion", text);
            GUI.Label(new Rect(22, 109, 450, 22),
                $"Speed {flight.CurrentSpeed:F0}  Boost {boost.Charge01:P0}  Shots {cannon.FiredCount}  Hits {cannon.HitCount}", text);
        }
    }
}
