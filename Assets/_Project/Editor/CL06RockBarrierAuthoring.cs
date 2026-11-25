using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    // Scene authoring only. Each rock is a separate reusable, closed radar solid.
    internal static class CL06RockBarrierAuthoring
    {
        private const string Art = "Assets/_Project/Art/Environment/Mission01_FirstSortie/";
        private const string Library = Art + "CL06_RockBarrier/";
        private const string Prefabs = "Assets/_Project/Prefabs/Environment/Cliffs";

        [Serializable] private sealed class Model { public string name; public int tone; }
        [Serializable] private sealed class Placement
        {
            public string section, model;
            public float x, z, yaw, sx, sy, sz;
        }
        [Serializable] private sealed class Layout { public Model[] models; public Placement[] placements; }

        private static Layout ReadLayout()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(Library + "Mission01_CL06_Layout.json");
            if (asset == null) throw new InvalidOperationException("CL06 layout JSON missing.");
            var layout = JsonUtility.FromJson<Layout>(asset.text);
            if (layout == null || layout.models == null || layout.placements == null || layout.models.Length != 8 || layout.placements.Length == 0)
                throw new InvalidOperationException("Invalid CL06 layout.");
            var names = new HashSet<string>(layout.models.Select(m => m.name));
            if (names.Count != layout.models.Length || layout.models.Any(m => string.IsNullOrEmpty(m.name) || m.tone < 0 || m.tone > 2) ||
                layout.placements.Any(p => !names.Contains(p.model) || string.IsNullOrEmpty(p.section) || p.sx <= 0 || p.sy <= 0 || p.sz <= 0))
                throw new InvalidOperationException("Invalid CL06 model or placement entry.");
            return layout;
        }

        [MenuItem("Air Combat/Art/Build CL06 Rock Library")]
        private static void BuildMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { var library = Build(ReadLayout()); Selection.activeObject = library.Values.First(); }
            catch (Exception ex) { Debug.LogError("CL06: " + ex); }
        }

        private static Dictionary<string, GameObject> Build(Layout layout)
        {
            var mats = Enumerable.Range(0, 3).Select(i => AssetDatabase.LoadAssetAtPath<Material>(Art + "CL05_TestRock/CL05_Stone_" + i + ".mat")).ToArray();
            if (mats.Any(m => m == null)) throw new InvalidOperationException("Accepted CL05 stone materials missing. Build CL05 first.");
            if (!AssetDatabase.IsValidFolder(Prefabs)) throw new InvalidOperationException("Cliffs prefab folder missing.");
            var library = new Dictionary<string, GameObject>();
            var sources = new Dictionary<string, GameObject>();
            foreach (var model in layout.models)
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(Library + model.name + ".obj");
                ValidateMesh(source, model.name);
                sources.Add(model.name, source);
                var existing = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/" + model.name + ".prefab");
                if (existing == null) continue;
                ValidateMesh(existing, model.name);
                if (existing.GetComponent<RadarTerrainObstacle>() == null) throw new InvalidOperationException("Missing radar component on " + model.name);
                library.Add(model.name, existing);
            }
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                foreach (var model in layout.models)
                {
                    if (library.ContainsKey(model.name)) continue;
                    var root = new GameObject(model.name);
                    SceneManager.MoveGameObjectToScene(root, preview);
                    var child = (GameObject)PrefabUtility.InstantiatePrefab(sources[model.name], root.transform);
                    if (child == null) throw new InvalidOperationException("Cannot instantiate " + model.name);
                    child.transform.localPosition = Vector3.zero;
                    child.transform.localRotation = Quaternion.identity;
                    child.transform.localScale = Vector3.one;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(child.transform);
                    foreach (var r in child.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        var slots = r.sharedMaterials;
                        for (int i = 0; i < slots.Length; i++) slots[i] = mats[model.tone];
                        r.sharedMaterials = slots;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                    }
                    root.AddComponent<RadarTerrainObstacle>();
                    var path = Prefabs + "/" + model.name + ".prefab";
                    var saved = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                    if (!success || saved == null) throw new InvalidOperationException("Cannot save " + path);
                    library.Add(model.name, saved);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            AssetDatabase.SaveAssets();
            return library;
        }

        private static void ValidateMesh(GameObject asset, string name)
        {
            var meshes = asset == null ? Array.Empty<MeshFilter>() : asset.GetComponentsInChildren<MeshFilter>(true);
            if (meshes.Length != 1 || meshes[0].sharedMesh == null || !meshes[0].sharedMesh.isReadable)
                throw new InvalidOperationException("Expected one readable closed mesh for " + name);
        }

    }
}
