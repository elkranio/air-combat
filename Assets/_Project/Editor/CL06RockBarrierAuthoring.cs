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
        private const string ScenePath = "Assets/_Project/Scenes/Mission01_FirstSortie.unity";
        private const string Art = "Assets/_Project/Art/Environment/Mission01_FirstSortie/";
        private const string Library = Art + "CL06_RockBarrier/";
        private const string Prefabs = "Assets/_Project/Prefabs/Environment/Cliffs";
        private const string OldBarrier = "Mission01 / Cliff barrier - camera layout v2";
        private const string NewBarrier = "Mission01 / CL06 rock barrier";
        private const string UndoName = "Replace Mission 01 rock barrier";

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

        [MenuItem("Air Combat/Missions/Replace Mission 01 Barrier with CL06")]
        private static void Replace()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                var scene = SceneManager.GetActiveScene();
                if (!scene.isLoaded || scene.path != ScenePath) throw new InvalidOperationException("Open Mission01_FirstSortie first.");
                var roots = scene.GetRootGameObjects();
                if (roots.Any(g => g.name == NewBarrier))
                { Debug.Log("CL06 already installed. Edit the scene directly."); return; }
                var old = roots.SingleOrDefault(g => g.name == OldBarrier);
                if (old == null)
                    throw new InvalidOperationException("Camera layout v2 not found. Run Air Combat > Missions > Apply Mission 01 Camera Layout v2 first, then retry.");
                if (old.transform.childCount != 8) throw new InvalidOperationException("Expected eight v2 cliff instances; inspect scene before replacement.");
                var objectives = roots.Single(g => g.name == "Mission01 / Objectives");
                if (!Enumerable.Range(0, objectives.transform.childCount).Any(i => objectives.transform.GetChild(i).name == "H2C / Camera layout v2 (visual only)"))
                    throw new InvalidOperationException("Expected v2 objective layout; no changes made.");
                var ground = roots.Single(g => g.name == "Mission01 / Faceted surface").GetComponent<MeshFilter>();
                if (ground == null || ground.sharedMesh == null || Mathf.Abs(ground.sharedMesh.bounds.min.y) > .01f || Mathf.Abs(ground.sharedMesh.bounds.max.y) > .01f)
                    throw new InvalidOperationException("Expected flat mission ground.");
                var trial = roots.SingleOrDefault(g => g.name == "Mission01 / CL05 rock trial");
                if (trial != null && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(trial) != Prefabs + "/CL05_TestRock.prefab")
                    throw new InvalidOperationException("Unexpected CL05 trial object; inspect scene before replacement.");
                var layout = ReadLayout();
                var library = Build(layout);
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(UndoName);
                try
                {
                    var root = NewGroup(scene, null, NewBarrier);
                    var sections = new Dictionary<string, Transform>();
                    int index = 0;
                    foreach (var p in layout.placements)
                    {
                        if (!sections.TryGetValue(p.section, out var parent))
                        {
                            parent = NewGroup(scene, root, p.section);
                            sections.Add(p.section, parent);
                        }
                        var instance = (GameObject)PrefabUtility.InstantiatePrefab(library[p.model], parent);
                        if (instance == null) throw new InvalidOperationException("Cannot place " + p.model);
                        Undo.RegisterCreatedObjectUndo(instance, UndoName);
                        Undo.RecordObjects(new UnityEngine.Object[] { instance, instance.transform }, UndoName);
                        instance.name = $"{++index:00} / {p.model}";
                        instance.transform.localPosition = new Vector3(p.x, 0, p.z);
                        instance.transform.localRotation = Quaternion.Euler(0, p.yaw, 0);
                        instance.transform.localScale = new Vector3(p.sx, p.sy, p.sz);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                    }
                    Undo.DestroyObjectImmediate(old);
                    if (trial != null) Undo.DestroyObjectImmediate(trial);
                    Undo.CollapseUndoOperations(group);
                    EditorSceneManager.MarkSceneDirty(scene);
                    Selection.activeGameObject = root.gameObject;
                    Debug.Log($"CL06: {layout.placements.Length} solid rocks installed in {sections.Count} groups. Save scene after review. Objectives, ground, lights and gameplay unchanged.");
                }
                catch { Undo.RevertAllDownToGroup(group); throw; }
            }
            catch (Exception ex) { Debug.LogError("CL06: " + ex); }
        }

        private static Transform NewGroup(Scene scene, Transform parent, string name)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            Undo.RegisterCreatedObjectUndo(go, UndoName);
            if (parent != null) Undo.SetTransformParent(go.transform, parent, UndoName);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }
    }
}
