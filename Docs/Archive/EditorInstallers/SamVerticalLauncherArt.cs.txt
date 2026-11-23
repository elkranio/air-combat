#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace AirCombat.EditorTools
{
    // Shared model authoring for fresh SAM installations and M1b upgrades.
    // Model is saved into the existing prefab, not rebuilt at runtime.
    internal static class SamVerticalLauncherArt
    {
        public static Transform Build(Transform root, Material foundation, Material armor,
            Material trim, Material dark, Material warning)
        {
            Part(root, "Concrete foundation", PrimitiveType.Cube,
                new Vector3(0f, .35f, 0f), new Vector3(14.5f, .7f, 14.5f), foundation);
            var array = Node(root, "Vertical Launch Array", Vector3.zero);
            Part(array, "Armoured equipment block", PrimitiveType.Cube,
                new Vector3(0f, 1.75f, 0f), new Vector3(11.5f, 2.8f, 11.5f), armor);
            Part(array, "Roof deck", PrimitiveType.Cube,
                new Vector3(0f, 3.15f, 0f), new Vector3(12.2f, .35f, 12.2f), trim);
            foreach (float x in new[] { -2.65f, 2.65f })
            foreach (float z in new[] { -2.65f, 2.65f })
            {
                // Four dark open mouths, thick armour and readable orange labels.
                var cell = Node(array, "Vertical launch cell", new Vector3(x, 0f, z));
                Part(cell, "Canister armour", PrimitiveType.Cube,
                    new Vector3(0f, 5.20f, 0f), new Vector3(4.5f, 3.9f, 4.5f), armor);
                Part(cell, "Launcher tube", PrimitiveType.Cylinder,
                    new Vector3(0f, 5.3f, 0f), new Vector3(3.5f, 2.1f, 3.5f), trim);
                Part(cell, "Upper collar", PrimitiveType.Cylinder,
                    new Vector3(0f, 7.35f, 0f), new Vector3(4.0f, .21f, 4.0f), dark);
                Part(cell, "Open launcher mouth", PrimitiveType.Cylinder,
                    new Vector3(0f, 7.54f, 0f), new Vector3(2.85f, .12f, 2.85f), dark);
                Part(cell, "Warning stripe", PrimitiveType.Cube,
                    new Vector3(0f, 6.85f, -2.29f), new Vector3(2.4f, .27f, .08f), warning);
            }
            Part(array, "Central service trunk", PrimitiveType.Cube,
                new Vector3(0f, 5.7f, 0f), new Vector3(.45f, 3.2f, .45f), dark);
            // Rear of rocket begins inside the open mouth, tip above the array.
            var muzzle = Node(array, "Launch muzzle", new Vector3(-2.65f, 8.25f, -2.65f));
            muzzle.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
            return muzzle;
        }

        private static Transform Node(Transform parent, string name, Vector3 position)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, parent.gameObject.scene);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            return go.transform;
        }

        private static Transform Part(Transform parent, string name, PrimitiveType type,
            Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            SceneManager.MoveGameObjectToScene(go, parent.gameObject.scene);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            var collider = go.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return go.transform;
        }
    }

    // Existing M1 install has already been committed. Preserve that prefab GUID
    // and the scene instance; never run the original install command again.
    internal static class UpgradeSamVerticalLauncher
    {
        private const string LauncherPath = "Assets/_Project/Prefabs/Enemies/SAM1Launcher.prefab";
        private const string MissilePath = "Assets/_Project/Prefabs/Enemies/SAM1Missile.prefab";
        private const string MaterialFolder = "Assets/_Project/Art/SAM1/SAM1_";

        [MenuItem("Air Combat/SAM1b/Upgrade Existing Launcher to Vertical")]
        private static void Upgrade()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { Debug.LogError("SAM1b: Exit Play Mode first."); return; }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LauncherPath);
            var missilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MissilePath);
            if (prefab == null || missilePrefab == null ||
                prefab.GetComponent<SurfaceToAirLauncher>() == null ||
                missilePrefab.GetComponent<SurfaceToAirMissile>() == null)
            { Debug.LogError("SAM1b: Existing SAM1 prefab assets are required."); return; }
            if (prefab.transform.Find("Vertical Launch Array") != null)
            { Debug.LogWarning("SAM1b: Prefab is already vertical. Upgrade skipped."); return; }
            Material foundation = Mat("Foundation"), armor = Mat("Armor"), trim = Mat("Trim"),
                dark = Mat("Dark"), warning = Mat("Warning");
            if (foundation == null || armor == null || trim == null || dark == null || warning == null)
            { Debug.LogError("SAM1b: One or more SAM1 materials are missing."); return; }

            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(LauncherPath);
                // Replace only visual children: preserve root prefab, collider,
                // SAM behaviour component and all scene-instance references.
                for (int i = root.transform.childCount - 1; i >= 0; --i)
                    UnityEngine.Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
                Transform muzzle = SamVerticalLauncherArt.Build(root.transform,
                    foundation, armor, trim, dark, warning);
                root.GetComponent<SurfaceToAirLauncher>().ConfigureArt(muzzle,
                    missilePrefab.GetComponent<SurfaceToAirMissile>());
                var hitbox = root.GetComponent<BoxCollider>();
                if (hitbox != null)
                {
                    hitbox.center = new Vector3(0f, 4.1f, 0f);
                    hitbox.size = new Vector3(12f, 8.2f, 12f);
                }
                bool saved;
                PrefabUtility.SaveAsPrefabAsset(root, LauncherPath, out saved);
                if (!saved) throw new InvalidOperationException("SAM1b: Failed to save launcher prefab.");
            }
            finally
            {
                if (root != null) PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("SAM1b: Upgraded the existing launcher prefab in place. " +
                "GroundAttackLab keeps its original instance and links. Commit the modified prefab.");
        }

        private static Material Mat(string suffix) => AssetDatabase.LoadAssetAtPath<Material>(
            MaterialFolder + suffix + ".mat");
    }
}
#endif
