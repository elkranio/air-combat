#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat.EditorTools
{
    // One-time non-destructive export from the green WeaponLab reference.
    // Creates role prefabs in an isolated preview scene. No scene replacement.
    internal static class AuthorJetPrefabs
    {
        private const string SourceScene = "Assets/_Project/Scenes/WeaponLab.unity";
        private const string ChassisPath = "Assets/_Project/Prefabs/Chassis/Kestrel.prefab";
        private const string RootFolder = "Assets/_Project/Prefabs";
        private const string RoleFolder = RootFolder + "/Aircraft";
        private const string CameraFolder = RootFolder + "/Camera";
        private const string PlayerPath = RoleFolder + "/PlayerJet.prefab";
        private const string EnemyPath = RoleFolder + "/EnemyJet.prefab";
        private const string CameraPath = CameraFolder + "/CombatCameraRig.prefab";
        private const string MenuPath = "Air Combat/Prefabs/Author Jet Prefabs";

        [MenuItem(MenuPath)]
        private static void Author()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { Debug.LogError("Exit Play Mode before authoring jet prefabs."); return; }
            if (SceneManager.GetActiveScene().path != SourceScene)
            { Debug.LogError("Open the existing WeaponLab.unity before authoring role prefabs."); return; }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath) != null ||
                AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPath) != null ||
                AssetDatabase.LoadAssetAtPath<GameObject>(CameraPath) != null)
            { Debug.LogError("One or more role prefabs already exist. Refusing to overwrite assets."); return; }

            var chassis = AssetDatabase.LoadAssetAtPath<GameObject>(ChassisPath);
            var chassisComponent = chassis != null ? chassis.GetComponent<JetChassis>() : null;
            var sourcePlayer = GameObject.Find("Player");
            var sourceEnemy = GameObject.Find("Dogfight Opponent");
            var sourceCamera = GameObject.Find("Main Camera");
            if (chassisComponent == null || !chassisComponent.HasRequiredSockets ||
                sourcePlayer == null || sourceEnemy == null || sourceCamera == null ||
                sourcePlayer.GetComponent<WeaponLabCannon>() == null ||
                sourceEnemy.GetComponent<WeaponLabEnemy>() == null ||
                sourceCamera.GetComponent<TopDownCameraFollow>() == null)
            { Debug.LogError("Missing Kestrel hardpoints or green WeaponLab source objects. Nothing created."); return; }

            EnsureFolder(RootFolder);
            EnsureFolder(RoleFolder);
            EnsureFolder(CameraFolder);
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var player = CloneToPreview(sourcePlayer, preview, "PlayerJet");
                var playerVisual = ReplaceVisual(player, chassis, preview);
                var oldWeapon = player.GetComponent<WeaponLabCannon>();
                var snapshot = WeaponSettings.Capture(oldWeapon);
                UnityEngine.Object.DestroyImmediate(oldWeapon);
                var weapon = player.AddComponent<WeaponController>();
                snapshot.Restore(weapon);
                Require<FlightController>(player);
                SetReference(player.GetComponent<FlightController>(), "visual", playerVisual.VisualRoot);
                AddIfMissing<BoundedAim>(player);
                AddIfMissing<WeaponSelection>(player);
                AddIfMissing<BreakTurn>(player);
                AddIfMissing<GuidedMissileLauncher>(player);
                Save(player, PlayerPath);

                var enemy = CloneToPreview(sourceEnemy, preview, "EnemyJet");
                var enemyVisual = ReplaceVisual(enemy, chassis, preview);
                var flight = Require<EnemyFlightController>(enemy);
                var fighter = Require<WeaponLabEnemy>(enemy);
                var evasion = Require<WeaponLabEvasion>(enemy);
                SetReference(flight, "visual", enemyVisual.VisualRoot);
                SetReference(flight, "target", null); // linked by the mission, never by the asset
                SetReference(fighter, "player", null);
                SetReference(evasion, "player", null);
                var parts = enemyVisual.Chassis.GetComponentsInChildren<Renderer>(true);
                SetReferences(fighter, "visibleParts", parts);
                AddIfMissing<BreakTurn>(enemy);
                Save(enemy, EnemyPath);

                var camera = CloneToPreview(sourceCamera, preview, "CombatCameraRig");
                SetReference(Require<TopDownCameraFollow>(camera), "target", null);
                SetReference(camera.GetComponent<TopDownCameraFollow>(), "afterburner", null);
                var targetLock = Require<TargetLock>(camera);
                SetReference(targetLock, "target", null);
                SetFloat(targetLock, "lockSeconds", 1f); // approved WeaponLab runtime timing
                SetFloat(targetLock, "graceSeconds", .5f);
                AddIfMissing<CameraLookAhead>(camera);
                AddIfMissing<TargetLockHud>(camera);
                AddIfMissing<CombatCameraRig>(camera);
                Save(camera, CameraPath);

                AssetDatabase.SaveAssets();
                Debug.Log("Created PlayerJet, EnemyJet and CombatCameraRig prefabs with nested Kestrel. " +
                    "WeaponLab is unchanged. Commit the generated assets and .meta files after inspection.");
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                // All paths were checked absent up front; clear even partial saves.
                AssetDatabase.DeleteAsset(PlayerPath);
                AssetDatabase.DeleteAsset(EnemyPath);
                AssetDatabase.DeleteAsset(CameraPath);
                Debug.LogError("Prefab authoring failed; incomplete output removed. WeaponLab was not modified.");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static GameObject CloneToPreview(GameObject source, Scene preview, string name)
        {
            var clone = UnityEngine.Object.Instantiate(source);
            SceneManager.MoveGameObjectToScene(clone, preview);
            clone.name = name;
            clone.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            clone.transform.localScale = Vector3.one;
            return clone;
        }

        private static JetAssembly ReplaceVisual(GameObject jet, GameObject chassisAsset, Scene preview)
        {
            var old = jet.transform.Find("JetVisual");
            var oldVfx = old != null ? old.GetComponent<AfterburnerVfx>() : null;
            if (old == null || oldVfx == null)
                throw new InvalidOperationException("Missing JetVisual/AfterburnerVfx on " + jet.name);
            var visual = new GameObject("JetVisual");
            SceneManager.MoveGameObjectToScene(visual, preview);
            visual.transform.SetParent(jet.transform, false);
            visual.transform.localPosition = old.localPosition;
            visual.transform.localRotation = old.localRotation;
            visual.transform.localScale = old.localScale;

            // Preserve boost-material/audio settings and the cloned root's tank reference.
            var newVfx = visual.AddComponent<AfterburnerVfx>();
            EditorUtility.CopySerialized(oldVfx, newVfx);
            var nested = (GameObject)PrefabUtility.InstantiatePrefab(chassisAsset, preview);
            nested.transform.SetParent(visual.transform, false);
            nested.transform.localPosition = Vector3.zero;
            nested.transform.localRotation = Quaternion.identity;
            nested.transform.localScale = Vector3.one;
            var chassis = nested.GetComponent<JetChassis>();
            if (chassis == null || !chassis.HasRequiredSockets)
                throw new InvalidOperationException("Kestrel prefab has invalid hardpoints.");
            newVfx.BindChassis(chassis);
            UnityEngine.Object.DestroyImmediate(old.gameObject);

            var assembly = jet.GetComponent<JetAssembly>();
            if (assembly == null) assembly = jet.AddComponent<JetAssembly>();
            assembly.SetAuthoringReferences(visual.transform, chassis);
            if (!assembly.IsConfigured) throw new InvalidOperationException("JetAssembly failed validation.");
            return assembly;
        }

        private static T AddIfMissing<T>(GameObject root) where T : Component
        {
            var component = root.GetComponent<T>();
            return component != null ? component : root.AddComponent<T>();
        }

        private static T Require<T>(GameObject root) where T : Component
        {
            var result = root.GetComponent<T>();
            if (result == null) throw new InvalidOperationException(root.name + " lacks " + typeof(T).Name);
            return result;
        }

        private static void SetReference(UnityEngine.Object component, string field, UnityEngine.Object value)
        {
            var so = new SerializedObject(component);
            var property = so.FindProperty(field);
            if (property == null) throw new InvalidOperationException(component.name + " lacks " + field);
            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(UnityEngine.Object component, string field, float value)
        {
            var so = new SerializedObject(component);
            var property = so.FindProperty(field);
            if (property == null) throw new InvalidOperationException(component.name + " lacks " + field);
            property.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetReferences(UnityEngine.Object component, string field, Renderer[] values)
        {
            var so = new SerializedObject(component);
            var property = so.FindProperty(field);
            if (property == null || !property.isArray)
                throw new InvalidOperationException(component.name + " lacks " + field + " array");
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Save(GameObject root, string path)
        {
            bool success;
            var output = PrefabUtility.SaveAsPrefabAsset(root, path, out success);
            if (!success || output == null)
                throw new InvalidOperationException("Could not save " + path);
            var stored = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (stored == null) throw new InvalidOperationException("Missing output " + path);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            int slash = folder.LastIndexOf('/');
            var parent = folder.Substring(0, slash);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder.Substring(slash + 1));
        }

        // Avoid serializing the WeaponLabCannon script into the reusable role prefab.
        // Snapshot every shared inspector tuning field before replacing the component.
        private sealed class WeaponSettings
        {
            private Material tracer;
            private AudioClip shot;
            private AudioClip[] variants;
            private float[] floats;
            private int burst;
            private static readonly string[] FloatFields =
            {
                "roundsPerSecond", "burstCooldown", "gaussCooldown", "gaussVelocity", "gaussRange",
                "gaussSightLength", "muzzleVelocity", "maxRange", "tracerLength", "soundVolume"
            };

            public static WeaponSettings Capture(WeaponLabCannon gun)
            {
                var so = new SerializedObject(gun);
                var result = new WeaponSettings
                {
                    tracer = (Material)so.FindProperty("tracerMaterial").objectReferenceValue,
                    shot = (AudioClip)so.FindProperty("shotClip").objectReferenceValue,
                    burst = so.FindProperty("burstSize").intValue,
                    floats = new float[FloatFields.Length]
                };
                var clips = so.FindProperty("soundVariants");
                result.variants = new AudioClip[clips.arraySize];
                for (int i = 0; i < clips.arraySize; i++)
                    result.variants[i] = (AudioClip)clips.GetArrayElementAtIndex(i).objectReferenceValue;
                for (int i = 0; i < FloatFields.Length; i++)
                    result.floats[i] = so.FindProperty(FloatFields[i]).floatValue;
                return result;
            }

            public void Restore(WeaponController gun)
            {
                var so = new SerializedObject(gun);
                so.FindProperty("tracerMaterial").objectReferenceValue = tracer;
                so.FindProperty("shotClip").objectReferenceValue = shot;
                so.FindProperty("burstSize").intValue = burst;
                var clips = so.FindProperty("soundVariants");
                clips.arraySize = variants.Length;
                for (int i = 0; i < variants.Length; i++)
                    clips.GetArrayElementAtIndex(i).objectReferenceValue = variants[i];
                for (int i = 0; i < FloatFields.Length; i++)
                    so.FindProperty(FloatFields[i]).floatValue = floats[i];
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
#endif
