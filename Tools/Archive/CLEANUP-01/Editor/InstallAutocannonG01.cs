#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    // One-time composition: profile + module prefab + main-map player override.
    // Existing PlayerJet, EnemyJet and both regression labs are untouched.
    public static class InstallAutocannonG01
    {
        private const string MainPath = "Assets/_Project/Scenes/ArcadeDesertE1.unity";
        private const string Folder = "Assets/_Project/Prefabs/Weapons";
        private const string PresetPath = "Assets/_Project/Prefabs/Weapons/AC01_Burst.asset";
        private const string ModulePath = "Assets/_Project/Prefabs/Weapons/AC01_Autocannon.prefab";

        [MenuItem("Air Combat/Weapons/GUN-01 - Install Forward Autocannon (Main Map)")]
        private static void Install()
        {
            try { Debug.Log(InstallMainMap()); }
            catch (Exception e) { Debug.LogError("GUN-01: " + e); }
        }

        private static string InstallMainMap()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before installing GUN-01.");
            var scene = SceneManager.GetActiveScene();
            if (!scene.isLoaded || scene.path != MainPath)
                throw new InvalidOperationException("Open ArcadeDesertE1.unity first.");
            if (scene.isDirty)
                throw new InvalidOperationException("Save or discard scene edits before GUN-01.");

            E1MapFlightSession session = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                session = root.GetComponentInChildren<E1MapFlightSession>(true);
                if (session != null) break;
            }
            if (session == null) throw new InvalidOperationException("Main-map flight session not found.");
            var so = new SerializedObject(session);
            var player = so.FindProperty("player").objectReferenceValue as JetAssembly;
            if (player == null || !player.IsConfigured)
                throw new InvalidOperationException("Main-map player JetAssembly missing.");
            if (player.GetComponentInChildren<AutocannonWeapon>(true) != null ||
                player.GetComponent<PlayerWeaponTrigger>() != null)
                throw new InvalidOperationException("GUN-01 already installed; will not duplicate the weapon.");
            var oldWeapon = player.GetComponent<WeaponController>();
            if (oldWeapon == null)
                throw new InvalidOperationException("PlayerJet legacy WeaponController is missing; cannot migrate baseline.");
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(PresetPath) != null ||
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(ModulePath) != null)
                throw new InvalidOperationException("GUN-01 assets already exist; refusing to overwrite them.");

            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "Weapons");

            // Copy existing serialized settings instead of silently changing feel.
            var legacy = new SerializedObject(oldWeapon);
            var variants = legacy.FindProperty("soundVariants");
            var clips = new AudioClip[variants.arraySize];
            for (int i = 0; i < clips.Length; i++)
                clips[i] = variants.GetArrayElementAtIndex(i).objectReferenceValue as AudioClip;
            var preset = ScriptableObject.CreateInstance<AutocannonPreset>();
            preset.SetAuthoringBaseline(
                legacy.FindProperty("tracerMaterial").objectReferenceValue as Material,
                legacy.FindProperty("shotClip").objectReferenceValue as AudioClip,
                clips,
                legacy.FindProperty("roundsPerSecond").floatValue,
                legacy.FindProperty("burstSize").intValue,
                legacy.FindProperty("burstCooldown").floatValue,
                legacy.FindProperty("muzzleVelocity").floatValue,
                legacy.FindProperty("maxRange").floatValue,
                legacy.FindProperty("tracerLength").floatValue,
                legacy.FindProperty("soundVolume").floatValue);
            if (preset.TracerMaterial == null)
            {
                UnityEngine.Object.DestroyImmediate(preset);
                throw new InvalidOperationException("Existing player cannon has no tracer material.");
            }
            AssetDatabase.CreateAsset(preset, PresetPath);

            var moduleRoot = new GameObject("AC-01 Autocannon Mount");
            try
            {
                moduleRoot.AddComponent<AutocannonEmitter>();
                moduleRoot.AddComponent<AutocannonWeapon>().SetAuthoringPreset(preset);
                PrefabUtility.SaveAsPrefabAsset(moduleRoot, ModulePath);
            }
            finally { UnityEngine.Object.DestroyImmediate(moduleRoot); }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModulePath);
            if (prefab == null) throw new InvalidOperationException("Autocannon prefab was not created.");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.SetTransformParent(instance.transform, player.transform, "Mount AC-01 on player");
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            var mount = instance.GetComponent<AutocannonWeapon>();
            var input = Undo.AddComponent<PlayerWeaponTrigger>(player.gameObject);
            input.SetAuthoringWeapon(mount);
            EditorUtility.SetDirty(input);

            // The shared player prefab remains unchanged; avoid two concurrent
            // autocannons in E1. Labs still use the old WeaponController as-is.
            Undo.RecordObject(oldWeapon, "Disable legacy E1 cannon");
            oldWeapon.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(oldWeapon);
            PrefabUtility.RecordPrefabInstancePropertyModifications(input);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, scene.path))
                throw new InvalidOperationException("Could not save ArcadeDesertE1.unity.");
            AssetDatabase.SaveAssets();
            return "GUN-01 installed. LMB fires AC-01 nose-forward in ArcadeDesertE1; labs and shared PlayerJet untouched.";
        }
    }
}
#endif
