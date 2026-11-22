#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat.EditorTools
{
    // A4c: migrate into a NEW scene. Never modify or overwrite green WeaponLab.
    // The actual scene and prefab serialization is performed by Unity, not by YAML edits.
    internal static class CreateWeaponLabPrefabScene
    {
        private const string SourcePath = "Assets/_Project/Scenes/WeaponLab.unity";
        private const string OutputPath = "Assets/_Project/Scenes/WeaponLabPrefabs.unity";
        private const string PlayerPath = "Assets/_Project/Prefabs/Aircraft/PlayerJet.prefab";
        private const string EnemyPath = "Assets/_Project/Prefabs/Aircraft/EnemyJet.prefab";
        private const string CameraPath = "Assets/_Project/Prefabs/Camera/CombatCameraRig.prefab";

        [MenuItem("Air Combat/Prefabs/Create WeaponLab Prefab Scene")]
        private static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("A4c: Exit Play Mode before scene migration.");
                return;
            }
            Scene sourceScene = SceneManager.GetActiveScene();
            if (sourceScene.path != SourcePath)
            {
                Debug.LogError("A4c: Open WeaponLab.unity in Edit Mode before migration.");
                return;
            }
            if (sourceScene.isDirty)
            {
                Debug.LogError("A4c: Save or discard pending WeaponLab scene edits before migration.");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(OutputPath) != null)
            {
                Debug.LogError("A4c: WeaponLabPrefabs.unity already exists. Refusing to overwrite it.");
                return;
            }

            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            GameObject enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPath);
            GameObject cameraPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CameraPath);
            if (playerPrefab == null || enemyPrefab == null || cameraPrefab == null ||
                !ValidJet(playerPrefab, true) || !ValidJet(enemyPrefab, false) ||
                cameraPrefab.GetComponent<CombatCameraRig>() == null)
            {
                Debug.LogError("A4c: Missing or incomplete A4b role/camera prefabs. No scene created.");
                return;
            }
            if (GameObject.Find("Player") == null || GameObject.Find("Dogfight Opponent") == null ||
                GameObject.Find("Main Camera") == null ||
                UnityEngine.Object.FindFirstObjectByType<WeaponLabSession>() == null)
            {
                Debug.LogError("A4c: Green WeaponLab reference objects are missing. No scene created.");
                return;
            }

            try
            {
                // SaveAsCopy keeps the loaded source scene (and its .unity) unchanged.
                if (!EditorSceneManager.SaveScene(sourceScene, OutputPath, true))
                    throw new InvalidOperationException("Unable to copy WeaponLab.unity.");
                AssetDatabase.Refresh();
                Scene scene = EditorSceneManager.OpenScene(OutputPath, OpenSceneMode.Single);
                ConvertScene(scene, playerPrefab, enemyPrefab, cameraPrefab);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new InvalidOperationException("Could not save converted scene.");
                AssetDatabase.SaveAssets();
                Debug.Log("A4c: WeaponLabPrefabs.unity created using PlayerJet / EnemyJet / " +
                    "CombatCameraRig instances. Original WeaponLab.unity unchanged. " +
                    "Test the OPEN converted scene before committing its asset and .meta.");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                // Never leave an incomplete converted scene masquerading as valid.
                try
                {
                    EditorSceneManager.OpenScene(SourcePath, OpenSceneMode.Single);
                    AssetDatabase.DeleteAsset(OutputPath);
                    AssetDatabase.Refresh();
                }
                catch (Exception cleanup)
                {
                    Debug.LogException(cleanup);
                    Debug.LogError("A4c: Manually delete incomplete " + OutputPath);
                }
            }
        }

        private static bool ValidJet(GameObject prefab, bool player)
        {
            var assembly = prefab.GetComponent<JetAssembly>();
            if (assembly == null || !assembly.IsConfigured) return false;
            return player
                ? prefab.GetComponent<FlightController>() != null &&
                  prefab.GetComponent<Afterburner>() != null &&
                  prefab.GetComponent<WeaponController>() != null
                : prefab.GetComponent<EnemyFlightController>() != null &&
                  prefab.GetComponent<WeaponLabEnemy>() != null &&
                  prefab.GetComponent<WeaponLabEvasion>() != null;
        }

        private static void ConvertScene(Scene scene, GameObject playerPrefab,
            GameObject enemyPrefab, GameObject cameraPrefab)
        {
            GameObject oldPlayer = GameObject.Find("Player");
            GameObject oldEnemy = GameObject.Find("Dogfight Opponent");
            GameObject oldCamera = GameObject.Find("Main Camera");
            var session = UnityEngine.Object.FindFirstObjectByType<WeaponLabSession>();
            if (oldPlayer == null || oldEnemy == null || oldCamera == null || session == null ||
                oldPlayer.GetComponent<WeaponLabCannon>() == null)
                throw new InvalidOperationException("Copied WeaponLab is not the expected green scene.");

            var player = InstantiateRole(playerPrefab, scene, oldPlayer, "Player");
            var enemy = InstantiateRole(enemyPrefab, scene, oldEnemy, "Dogfight Opponent");
            var camera = InstantiateRole(cameraPrefab, scene, oldCamera, "Main Camera");

            var flight = Require<FlightController>(player);
            var boost = Require<Afterburner>(player);
            var weapon = Require<WeaponController>(player);
            var enemyFlight = Require<EnemyFlightController>(enemy);
            var fighter = Require<WeaponLabEnemy>(enemy);
            var evasion = Require<WeaponLabEvasion>(enemy);
            var cameraRig = Require<CombatCameraRig>(camera);
            if (cameraRig.ViewCamera == null || cameraRig.Lock == null ||
                !Require<JetAssembly>(player).IsConfigured ||
                !Require<JetAssembly>(enemy).IsConfigured)
                throw new InvalidOperationException("Incomplete prefab instance after instantiation.");

            // These are SCENE relationships, intentionally absent from role prefabs.
            // Do not use EnemyFlightController.Configure(): it also clears afterburner/evasion.
            SetReference(enemyFlight, "target", player.transform);
            SetReference(fighter, "player", player.transform);
            SetReference(evasion, "player", player.transform);
            SetReference(session, "player", player.transform);
            SetReference(session, "flight", flight);
            SetReference(session, "boost", boost);
            SetReference(session, "cannon", weapon);
            SetReference(session, "fighter", fighter);

            // A4b's visual prefab uses the player's shared Kestrel art. Restore the
            // approved enemy material overrides from the original scene instance
            // on this enemy INSTANCE, without mutating Kestrel or EnemyJet assets.
            CopyEnemyMaterials(oldEnemy, Require<JetAssembly>(enemy).Chassis);

            // The prefab camera is preconfigured but has no cross-scene target;
            // WeaponLabSession binds its camera rig on entering Play Mode.
            // Do not serialize source scene target references into shared prefabs.
            UnityEngine.Object.DestroyImmediate(oldPlayer);
            UnityEngine.Object.DestroyImmediate(oldEnemy);
            UnityEngine.Object.DestroyImmediate(oldCamera);

            if (!PrefabUtility.IsPartOfPrefabInstance(player) ||
                !PrefabUtility.IsPartOfPrefabInstance(enemy) ||
                !PrefabUtility.IsPartOfPrefabInstance(camera) ||
                session.GetComponent<JetAssembly>() != null)
                throw new InvalidOperationException("Scene objects were not retained as prefab instances.");
        }

        private static GameObject InstantiateRole(GameObject prefab, Scene scene,
            GameObject source, string name)
        {
            var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (instance == null) throw new InvalidOperationException("Failed to instantiate " + prefab.name);
            Transform root = instance.transform;
            Transform old = source.transform;
            if (old.parent != null) root.SetParent(old.parent, true);
            root.SetPositionAndRotation(old.position, old.rotation);
            root.localScale = old.localScale;
            root.SetSiblingIndex(old.GetSiblingIndex());
            instance.name = name;
            PrefabUtility.RecordPrefabInstancePropertyModifications(root);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
            return instance;
        }

        private static void CopyEnemyMaterials(GameObject oldEnemy, JetChassis newChassis)
        {
            var oldRenderers = oldEnemy.GetComponentsInChildren<Renderer>(true);
            var byName = new Dictionary<string, Renderer>(StringComparer.Ordinal);
            foreach (var renderer in oldRenderers)
            {
                if (!byName.TryAdd(renderer.gameObject.name, renderer))
                    throw new InvalidOperationException("Duplicate enemy renderer: " + renderer.name);
            }
            var newRenderers = newChassis.GetComponentsInChildren<Renderer>(true);
            if (newRenderers.Length != oldRenderers.Length)
                throw new InvalidOperationException("Enemy renderers differ from green scene: " +
                    oldRenderers.Length + " vs " + newRenderers.Length);
            foreach (var renderer in newRenderers)
            {
                if (!byName.TryGetValue(renderer.gameObject.name, out var reference))
                    throw new InvalidOperationException("Enemy renderer missing: " + renderer.name);
                renderer.sharedMaterials = reference.sharedMaterials;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
        }

        private static T Require<T>(GameObject gameObject) where T : Component
        {
            var value = gameObject.GetComponent<T>();
            if (value == null) throw new InvalidOperationException(gameObject.name + " is missing " + typeof(T).Name);
            return value;
        }

        private static void SetReference(UnityEngine.Object component, string property, UnityEngine.Object target)
        {
            var so = new SerializedObject(component);
            var field = so.FindProperty(property);
            if (field == null) throw new InvalidOperationException(component.name + " is missing " + property);
            field.objectReferenceValue = target;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (PrefabUtility.IsPartOfPrefabInstance(component))
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }
    }
}
#endif
