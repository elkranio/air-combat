#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AirCombat
{
    // B1a: one reusable barracks prefab, three instances and a scene-only flight
    // approach. Never regenerate or replace the authored Terrain / E1c plateau.
    public static class InstallBarracksB1A
    {
        private const string ScenePath = "Assets/_Project/Scenes/ArcadeDesertE1.unity";
        private const string ModelPath = "Assets/_Project/Art/Buildings/Base/Barracks_B1a";
        private const string PrefabFolder = "Assets/_Project/Prefabs/Environment/Base";
        private const string PrefabPath = PrefabFolder + "/Barracks_B1a.prefab";
        private const string BaseRootName = "B1 / Base sector - barracks trial";

        // One new sector, far enough from the plateau at roughly (110, 135).
        // Row aligned across X; buildings' long axes are parallel to Z.
        private static readonly Vector3 BarracksCenter = new Vector3(-485f, 0f, -270f);
        // C3 view from Y150: the center of this row is close to the center of
        // the first Game view, while the aircraft approaches along (-X,+Z).
        private static readonly Vector3 ApproachSpawn = new Vector3(-362f, 150f, -393f);
        private static readonly Vector3 ApproachHeading = new Vector3(-1f, 0f, 1f);
        private const float CameraHeightAboveJet = 88f;
        private const float CameraOffsetZ = -90f;

        private static readonly (string Section, string Hex)[] Palette =
        {
            ("Walls", "#B8C2B9"),
            ("Roof", "#53666B"),
            ("Openings", "#293B42"),
            ("Trim", "#777F79")
        };

        [MenuItem("Air Combat/Art/B1a - Install Three Barracks + Base Approach")]
        private static void InstallFromMenu()
        {
            try { Debug.Log(Install()); }
            catch (Exception error) { Debug.LogError("B1a barracks installation failed: " + error); }
        }

        public static string Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before installing B1a.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save or discard open scene changes first.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                throw new InvalidOperationException("Main scene is missing; never regenerate it.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null || File.Exists(PrefabPath))
                throw new InvalidOperationException("Barracks prefab exists already. Edit it and its scene instances directly; do not rerun B1a.");

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new InvalidOperationException("Missing URP Lit shader.");

            // Validate *all* required inputs before creating prefab/material assets.
            var sourceModels = new GameObject[Palette.Length];
            for (int i = 0; i < Palette.Length; i++)
            {
                string path = ModelPath + "/Barracks_B1a_" + Palette[i].Section + ".obj";
                sourceModels[i] = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (sourceModels[i] == null || sourceModels[i].GetComponentsInChildren<MeshFilter>(true).Length == 0)
                    throw new InvalidOperationException("OBJ not imported or has no mesh: " + path);
            }
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            if (roots.Any(r => r.name == BaseRootName))
                throw new InvalidOperationException("B1a is already installed in the main map. Edit it manually.");
            var plateau = roots.SingleOrDefault(r => r.name.StartsWith("E1C / Main plateau"));
            var player = roots.SingleOrDefault(r => r.name.StartsWith("E1 / PlayerJet"));
            var camera = roots.SingleOrDefault(r => r.name == "E1 / Main Camera");
            var terrain = roots.SingleOrDefault(r => r.GetComponent<Terrain>() != null);
            if (plateau == null || player == null || camera == null || terrain == null ||
                player.GetComponent<JetAssembly>() == null || camera.GetComponent<ArcadeCameraFollow>() == null)
                throw new InvalidOperationException("Expected E1c scene roots not found. Inspect current scene before B1a.");
            if (Vector2.Distance(new Vector2(plateau.transform.position.x, plateau.transform.position.z),
                    new Vector2(BarracksCenter.x, BarracksCenter.z)) < 400f)
                throw new InvalidOperationException("Plateau appears too close to planned B1 site; don't place barracks on top of geology.");

            EnsureFolder(ModelPath);
            EnsureFolder(PrefabFolder);
            var materials = new Material[Palette.Length];
            for (int i = 0; i < Palette.Length; i++)
            {
                var material = new Material(shader) { name = "B1a / " + Palette[i].Section };
                if (!ColorUtility.TryParseHtmlString(Palette[i].Hex, out var albedo))
                    throw new InvalidOperationException("Bad barracks color " + Palette[i].Hex);
                material.SetColor("_BaseColor", albedo);
                material.SetFloat("_Metallic", 0f);
                material.SetFloat("_Smoothness", 0f);
                material.enableInstancing = true;
                AssetDatabase.CreateAsset(material, ModelPath + "/B1a_" + Palette[i].Section + ".mat");
                materials[i] = material;
            }

            var master = new GameObject("Barracks_B1a");
            SceneManager.MoveGameObjectToScene(master, scene);
            for (int i = 0; i < Palette.Length; i++)
            {
                // Keep each imported OBJ as a nested model instance so Unity's
                // axis/import transforms are respected and source remains editable.
                var section = (GameObject)PrefabUtility.InstantiatePrefab(sourceModels[i], scene);
                section.name = Palette[i].Section;
                section.transform.SetParent(master.transform, false);
                section.transform.localPosition = Vector3.zero;
                section.transform.localRotation = Quaternion.identity;
                section.transform.localScale = Vector3.one;
                foreach (var renderer in section.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var count = renderer.sharedMaterials.Length;
                    var overrides = new Material[Mathf.Max(1, count)];
                    for (int j = 0; j < overrides.Length; j++) overrides[j] = materials[i];
                    renderer.sharedMaterials = overrides;
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }
            }
            var boundsCollider = master.AddComponent<BoxCollider>();
            boundsCollider.center = new Vector3(0f, 3.08f, 0f);
            boundsCollider.size = new Vector3(11.7f, 6.35f, 32.3f);
            var prefab = PrefabUtility.SaveAsPrefabAsset(master, PrefabPath);
            Object.DestroyImmediate(master);
            if (prefab == null)
                throw new InvalidOperationException("Prefab save returned null: " + PrefabPath);

            var baseRoot = new GameObject(BaseRootName);
            SceneManager.MoveGameObjectToScene(baseRoot, scene);
            baseRoot.transform.position = BarracksCenter;
            for (int i = 0; i < 3; i++)
            {
                var building = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                building.name = $"Barracks {i + 1:00}";
                building.transform.SetParent(baseRoot.transform, false);
                building.transform.localPosition = new Vector3((i - 1) * 25f, 0f, 0f);
                building.transform.localRotation = Quaternion.identity;
            }

            // Reposition instances only. No shared PlayerJet/camera prefab edit.
            player.transform.SetPositionAndRotation(ApproachSpawn,
                Quaternion.LookRotation(ApproachHeading, Vector3.up));
            PrefabUtility.RecordPrefabInstancePropertyModifications(player.transform);
            camera.transform.position = new Vector3(ApproachSpawn.x,
                ApproachSpawn.y + CameraHeightAboveJet, ApproachSpawn.z + CameraOffsetZ);
            PrefabUtility.RecordPrefabInstancePropertyModifications(camera.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Unable to save main map with B1 barracks.");
            Selection.activeGameObject = baseRoot;
            return "B1a installed on existing E1 scene: 3 prefab barracks, preserved plateau, " +
                   "new Y150 approach spawn. Open Game / Play / R reset. " +
                   "Commit scene, generated prefab/materials and all .meta files after review.";
        }

        private static void EnsureFolder(string path)
        {
            string current = "Assets";
            foreach (string segment in path.Substring("Assets/".Length).Split('/'))
            {
                string next = current + "/" + segment;
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segment);
                current = next;
            }
        }
    }
}
#endif
