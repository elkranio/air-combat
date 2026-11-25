using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    // Explicit scene authoring. No model, material or runtime changes.
    internal static class Mission01RockLayoutV3
    {
        private const string ScenePath = "Assets/_Project/Scenes/Mission01_FirstSortie.unity";
        private const string Prefabs = "Assets/_Project/Prefabs/Environment/Cliffs/";
        private const string SamPath = "Assets/_Project/Prefabs/Environment/SAM01_Picket.prefab";
        private const string RecipePath = "Assets/_Project/Art/Environment/Mission01_FirstSortie/CL06_RockBarrier/Mission01_RockLayoutV3.json";
        private const string NewBarrier = "Mission01 / CL06 rock layout v3";
        private const string NewPlan = "M01 / Rock layout v3 (visual only)";
        private const string UndoName = "Apply Mission 01 rock layout v3";
        private static readonly string[] OldRocks = { "Mission01 / CL06 rock barrier", "Mission01 / CL06 orientation trials", "Mission01 / CL05 rock trial", "Mission01 / Cliff barrier - camera layout v2", NewBarrier };
        private static readonly string[] OldPlans = { "H2C / Camera layout v2 (visual only)", NewPlan };
        [Serializable] private sealed class Placement
        {
            public string section, model;
            public float x,z,yaw,sx,sy,sz;
        }
        [Serializable] private sealed class Point { public float x,z,radius; }
        [Serializable] private sealed class Layout
        {
            public Placement[] placements, debris;
            public Point[] sam, recon, mainRoute, optionalRoute;
            public Point extra;
        }

        [MenuItem("Air Combat/Missions/Apply Mission 01 Rock Layout v3")]
        private static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                var scene = SceneManager.GetActiveScene();
                if (!scene.isLoaded || scene.path != ScenePath)
                    throw new InvalidOperationException("Open Mission01_FirstSortie first.");
                var roots = scene.GetRootGameObjects();
                var old = roots.Where(g => OldRocks.Contains(g.name)).ToArray();
                if (!old.Any(g => g.name == OldRocks[0] || g.name == NewBarrier))
                    throw new InvalidOperationException("Expected installed CL06 main barrier; no changes made.");
                var objectives = roots.Single(g => g.name == "Mission01 / Objectives").transform;
                var oldPlans = Enumerable.Range(0, objectives.childCount).Select(objectives.GetChild)
                    .Where(t => OldPlans.Contains(t.name)).ToArray();
                if (oldPlans.Length != 1)
                    throw new InvalidOperationException("Expected one existing v2/v3 objective plan; no changes made.");
                var ground = roots.Single(g => g.name == "Mission01 / Faceted surface").GetComponent<MeshFilter>();
                if (ground == null || ground.sharedMesh == null || Mathf.Abs(ground.sharedMesh.bounds.min.y) > .01f || Mathf.Abs(ground.sharedMesh.bounds.max.y) > .01f)
                    throw new InvalidOperationException("Expected flat mission ground.");
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(RecipePath);
                if (asset == null) throw new InvalidOperationException("Missing v3 layout recipe.");
                var data = JsonUtility.FromJson<Layout>(asset.text);
                if (data == null || data.placements == null || data.placements.Length == 0 || data.debris == null ||
                    data.sam == null || data.sam.Length != 6 || data.recon == null || data.recon.Length != 3 ||
                    data.mainRoute == null || data.optionalRoute == null || data.extra == null)
                    throw new InvalidOperationException("Invalid v3 layout recipe.");
                var prefabs = new Dictionary<string, GameObject>();
                foreach (var p in data.placements.Concat(data.debris))
                {
                    if (string.IsNullOrEmpty(p.section) || string.IsNullOrEmpty(p.model) || p.sx <= 0 || p.sy <= 0 || p.sz <= 0)
                        throw new InvalidOperationException("Invalid placement.");
                    if (prefabs.ContainsKey(p.model)) continue;
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + p.model + ".prefab");
                    var filters = prefab != null ? prefab.GetComponentsInChildren<MeshFilter>(true) : Array.Empty<MeshFilter>();
                    if (prefab == null || prefab.GetComponent<RadarTerrainObstacle>() == null || filters.Length != 1 ||
                        filters[0].sharedMesh == null || !filters[0].sharedMesh.isReadable || filters[0].GetComponent<MeshRenderer>() == null)
                        throw new InvalidOperationException("Invalid CL06 prefab: " + p.model);
                    prefabs.Add(p.model, prefab);
                }
                var sam = AssetDatabase.LoadAssetAtPath<GameObject>(SamPath);
                if (sam == null) throw new InvalidOperationException("Missing SAM01 visual prefab.");
                Undo.IncrementCurrentGroup();
                int undo = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(UndoName);
                try
                {
                    var root = Group(scene, null, NewBarrier, Vector3.zero);
                    var sections = new Dictionary<string, Transform>();
                    int index = 0;
                    foreach (var p in data.placements)
                    {
                        if (!sections.TryGetValue(p.section, out var parent))
                        {
                            parent = Group(scene, root, p.section, Vector3.zero);
                            sections.Add(p.section, parent);
                        }
                        var instance = Instance(prefabs[p.model], parent);
                        Undo.RecordObjects(new UnityEngine.Object[] { instance, instance.transform }, UndoName);
                        instance.name = $"{++index:000} / {p.model}";
                        Pose(instance.transform, p, 0f);
                        Record(instance);
                    }
                    var debrisRoot = Group(scene, root, "09 Compact base debris (visual only)", Vector3.zero);
                    int rubble = 0;
                    foreach (var p in data.debris)
                    {
                        // Copy only the visual mesh and materials, not the radar solid.
                        var source = prefabs[p.model];
                        var filter = source.GetComponentInChildren<MeshFilter>(true);
                        var sourceRenderer = filter.GetComponent<MeshRenderer>();
                        var piece = new GameObject($"{++rubble:000} / {p.model}", typeof(MeshFilter), typeof(MeshRenderer));
                        SceneManager.MoveGameObjectToScene(piece, scene);
                        Undo.RegisterCreatedObjectUndo(piece, UndoName);
                        Undo.SetTransformParent(piece.transform, debrisRoot, UndoName);
                        var relative = source.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                        var matrix = Matrix4x4.TRS(new Vector3(p.x,-.3f,p.z), Quaternion.Euler(0,p.yaw,0), new Vector3(p.sx,p.sy,p.sz)) * relative;
                        piece.transform.localPosition = matrix.GetColumn(3);
                        piece.transform.localRotation = matrix.rotation;
                        piece.transform.localScale = matrix.lossyScale;
                        piece.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                        var renderer = piece.GetComponent<MeshRenderer>();
                        renderer.sharedMaterials = sourceRenderer.sharedMaterials;
                        renderer.shadowCastingMode = sourceRenderer.shadowCastingMode;
                        renderer.receiveShadows = sourceRenderer.receiveShadows;
                        GameObjectUtility.SetStaticEditorFlags(piece, StaticEditorFlags.BatchingStatic);
                    }
                    var plan = Group(scene, objectives, NewPlan, Vector3.zero);
                    var sites = Group(scene, plan, "01 / SAM sites - visual only", Vector3.zero);
                    for (int i = 0; i < data.sam.Length; i++)
                    {
                        var p = data.sam[i];
                        var instance = Instance(sam, sites);
                        Undo.RecordObjects(new UnityEngine.Object[] { instance, instance.transform }, UndoName);
                        instance.name = $"SAM {(char)('A'+i)} - visual (R{p.radius:0})";
                        instance.transform.SetPositionAndRotation(World(p,0), Quaternion.Euler(0,i%2==0?135:225,0));
                        Record(instance);
                    }
                    var center = (World(data.recon[0],78)+World(data.recon[1],78)+World(data.recon[2],78))/3f;
                    var recon = Group(scene, plan, "02 / Recon zone - empty markers", center);
                    for (int i=0; i<data.recon.Length; i++) Group(scene, recon, $"Recon {i+1:00}", World(data.recon[i],78));
                    Group(scene, plan, "03 / Optional objective - empty marker", World(data.extra,0));
                    Route(scene,plan,"04 / Main route - not enforced",data.mainRoute);
                    Route(scene,plan,"05 / Optional route - not enforced",data.optionalRoute);
                    // Only remove accepted old objects after the complete new layout exists.
                    foreach (var g in old) Undo.DestroyObjectImmediate(g);
                    foreach (var t in oldPlans) Undo.DestroyObjectImmediate(t.gameObject);
                    Undo.CollapseUndoOperations(undo);
                    EditorSceneManager.MarkSceneDirty(scene);
                    Selection.activeGameObject = root.gameObject;
                    Debug.Log($"M01 v3: {index} rock solids, {rubble} cosmetic debris, six visual SAMs and objective markers. Save after review. Map edge enforcement is not part of this pass.");
                }
                catch { Undo.RevertAllDownToGroup(undo); throw; }
            }
            catch (Exception ex) { Debug.LogError("M01 rock layout v3: " + ex); }
        }
        private static Vector3 World(Point p, float y) => new Vector3(p.x,y,p.z);
        private static void Pose(Transform t, Placement p, float y)
        {
            t.localPosition = new Vector3(p.x,y,p.z);
            t.localRotation = Quaternion.Euler(0,p.yaw,0);
            t.localScale = new Vector3(p.sx,p.sy,p.sz);
        }
        private static Transform Group(Scene scene, Transform parent, string name, Vector3 world)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go,scene);
            Undo.RegisterCreatedObjectUndo(go,UndoName);
            if (parent != null) Undo.SetTransformParent(go.transform,parent,UndoName);
            go.transform.SetPositionAndRotation(world,Quaternion.identity);
            go.transform.localScale = Vector3.one;
            return go.transform;
        }
        private static GameObject Instance(GameObject prefab, Transform parent)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);
            if (go == null) throw new InvalidOperationException("Cannot instantiate " + prefab.name);
            Undo.RegisterCreatedObjectUndo(go,UndoName);
            return go;
        }
        private static void Record(GameObject go)
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(go);
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        }
        private static void Route(Scene scene, Transform plan, string name, Point[] route)
        {
            var root = Group(scene,plan,name,Vector3.zero);
            for (int i=0;i<route.Length;i++) Group(scene,root,$"{i+1:00}",World(route[i],78));
        }
    }
}
