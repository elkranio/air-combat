#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace AirCombat.EditorTools
{
    // M1 one-time authoring. Creates two editable prefabs, then places ONE SAM.
    // Runtime never generates/overwrites the accepted assets.
    internal static class InstallSamTrial
    {
        private const string ScenePath = "Assets/_Project/Scenes/GroundAttackLab.unity";
        private const string PrefabFolder = "Assets/_Project/Prefabs/Enemies";
        private const string LauncherPath = PrefabFolder + "/SAM1Launcher.prefab";
        private const string MissilePath = PrefabFolder + "/SAM1Missile.prefab";
        private const string MaterialFolder = "Assets/_Project/Art/SAM1";
        private const string InstanceName = "SAM-01 Missile Launcher";

        [MenuItem("Air Combat/SAM1/Install Missile Tower Trial")]
        private static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { Debug.LogError("SAM1: Exit Play Mode first."); return; }
            Scene scene = SceneManager.GetActiveScene();
            if (SceneManager.sceneCount != 1 || scene.path != ScenePath || scene.isDirty)
            { Debug.LogError("SAM1: Open only SAVED GroundAttackLab in Edit Mode."); return; }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(LauncherPath) != null ||
                AssetDatabase.LoadAssetAtPath<GameObject>(MissilePath) != null ||
                (AssetDatabase.IsValidFolder(MaterialFolder) &&
                 AssetDatabase.FindAssets("t:Material", new[] { MaterialFolder }).Length > 0))
            { Debug.LogError("SAM1: Existing authored assets; refusing to overwrite."); return; }
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == InstanceName)
                { Debug.LogError("SAM1: Scene already contains the launcher."); return; }
            var sessions = UnityEngine.Object.FindObjectsByType<GroundAttackLabSession>(FindObjectsSortMode.None);
            if (sessions.Length != 1)
            { Debug.LogError("SAM1: Expected one GroundAttackLabSession."); return; }
            if (Shader.Find("Universal Render Pipeline/Lit") == null ||
                Shader.Find("AirCombat/SandboxEffect") == null)
            { Debug.LogError("SAM1: Missing URP Lit or SandboxEffect shader."); return; }

            EnsureFolder(PrefabFolder);
            EnsureFolder(MaterialFolder);
            Material foundation = Mat("Foundation", new Color(.28f, .34f, .31f));
            Material armor = Mat("Armor", new Color(.18f, .24f, .25f));
            Material trim = Mat("Trim", new Color(.48f, .55f, .49f));
            Material dark = Mat("Dark", new Color(.08f, .105f, .115f));
            Material orange = Mat("Warning", new Color(.91f, .47f, .11f));
            Material trail = new Material(Shader.Find("AirCombat/SandboxEffect")) { name = "SAM1_Trail" };
            AssetDatabase.CreateAsset(trail, MaterialFolder + "/SAM1_Trail.mat");

            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var missileRoot = new GameObject("SAM1Missile");
                SceneManager.MoveGameObjectToScene(missileRoot, preview);
                BuildMissile(missileRoot.transform, armor, trim, dark, orange, trail);
                bool saved;
                PrefabUtility.SaveAsPrefabAsset(missileRoot, MissilePath, out saved);
                if (!saved) throw new InvalidOperationException("SAM1: Couldn't save missile prefab.");

                var launcherRoot = new GameObject(InstanceName);
                SceneManager.MoveGameObjectToScene(launcherRoot, preview);
                Transform launchSocket = SamVerticalLauncherArt.Build(launcherRoot.transform,
                    foundation, armor, trim, dark, orange);
                var box = launcherRoot.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 4.1f, 0f);
                box.size = new Vector3(12f, 8.2f, 12f);
                var missile = AssetDatabase.LoadAssetAtPath<GameObject>(MissilePath);
                if (missile == null) throw new InvalidOperationException("SAM1: Missile prefab load failed.");
                launcherRoot.AddComponent<SurfaceToAirLauncher>().ConfigureArt(
                    launchSocket, missile.GetComponent<SurfaceToAirMissile>());
                PrefabUtility.SaveAsPrefabAsset(launcherRoot, LauncherPath, out saved);
                if (!saved) throw new InvalidOperationException("SAM1: Couldn't save launcher prefab.");
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LauncherPath);
            var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (instance == null) throw new InvalidOperationException("SAM1: Scene instantiate failed.");
            instance.transform.position = new Vector3(-320f, 0f, 215f);
            sessions[0].ConfigureMissileTower(instance.GetComponent<SurfaceToAirLauncher>());
            EditorUtility.SetDirty(sessions[0]);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("SAM1: Couldn't save GroundAttackLab.");
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = instance;
            Debug.Log("SAM1: Installed two prefabs and one launcher at (-320,0,215). " +
                "Do not rerun; commit prefab/material/scene/.meta files after Play Mode checks.");
        }

        private static Material Mat(string name, Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                { name = "SAM1_" + name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", .12f);
            AssetDatabase.CreateAsset(material, MaterialFolder + "/SAM1_" + name + ".mat");
            return material;
        }

        private static void BuildMissile(Transform root, Material armor, Material trim,
            Material dark, Material orange, Material trailMat)
        {
            var tube = Part(root, "Missile fuselage", PrimitiveType.Cylinder,
                new Vector3(0f, 0f, -.2f), new Vector3(.62f, 2.35f, .62f), trim);
            tube.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var nose = Part(root, "Nose cone", PrimitiveType.Capsule,
                new Vector3(0f, 0f, 2.35f), new Vector3(.5f, .6f, .5f), armor);
            nose.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Part(root, "Motor nozzle", PrimitiveType.Cylinder,
                new Vector3(0f, 0f, -2.35f), new Vector3(.62f, .3f, .62f), dark)
                .localRotation = Quaternion.Euler(90f, 0f, 0f);
            foreach (int side in new[] { -1, 1 })
            {
                Part(root, "Stabiliser X", PrimitiveType.Cube,
                    new Vector3(side * .63f, 0f, -1.75f), new Vector3(1f, .09f, 1.0f), armor);
                Part(root, "Stabiliser Y", PrimitiveType.Cube,
                    new Vector3(0f, side * .63f, -1.75f), new Vector3(.09f, 1f, 1f), armor);
            }
            Part(root, "Warhead marking", PrimitiveType.Cylinder,
                new Vector3(0f, 0f, 1.55f), new Vector3(.65f, .1f, .65f), orange)
                .localRotation = Quaternion.Euler(90f, 0f, 0f);
            var trailNode = Node(root, "Exhaust Trail", new Vector3(0f, 0f, -2.55f));
            var trail = trailNode.gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = trailMat;
            trail.time = .85f;
            trail.startWidth = 1.5f;
            trail.endWidth = .1f;
            trail.minVertexDistance = .8f;
            trail.startColor = new Color(1f, .8f, .25f, .8f);
            trail.endColor = new Color(1f, .35f, .08f, 0f);
            trail.emitting = false;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            root.gameObject.AddComponent<SurfaceToAirMissile>().ConfigureVisual(trail, trailMat);
        }

        private static Transform Node(Transform parent, string name, Vector3 position)
        {
            var child = new GameObject(name);
            SceneManager.MoveGameObjectToScene(child, parent.gameObject.scene);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = position;
            return child.transform;
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

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int split = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, split));
            AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
        }
    }
}
#endif
