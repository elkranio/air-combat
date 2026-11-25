using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AirCombat
{
    // Explicit authoring only; never modifies a scene or an existing prefab.
    internal static class CliffModuleAuthoring
    {
        private const string Art = "Assets/_Project/Art/Environment/Mission01_FirstSortie/";
        private const string PrefabRoot = "Assets/_Project/Prefabs/Environment";
        private const string Prefabs = PrefabRoot + "/Cliffs";
        private static readonly string[] Names = { "CL01_Fan", "CL02_Spur", "CL03_Ridge", "CL04_Citadel" };
        private static readonly string[] Groups = { "Foot", "Wall", "Crown" };

        [MenuItem("Air Combat/Art/Build Cliff Module Prefabs")]
        private static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                var mats = Groups.Select(g => AssetDatabase.LoadAssetAtPath<Material>(Art + "CliffTrial/CliffTrial_" + g + ".mat")).ToArray();
                if (mats.Any(m => m == null)) throw new InvalidOperationException("Accepted cliff materials missing.");
                var models = new GameObject[Names.Length, Groups.Length];
                for (int n = 0; n < Names.Length; n++)
                for (int g = 0; g < Groups.Length; g++)
                {
                    var path = Art + "CliffModules/" + Names[n] + "_" + Groups[g] + ".obj";
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    var filters = model == null ? Array.Empty<MeshFilter>() : model.GetComponentsInChildren<MeshFilter>(true);
                    if (filters.Length == 0 || filters.Any(f => f.sharedMesh == null || !f.sharedMesh.isReadable))
                        throw new InvalidOperationException("Missing or unreadable mesh: " + path);
                    models[n,g] = model;
                }
                if (!AssetDatabase.IsValidFolder(Prefabs)) AssetDatabase.CreateFolder(PrefabRoot, "Cliffs");
                for (int n = 0; n < Names.Length; n++)
                {
                    var path = Prefabs + "/" + Names[n] + ".prefab";
                    if (AssetDatabase.LoadMainAssetAtPath(path) != null) continue;
                    // Preview scene avoids dirtying the open authored mission while assembling assets.
                    var preview = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
                    try
                    {
                        var root = new GameObject(Names[n]);
                        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, preview);
                        for (int g = 0; g < Groups.Length; g++)
                        {
                            var child = (GameObject)PrefabUtility.InstantiatePrefab(models[n,g], root.transform);
                            if (child == null) throw new InvalidOperationException("Mesh instantiation failed.");
                            child.transform.localPosition = Vector3.zero;
                            child.transform.localRotation = Quaternion.identity;
                            child.transform.localScale = Vector3.one;
                            PrefabUtility.RecordPrefabInstancePropertyModifications(child.transform);
                            foreach (var renderer in child.GetComponentsInChildren<MeshRenderer>(true))
                            {
                                var slots = renderer.sharedMaterials;
                                for (int s = 0; s < slots.Length; s++) slots[s] = mats[g];
                                renderer.sharedMaterials = slots;
                                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                            }
                        }
                        root.AddComponent<RadarTerrainObstacle>();
                        PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                        if (!success) throw new InvalidOperationException("Prefab save failed: " + path);
                    }
                    finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview); }
                }
                AssetDatabase.SaveAssets();
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/" + Names[0] + ".prefab");
                Debug.Log("Four cliff module prefabs ready. Existing prefabs and mission scene untouched. Drag a module into a flat area for review.");
            }
            catch (Exception ex) { Debug.LogError("Cliff module authoring: " + ex); }
        }
    }
}
