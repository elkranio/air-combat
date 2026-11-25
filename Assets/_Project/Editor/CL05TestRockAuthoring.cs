using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    // One isolated art trial. No changes to the existing barrier, terrain or combat.
    internal static class CL05TestRockAuthoring
    {
        private const string Art = "Assets/_Project/Art/Environment/Mission01_FirstSortie/CL05_TestRock/";
        private const string Prefab = "Assets/_Project/Prefabs/Environment/Cliffs/CL05_TestRock.prefab";
        private const string ScenePath = "Assets/_Project/Scenes/Mission01_FirstSortie.unity";
        private const string TrialName = "Mission01 / CL05 rock trial";
        private static readonly string[] Names = { "01_Core", "02_West", "03_East", "04_Front", "05_Shoulder", "06_Slab", "07_Slab" };
        private static readonly int[] Tones = { 0, 1, 0, 1, 2, 2, 1 };
        private static readonly Color32[] Colors = {
            new Color32(150,122,99,255), new Color32(143,116,95,255), new Color32(158,131,108,255)
        };
        private static readonly Vector3 Position = new Vector3(350,0,-650);

        [MenuItem("Air Combat/Art/Build CL05 Test Rock")]
        private static void BuildMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { Selection.activeObject = Build(); }
            catch (Exception ex) { Debug.LogError("CL05: " + ex); }
        }

        [MenuItem("Air Combat/Missions/Place CL05 Rock Trial")]
        private static void Place()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                var scene = SceneManager.GetActiveScene();
                if (!scene.isLoaded || scene.path != ScenePath)
                    throw new InvalidOperationException("Open Mission01_FirstSortie first.");
                var roots = scene.GetRootGameObjects();
                var previous = roots.SingleOrDefault(g => g.name == TrialName);
                if (previous != null) { Selection.activeGameObject = previous; return; }
                var terrain = roots.Single(g => g.name == "Mission01 / Editable terrain").GetComponent<Terrain>();
                if (terrain == null || terrain.terrainData == null) throw new InvalidOperationException("Mission terrain missing.");
                for (int z = -100; z <= 100; z += 20)
                for (int x = -120; x <= 120; x += 20)
                {
                    var p = Position + new Vector3(x,0,z);
                    var local = p - terrain.transform.position;
                    var size = terrain.terrainData.size;
                    if (local.x < 0 || local.z < 0 || local.x > size.x || local.z > size.z ||
                        Mathf.Abs(terrain.SampleHeight(p) + terrain.transform.position.y) > .1f)
                        throw new InvalidOperationException("Trial footprint is not flat; build and place manually instead.");
                }
                foreach (var root in roots)
                foreach (var obstacle in root.GetComponentsInChildren<RadarTerrainObstacle>())
                foreach (var r in obstacle.GetComponentsInChildren<MeshRenderer>())
                {
                    var b = r.bounds;
                    if (b.min.x < Position.x+125 && b.max.x > Position.x-125 &&
                        b.min.z < Position.z+100 && b.max.z > Position.z-100)
                        throw new InvalidOperationException("Trial area overlaps an existing radar rock; build and place manually instead.");
                }
                var prefab = Build();
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                if (instance == null) throw new InvalidOperationException("CL05 placement failed.");
                Undo.RegisterCreatedObjectUndo(instance, "Place CL05 rock trial");
                instance.name = TrialName;
                instance.transform.SetPositionAndRotation(Position, Quaternion.identity);
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                EditorSceneManager.MarkSceneDirty(scene);
                Selection.activeGameObject = instance;
                Debug.Log("CL05 placed at (350,0,-650). Existing layout untouched. Remove trial after review if rejected.");
            }
            catch (Exception ex) { Debug.LogError("CL05: " + ex); }
        }

        private static GameObject Build()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            if (existing != null) return existing;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit missing.");
            var models = Names.Select(n => AssetDatabase.LoadAssetAtPath<GameObject>(Art + "CL05_" + n + ".obj")).ToArray();
            foreach (var model in models)
            {
                if (model == null) throw new InvalidOperationException("Missing CL05 OBJ import.");
                var filters = model.GetComponentsInChildren<MeshFilter>(true);
                if (filters.Length == 0 || filters.Any(f => f.sharedMesh == null || !f.sharedMesh.isReadable))
                    throw new InvalidOperationException("Missing or unreadable CL05 mesh: " + model.name);
            }
            var mats = new Material[Colors.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                var path = Art + "CL05_Stone_" + i + ".mat";
                mats[i] = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mats[i] != null) continue;
                var mat = new Material(shader) { name = "CL05_Stone_" + i };
                mat.SetColor("_BaseColor", Colors[i]);
                mat.SetFloat("_Metallic",0); mat.SetFloat("_Smoothness",0);
                mat.SetFloat("_SpecularHighlights",0); mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                mat.SetFloat("_EnvironmentReflections",0); mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                AssetDatabase.CreateAsset(mat,path); mats[i] = mat;
            }
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("CL05_TestRock");
                SceneManager.MoveGameObjectToScene(root,preview);
                for (int i = 0; i < models.Length; i++)
                {
                    var block = new GameObject(Names[i]);
                    SceneManager.MoveGameObjectToScene(block,preview);
                    block.transform.SetParent(root.transform,false);
                    var child = (GameObject)PrefabUtility.InstantiatePrefab(models[i],block.transform);
                    if (child == null) throw new InvalidOperationException("CL05 mesh instantiation failed.");
                    child.transform.localPosition = Vector3.zero;
                    child.transform.localRotation = Quaternion.identity;
                    child.transform.localScale = Vector3.one;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(child.transform);
                    foreach (var renderer in child.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        var slots = renderer.sharedMaterials;
                        for (int j = 0; j < slots.Length; j++) slots[j] = mats[Tones[i]];
                        renderer.sharedMaterials = slots;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    }
                    // Each overlapping block is a separate closed solid: putting the radar
                    // on their shared root would XOR the overlapping sections into holes.
                    block.AddComponent<RadarTerrainObstacle>();
                }
                PrefabUtility.SaveAsPrefabAsset(root,Prefab,out bool success);
                if (!success) throw new InvalidOperationException("CL05 prefab save failed.");
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
        }
    }
}
