using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    // Explicit, one-time authoring pass. All coordinates are world X/Z;
    // the accepted layout was drafted in the fixed camera's ground-plane axes.
    internal static class Mission01CameraLayoutV2
    {
        private const string ScenePath = "Assets/_Project/Scenes/Mission01_FirstSortie.unity";
        private const string Cliffs = "Assets/_Project/Prefabs/Environment/Cliffs/";
        private const string SamPath = "Assets/_Project/Prefabs/Environment/SAM01_Picket.prefab";
        private const string OldBarrier = "Mission01 / Cliff barrier v4";
        private const string NewBarrier = "Mission01 / Cliff barrier - camera layout v2";
        private const string OldPlan = "H / Route plan (visual only)";
        private const string NewPlan = "H2C / Camera layout v2 (visual only)";
        private const string UndoName = "Mission 01 camera layout v2";

        private static readonly string[] ModuleNames = {
            "CL03_Ridge", "CL01_Fan", "CL03_Ridge", "CL03_Ridge",
            "CL01_Fan", "CL03_Ridge", "CL04_Citadel", "CL02_Spur"
        };
        private static readonly Vector3[] Positions = {
            new Vector3(-968,0,-395), new Vector3(-680,0,-106),
            new Vector3(-418,0,165), new Vector3(-143,0,449),
            new Vector3(137,0,729), new Vector3(404,0,1004),
            new Vector3(638,0,1203), new Vector3(1270,0,-820)
        };
        private static readonly float[] Yaws = { 49, -40, 43, 51, -48, 40, -31, -48 };
        private static readonly Vector3[] Scales = {
            new Vector3(.63f,.76f,.84f), new Vector3(.66f,.83f,.52f),
            new Vector3(.62f,.72f,.82f), new Vector3(.61f,.79f,.86f),
            new Vector3(.65f,.78f,.48f), new Vector3(.60f,.74f,.76f),
            new Vector3(.87f,.66f,.70f), new Vector3(1.16f,1.02f,.93f)
        };
        private static readonly Vector3[] SamPositions = {
            new Vector3(-359,0,-337), new Vector3(805,0,-436),
            new Vector3(688,0,711), new Vector3(593,0,1573),
            new Vector3(1194,0,1938), new Vector3(1726,0,2083)
        };
        // Planning radii only. No radar registration, launcher or damage in this atom.
        private static readonly int[] SamRadii = { 320, 465, 350, 420, 465, 450 };
        private static readonly Vector3[] Recon = {
            new Vector3(1383,78,1081), new Vector3(1415,78,1266), new Vector3(1613,78,1284)
        };
        private static readonly Vector3 ExtraTarget = new Vector3(-698,0,814);
        private static readonly Vector3[] MainRoute = {
            new Vector3(-355,78,-738), new Vector3(-151,78,-707),
            new Vector3(34,78,-540), new Vector3(106,78,-260),
            new Vector3(232,78,-52), new Vector3(390,78,79),
            new Vector3(665,78,155), new Vector3(914,78,250),
            new Vector3(1157,78,503), new Vector3(1243,78,778),
            new Vector3(1338,78,954), new Vector3(1460,78,1058), new Vector3(1582,78,1090)
        };
        private static readonly Vector3[] ExtraRoute = {
            new Vector3(1582,78,1090), new Vector3(1573,78,1342),
            new Vector3(1356,78,1532), new Vector3(1117,78,1672),
            new Vector3(846,78,1771), new Vector3(580,78,1776),
            new Vector3(282,78,1631), new Vector3(-66,78,1464),
            new Vector3(-350,78,1270), new Vector3(-585,78,1017), new Vector3(-698,78,814)
        };

        [MenuItem("Air Combat/Missions/Apply Mission 01 Camera Layout v2")]
        private static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                var scene = SceneManager.GetActiveScene();
                if (!scene.isLoaded || scene.path != ScenePath)
                    throw new InvalidOperationException("Open Mission01_FirstSortie first.");
                var roots = scene.GetRootGameObjects();
                var objectives = roots.Single(g => g.name == "Mission01 / Objectives");
                bool hasNewBarrier = roots.Any(g => g.name == NewBarrier);
                bool hasNewPlan = Child(objectives.transform, NewPlan) != null;
                if (hasNewBarrier && hasNewPlan)
                {
                    Debug.Log("MISSION-01H2C: camera layout already installed. Edit the authored scene directly.");
                    return;
                }
                if (hasNewBarrier || hasNewPlan)
                    throw new InvalidOperationException("Partial v2 layout found; inspect scene before retrying.");
                var oldBarrier = roots.Single(g => g.name == OldBarrier);
                var oldPlan = Child(objectives.transform, OldPlan);
                var oldSites = Child(oldPlan, "01 / SAM sites - no combat yet");
                if (oldBarrier.transform.childCount != 6 || oldPlan == null || oldSites == null || oldSites.childCount != 2)
                    throw new InvalidOperationException("Expected the saved H2B barrier and original two-SAM planning group.");
                var terrain = roots.Single(g => g.name == "Mission01 / Editable terrain").GetComponent<Terrain>();
                var surface = roots.Single(g => g.name == "Mission01 / Faceted surface").GetComponent<MeshFilter>();
                if (terrain == null || terrain.terrainData == null || surface == null || surface.sharedMesh == null ||
                    (terrain.terrainData.size - new Vector3(3200,180,3200)).sqrMagnitude > .01f ||
                    (terrain.transform.position - new Vector3(-1000,0,-1000)).sqrMagnitude > .01f ||
                    Mathf.Abs(surface.sharedMesh.bounds.min.y) > .01f || Mathf.Abs(surface.sharedMesh.bounds.max.y) > .01f)
                    throw new InvalidOperationException("Expected the flattened H2B ground; this tool never modifies terrain.");
                int n = terrain.terrainData.heightmapResolution;
                foreach (float height in terrain.terrainData.GetHeights(0,0,n,n))
                    if (Mathf.Abs(height) > .00001f)
                        throw new InvalidOperationException("Terrain is not flat; refusing to place modules.");

                var modules = ModuleNames.Select(name => AssetDatabase.LoadAssetAtPath<GameObject>(Cliffs + name + ".prefab")).ToArray();
                foreach (var module in modules)
                {
                    if (module == null || module.GetComponent<RadarTerrainObstacle>() == null)
                        throw new InvalidOperationException("Missing cliff prefab or radar component.");
                    var filters = module.GetComponentsInChildren<MeshFilter>(true);
                    if (filters.Length == 0 || filters.Any(f => f.sharedMesh == null || !f.sharedMesh.isReadable))
                        throw new InvalidOperationException("Missing or unreadable cliff mesh: " + module.name);
                }
                var sam = AssetDatabase.LoadAssetAtPath<GameObject>(SamPath);
                var existing = new[] { Child(oldSites,"SAM A - visual"), Child(oldSites,"SAM B - visual") };
                if (sam == null || existing.Any(t => t == null || PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) != SamPath))
                    throw new InvalidOperationException("Expected the original two SAM01 visual prefab instances.");

                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(UndoName);
                try
                {
                    var barrier = NewObject(scene, null, NewBarrier, Vector3.zero);
                    for (int i = 0; i < modules.Length; i++)
                    {
                        var instance = Instantiate(modules[i], barrier);
                        Undo.RecordObjects(new UnityEngine.Object[] { instance, instance.transform }, UndoName);
                        instance.name = $"{i+1:00} {(i == 7 ? "Lower spur" : "Main ridge")} / {ModuleNames[i]}";
                        instance.transform.localPosition = Positions[i];
                        instance.transform.localRotation = Quaternion.Euler(0,Yaws[i],0);
                        instance.transform.localScale = Scales[i];
                        Record(instance);
                    }
                    var plan = NewObject(scene, objectives.transform, NewPlan, Vector3.zero);
                    var sites = NewObject(scene, plan, "01 / SAM sites - visual only", Vector3.zero);
                    for (int i = 0; i < SamPositions.Length; i++)
                    {
                        GameObject instance;
                        if (i < existing.Length)
                        {
                            instance = existing[i].gameObject;
                            Undo.SetTransformParent(instance.transform, sites, UndoName);
                        }
                        else instance = Instantiate(sam, sites);
                        Undo.RecordObjects(new UnityEngine.Object[] { instance, instance.transform }, UndoName);
                        instance.name = $"SAM {(char)('A'+i)} - visual (R{SamRadii[i]})";
                        instance.transform.SetPositionAndRotation(SamPositions[i], Quaternion.Euler(0, i % 2 == 0 ? 135 : 225, 0));
                        Record(instance);
                    }
                    var recon = NewObject(scene, plan, "02 / Recon zone - empty markers", (Recon[0]+Recon[1]+Recon[2])/3f);
                    for (int i = 0; i < Recon.Length; i++)
                        NewObject(scene, recon, $"Recon {i+1:00}", Recon[i]);
                    NewObject(scene, plan, "03 / Optional objective - empty marker", ExtraTarget);
                    AddRoute(scene, plan, "04 / Main route - not enforced", MainRoute);
                    AddRoute(scene, plan, "05 / Optional route - not enforced", ExtraRoute);
                    // Only retire the previous layout once all replacements exist.
                    Undo.DestroyObjectImmediate(oldBarrier);
                    Undo.DestroyObjectImmediate(oldPlan.gameObject);
                    Undo.CollapseUndoOperations(group);
                    EditorSceneManager.MarkSceneDirty(scene);
                    Selection.activeGameObject = barrier.gameObject;
                    Debug.Log("MISSION-01H2C: camera-relative v2 installed. Eight cliffs, six visual SAMs and objective/route markers. Save scene after review. No combat or collision added.");
                }
                catch { Undo.RevertAllDownToGroup(group); throw; }
            }
            catch (Exception ex) { Debug.LogError("MISSION-01H2C: " + ex); }
        }

        private static Transform Child(Transform parent, string name)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.childCount; i++)
                if (parent.GetChild(i).name == name) return parent.GetChild(i);
            return null;
        }

        private static Transform NewObject(Scene scene, Transform parent, string name, Vector3 worldPosition)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            Undo.RegisterCreatedObjectUndo(go, UndoName);
            if (parent != null) Undo.SetTransformParent(go.transform, parent, UndoName);
            Undo.RecordObject(go.transform, UndoName);
            go.transform.SetPositionAndRotation(worldPosition, Quaternion.identity);
            return go.transform;
        }

        private static GameObject Instantiate(GameObject prefab, Transform parent)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            if (instance == null) throw new InvalidOperationException("Could not instantiate " + prefab.name);
            Undo.RegisterCreatedObjectUndo(instance, UndoName);
            return instance;
        }

        private static void Record(GameObject instance)
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
        }

        private static void AddRoute(Scene scene, Transform parent, string name, Vector3[] points)
        {
            var route = NewObject(scene, parent, name, Vector3.zero);
            for (int i = 0; i < points.Length; i++)
                NewObject(scene, route, $"{i+1:00}", points[i]);
        }
    }
}
