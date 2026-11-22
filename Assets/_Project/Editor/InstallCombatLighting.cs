#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace AirCombat.EditorTools
{
    // One-time migration, deliberately not a scene regenerator. The created prefab
    // is the lighting source of truth for all future combat scenes.
    internal static class InstallCombatLighting
    {
        private const string PipelinePath = "Assets/Settings/ScorchedFrontier_URP.asset";
        private const string PrefabPath = "Assets/_Project/Prefabs/Environment/CombatLightingRig.prefab";
        private const string WeaponLab = "Assets/_Project/Scenes/WeaponLab.unity";
        private const string GroundLab = "Assets/_Project/Scenes/GroundAttackLab.unity";
        private const string SlopeMeshPath = "Assets/_Project/Art/GroundAttackLab/ShadowSlopeTest.asset";
        private const string GroundMaterialPath = "Assets/_Project/Art/GroundAttackLab/TestGround.mat";

        [MenuItem("Air Combat/Lighting/Install Unified Lighting L1")]
        private static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
            {
                Debug.LogError("L1: Exit Play Mode and save all open scenes first.");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null ||
                AssetDatabase.LoadAssetAtPath<Mesh>(SlopeMeshPath) != null)
            {
                Debug.LogError("L1: Generated assets already exist. Refusing to overwrite them.");
                return;
            }
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            var groundMaterial = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
            if (pipeline == null || groundMaterial == null ||
                AssetDatabase.LoadAssetAtPath<SceneAsset>(WeaponLab) == null ||
                AssetDatabase.LoadAssetAtPath<SceneAsset>(GroundLab) == null)
            {
                Debug.LogError("L1: Expected green scenes, ground material or URP profile are missing.");
                return;
            }

            // Validate both scenes before authoring. Never delete an unidentified light.
            foreach (string path in new[] { WeaponLab, GroundLab })
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                // Never probe arbitrary scene roots (Player/Enemy/etc.) for a Light.
                // Only the explicitly known legacy sun is eligible for replacement.
                if (!TryFindLegacySun(scene, path, out _)) return;
                if (scene.GetRootGameObjects().Any(go => go.name == "Combat Lighting"))
                {
                    Debug.LogError("L1: LightingRig already exists in " + path);
                    return;
                }
                if (path == GroundLab && scene.GetRootGameObjects().Any(go => go.name == "Shadow Slope - test only"))
                {
                    Debug.LogError("L1: GroundAttackLab already has a shadow test slope.");
                    return;
                }
            }

            EnsureFolder("Assets/_Project/Prefabs/Environment");
            Scene preview = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                root = new GameObject("Combat Lighting");
                SceneManager.MoveGameObjectToScene(root, preview);
                var sun = root.AddComponent<Light>();
                sun.type = LightType.Directional;
                root.AddComponent<CombatLightingRig>().ConfigureAuthoring(pipeline);
                bool success;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out success);
                if (!success) throw new InvalidOperationException("Unable to create CombatLightingRig prefab.");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) throw new InvalidOperationException("Lighting prefab failed to import.");
            foreach (string path in new[] { WeaponLab, GroundLab })
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                if (!TryFindLegacySun(scene, path, out var oldSun))
                    throw new InvalidOperationException("L1: Legacy sun changed during installation: " + path);
                var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
                if (instance == null) throw new InvalidOperationException("Cannot instantiate lighting prefab in " + path);
                // The old light must not coexist, otherwise we get double lighting/shadows.
                UnityEngine.Object.DestroyImmediate(oldSun);
                RenderSettings.sun = instance.GetComponent<Light>();
                if (path == GroundLab) CreateTestSlope(scene, groundMaterial);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new InvalidOperationException("Unable to save updated scene: " + path);
            }
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(prefab);
            Debug.Log("L1: Unified lighting installed in WeaponLab and GroundAttackLab. " +
                "Play GroundAttackLab: test player/bomb shadows and the test slope. " +
                "Commit generated prefab, mesh and Unity .meta files after green. Sandbox unchanged.");
        }

        private static bool TryFindLegacySun(Scene scene, string path, out GameObject oldSun)
        {
            oldSun = null;
            string expectedName = path == WeaponLab ? "Directional Light" : "Sun";
            var matches = scene.GetRootGameObjects().Where(go => go.name == expectedName).ToArray();
            if (matches.Length != 1)
            {
                Debug.LogError("L1: Expected exactly one root named '" + expectedName +
                    "' in " + path + ", found " + matches.Length + ". No scene objects changed.");
                return false;
            }
            // Unity's null semantics for Components differ from C# 'is' patterns;
            // verify the known light object explicitly, not Player or other roots.
            var light = matches[0].GetComponent<Light>();
            if (light == null || light.type != LightType.Directional)
            {
                Debug.LogError("L1: '" + expectedName + "' has no valid directional Light in " + path);
                return false;
            }
            oldSun = matches[0];
            return true;
        }

        private static void CreateTestSlope(Scene scene, Material material)
        {
            if (AssetDatabase.LoadAssetAtPath<Mesh>(SlopeMeshPath) != null)
                throw new InvalidOperationException("Shadow slope asset already exists.");
            var mesh = new Mesh { name = "L1 slope - ramp from 0 to 18m" };
            mesh.vertices = new[]
            {
                new Vector3(-32f, 0f, -36f), new Vector3(32f, 0f, -36f),
                new Vector3(-32f, 18f, 36f), new Vector3(32f, 18f, 36f)
            };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0, 1), new Vector2(1, 1) };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, SlopeMeshPath);
            var slope = new GameObject("Shadow Slope - test only");
            if (slope.scene != scene) SceneManager.MoveGameObjectToScene(slope, scene);
            slope.transform.position = new Vector3(170f, 0f, 420f);
            slope.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = slope.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            slope.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
#endif
