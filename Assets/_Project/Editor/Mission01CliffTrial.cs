using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    // Visual comparison only. Existing terrain, camera and mission layout are untouched.
    internal static class Mission01CliffTrial
    {
        private const string ScenePath = "Assets/_Project/Scenes/Mission01_FirstSortie.unity";
        private const string Art = "Assets/_Project/Art/Environment/Mission01_FirstSortie/CliffTrial/";
        private const string TrialName = "Mission01 / Cliff mesh trial";
        private static readonly string[] Groups = { "Foot", "Wall", "Crown" };
        private static readonly Color32[] Colors = {
            new Color32(165,145,122,255), new Color32(112,92,77,255), new Color32(160,143,123,255)
        };
        private static readonly Vector3 Position = new Vector3(-180f, 0f, -70f);

        [MenuItem("Air Combat/Missions/Place Mission 01 Cliff Mesh Trial")]
        private static void Place()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetActiveScene();
            if (!scene.isLoaded || scene.path != ScenePath)
            { Debug.LogError("Open Mission01_FirstSortie first."); return; }
            try
            {
                var roots = scene.GetRootGameObjects();
                var previous = roots.SingleOrDefault(g => g.name == TrialName);
                if (previous != null)
                {
                    if (previous.GetComponent<RadarTerrainObstacle>() == null)
                    {
                        Undo.AddComponent<RadarTerrainObstacle>(previous);
                        EditorSceneManager.MarkSceneDirty(scene);
                    }
                    Selection.activeGameObject = previous;
                    Debug.Log("Cliff terrain radar enabled. Save scene.");
                    return;
                }
                var terrain = roots.Single(g => g.name == "Mission01 / Editable terrain").GetComponent<Terrain>();
                if (terrain == null || terrain.terrainData == null) throw new InvalidOperationException("Mission terrain missing.");
                // Check the full nominal footprint and a little clearance against the editable source.
                for (int z = -110; z <= 110; z += 10)
                for (int x = -180; x <= 180; x += 10)
                {
                    var p = Position + new Vector3(x,0,z);
                    var local = p - terrain.transform.position;
                    var size = terrain.terrainData.size;
                    if (local.x < 0 || local.z < 0 || local.x > size.x || local.z > size.z ||
                        Mathf.Abs(terrain.SampleHeight(p) + terrain.transform.position.y - Position.y) > .1f)
                        throw new InvalidOperationException("Trial footprint is not flat; no scene changes.");
                }
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("URP Lit missing.");
                var models = Groups.Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(Art + "CliffTrial_" + g + ".obj")).ToArray();
                if (models.Any(m => m == null || m.GetComponentInChildren<MeshFilter>(true) == null))
                    throw new InvalidOperationException("Cliff OBJ import incomplete.");
                var mats = new Material[Groups.Length];
                for (int i = 0; i < Groups.Length; i++)
                {
                    var path = Art + "CliffTrial_" + Groups[i] + ".mat";
                    mats[i] = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (mats[i] != null) continue;
                    var mat = new Material(shader) { name = "CliffTrial_" + Groups[i] };
                    mat.SetColor("_BaseColor", Colors[i]);
                    mat.SetFloat("_Metallic", 0f);
                    mat.SetFloat("_Smoothness", 0f);
                    mat.SetFloat("_SpecularHighlights", 0f);
                    mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                    mat.SetFloat("_EnvironmentReflections", 0f);
                    mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                    AssetDatabase.CreateAsset(mat,path);
                    mats[i] = mat;
                }
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Place cliff mesh trial");
                try
                {
                    var root = new GameObject(TrialName);
                    Undo.RegisterCreatedObjectUndo(root,"Place cliff mesh trial");
                    root.transform.position = Position;
                    for (int i = 0; i < Groups.Length; i++)
                    {
                        var child = (GameObject)PrefabUtility.InstantiatePrefab(models[i],root.transform);
                        if (child == null) throw new InvalidOperationException("Cliff instantiation failed.");
                        Undo.RegisterCreatedObjectUndo(child,"Place cliff mesh trial");
                        child.transform.localPosition = Vector3.zero;
                        child.transform.localRotation = Quaternion.identity;
                        child.transform.localScale = Vector3.one;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(child.transform);
                        foreach (var r in child.GetComponentsInChildren<MeshRenderer>(true))
                        {
                            var slots = r.sharedMaterials;
                            for (int j = 0; j < slots.Length; j++) slots[j] = mats[i];
                            r.sharedMaterials = slots;
                            PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                        }
                    }
                    Undo.AddComponent<RadarTerrainObstacle>(root);
                    Undo.CollapseUndoOperations(group);
                    EditorSceneManager.MarkSceneDirty(scene);
                    AssetDatabase.SaveAssets();
                    Selection.activeGameObject = root;
                    Debug.Log("Mission01 cliff mesh trial placed. Terrain radar enabled; no collision yet. Save scene after review.");
                }
                catch { Undo.RevertAllDownToGroup(group); throw; }
            }
            catch (Exception ex) { Debug.LogError("Mission01 cliff trial: " + ex); }
        }
    }
}
