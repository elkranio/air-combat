#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace AirCombat.EditorTools
{
    // One-shot, fail-closed creation of an independent prefab-composed flight scene.
    // Unlike old WeaponLab generators, it never edits an existing scene or prefab.
    internal static class CreateGroundAttackLab
    {
        private const string ReferenceScene = "Assets/_Project/Scenes/WeaponLab.unity";
        private const string OutputScene = "Assets/_Project/Scenes/GroundAttackLab.unity";
        private const string PlayerPath = "Assets/_Project/Prefabs/Aircraft/PlayerJet.prefab";
        private const string CameraPath = "Assets/_Project/Prefabs/Camera/CombatCameraRig.prefab";
        private const string GroundFolder = "Assets/_Project/Art/GroundAttackLab";
        private const string GroundMaterialPath = GroundFolder + "/TestGround.mat";

        [MenuItem("Air Combat/Scenes/Create GroundAttackLab G1")]
        private static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("G1: Exit Play Mode before creating the new scene.");
                return;
            }
            var source = SceneManager.GetActiveScene();
            if (source.path != ReferenceScene || source.isDirty)
            {
                Debug.LogError("G1: Open the clean WeaponLab.unity in Edit Mode first.");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(OutputScene) != null ||
                AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath) != null)
            {
                Debug.LogError("G1: Output scene/material already exists; refusing to overwrite.");
                return;
            }
            var playerAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            var cameraAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CameraPath);
            if (playerAsset == null || cameraAsset == null ||
                playerAsset.GetComponent<JetAssembly>() == null ||
                !playerAsset.GetComponent<JetAssembly>().IsConfigured ||
                playerAsset.GetComponent<WeaponController>() == null ||
                cameraAsset.GetComponent<CombatCameraRig>() == null)
            {
                Debug.LogError("G1: Required approved prefabs are missing/incomplete.");
                return;
            }
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("G1: URP Lit shader not found; check the project's render pipeline.");
                return;
            }

            try
            {
                // New empty scene: no scene YAML copies, no WeaponLabSession, no enemy.
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.55f, .52f, .47f);
                RenderSettings.fog = false;

                var sun = new GameObject("Sun");
                var directional = sun.AddComponent<Light>();
                directional.type = LightType.Directional;
                directional.intensity = 1.1f;
                directional.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

                EnsureFolder(GroundFolder);
                var groundMaterial = new Material(shader) { name = "G1 Test Ground" };
                groundMaterial.SetColor("_BaseColor", new Color(.43f, .39f, .33f, 1f));
                groundMaterial.SetFloat("_Smoothness", .08f);
                AssetDatabase.CreateAsset(groundMaterial, GroundMaterialPath);
                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "Flat ground - no damage target";
                ground.transform.position = Vector3.zero;
                ground.transform.localScale = new Vector3(1200f, 1f, 1200f);
                ground.GetComponent<MeshRenderer>().sharedMaterial = groundMaterial;

                var player = (GameObject)PrefabUtility.InstantiatePrefab(playerAsset, scene);
                var camera = (GameObject)PrefabUtility.InstantiatePrefab(cameraAsset, scene);
                if (player == null || camera == null)
                    throw new InvalidOperationException("Could not instantiate PlayerJet / CombatCameraRig.");
                player.transform.SetPositionAndRotation(new Vector3(0f, 48f, 0f), Quaternion.identity);
                camera.transform.SetPositionAndRotation(new Vector3(0f, 153f, -48f),
                    Quaternion.Euler(65f, 0f, 0f));
                PrefabUtility.RecordPrefabInstancePropertyModifications(player.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(camera.transform);

                var composition = new GameObject("GroundAttackLab Session").AddComponent<GroundAttackLabSession>();
                composition.Configure(player.GetComponent<JetAssembly>(), camera.GetComponent<CombatCameraRig>());
                if (composition.GetComponent<GroundAttackLabSession>() == null)
                    throw new InvalidOperationException("GroundAttackLabSession was not installed.");
                if (!PrefabUtility.IsPartOfPrefabInstance(player) ||
                    !PrefabUtility.IsPartOfPrefabInstance(camera))
                    throw new InvalidOperationException("The flight objects are not prefab instances.");

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, OutputScene))
                    throw new InvalidOperationException("Unable to save GroundAttackLab scene.");
                AssetDatabase.SaveAssets();
                Debug.Log("G1: Created GroundAttackLab.unity. Test it in Play Mode, then stage " +
                    "the scene, material and Unity-generated .meta files. WeaponLab/Sandbox untouched.");
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(OutputScene));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorSceneManager.OpenScene(ReferenceScene, OpenSceneMode.Single);
                AssetDatabase.DeleteAsset(OutputScene);
                AssetDatabase.DeleteAsset(GroundMaterialPath);
                Debug.LogError("G1: Creation failed, partial outputs removed. Existing scenes unchanged.");
            }
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
