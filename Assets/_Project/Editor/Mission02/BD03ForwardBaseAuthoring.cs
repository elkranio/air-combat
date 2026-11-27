using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    // Install once, not a scene generator. All positions use the final convoy
    // leg's local frame: X = right, Z = forward from B. No runtime placement.
    internal static class BD03ForwardBaseAuthoring
    {
        private const string Mission = "Assets/_Project/Scenes/Mission02_ConvoyInterdiction.unity";
        private const string RootName = "M02 / BD03 forward base";
        private const string Prefabs = "Assets/_Project/Prefabs/Environment/";
        private const float MarchClearance = 12f;
        private static readonly string[] PreviewNames = {
            "M02 / BD01 warehouse visual trial", "M02 / WH02 equipment visual trial", "M02 / WH03 supply visual trial" };
        private static readonly string[] Types = { "BD01_SupplyWarehouse", "WH02_EquipmentShelter", "WH03_SupplyShelter" };
        private static readonly Vector3[] Sizes = { new Vector3(18,9,28), new Vector3(14,6,20), new Vector3(16,7,22) };
        private sealed class Slot
        {
            public readonly int type;
            public readonly string name;
            public readonly Vector2 centre;
            public Vector3 world;
            public Slot(int type, string name, float x, float z)
            { this.type = type; this.name = name; centre = new Vector2(x,z); }
        }
        private static Slot[] Slots() => new[] {
            new Slot(0,"WH01 / Warehouse A",-44,65),
            new Slot(0,"WH01 / Warehouse B",0,65),
            new Slot(0,"WH01 / Warehouse C",44,65),
            new Slot(1,"WH02 / Equipment",-30,110),
            new Slot(2,"WH03 / Supply",29,113)
        };

        [MenuItem("Air Combat/Missions/M02 - Install BD03 Forward Base")]
        private static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != Mission || scene.isDirty)
            { Debug.LogError("BD03: open and save the existing M02 scene first."); return; }
            try
            {
                var roots = scene.GetRootGameObjects();
                if (roots.Any(r => r.name == RootName))
                    throw new InvalidOperationException("BD03 already installed. No changes made.");
                var trial = roots.SelectMany(r => r.GetComponentsInChildren<Mission02RouteTrial>(true)).Single();
                var serialized = new SerializedObject(trial);
                var terrain = serialized.FindProperty("convoyTerrain").objectReferenceValue as Terrain;
                var line = serialized.FindProperty("convoyRoute").objectReferenceValue as LineRenderer;
                if (terrain == null || terrain.terrainData == null || line == null || line.positionCount < 2)
                    throw new InvalidOperationException("BD03 requires the authored convoy terrain and route.");
                var points = new Vector3[line.positionCount];
                line.GetPositions(points);
                for (int i = 0; i < points.Length; i++)
                {
                    if (!line.useWorldSpace) points[i] = line.transform.TransformPoint(points[i]);
                    points[i].y = 0f;
                }
                Vector3 end = points[points.Length-1];
                Vector3 delta = end - points[points.Length-2];
                Vector3 b = trial.PointB; b.y = 0f;
                if (delta.magnitude <= 100f || (b-end).sqrMagnitude > 1f)
                    throw new InvalidOperationException("BD03 needs a final straight leg >100 and route endpoint at B.");
                Quaternion rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
                Quaternion inverse = Quaternion.Inverse(rotation);
                var prefabs = new GameObject[Types.Length];
                for (int i = 0; i < Types.Length; i++)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs+Types[i]+".prefab");
                    var box = prefab != null ? prefab.GetComponent<BoxCollider>() : null;
                    if (prefab == null || prefab.GetComponent<DepotBuildingTarget>() == null || box == null ||
                        !box.enabled || box.isTrigger || (box.size-Sizes[i]).sqrMagnitude > .001f ||
                        (box.center-Vector3.up*(Sizes[i].y*.5f)).sqrMagnitude > .001f ||
                        (prefab.transform.localScale-Vector3.one).sqrMagnitude > .001f)
                        throw new InvalidOperationException("BD03: run BD02 prefab upgrade first; invalid "+Types[i]);
                    prefabs[i] = prefab;
                }
                // Remove only known single-prefab comparison stands, not any
                // arbitrary artist work placed beneath them.
                var previews = new List<GameObject>();
                for (int i = 0; i < PreviewNames.Length; i++)
                {
                    var matches = roots.Where(r=>r.name==PreviewNames[i]).ToArray();
                    if (matches.Length==0) continue;
                    if (matches.Length!=1 || matches[0].transform.childCount!=1 ||
                        PrefabUtility.GetCorrespondingObjectFromSource(matches[0].transform.GetChild(0).gameObject)!=prefabs[i])
                        throw new InvalidOperationException("BD03: unexpected comparison root: "+PreviewNames[i]);
                    previews.Add(matches[0]);
                }
                var slots = Slots();
                Physics.SyncTransforms();
                for (int i = 0; i < slots.Length; i++)
                {
                    Slot slot = slots[i]; Vector3 size = Sizes[slot.type];
                    var half = new Vector2(size.x*.5f,size.z*.5f);
                    // Reserve current BD02 convoy's full parking band (the
                    // existing quarter-turn advances 18 units beyond B).
                    if (slot.centre.y-half.y < 18f+MarchClearance)
                        throw new InvalidOperationException(slot.name+": inside reserved deployment band.");
                    for (int j=0;j<i;j++)
                    {
                        Slot other=slots[j]; Vector3 os=Sizes[other.type];
                        Vector2 d=slot.centre-other.centre;
                        if (Mathf.Abs(d.x)<half.x+os.x*.5f+8f && Mathf.Abs(d.y)<half.y+os.z*.5f+8f)
                            throw new InvalidOperationException(slot.name+": overlaps "+other.name+" clearance.");
                    }
                    // Analytic segment/AABB intersection in base-local XZ;
                    // expand the building for a conservative vehicle envelope.
                    var reserve = half + Vector2.one*MarchClearance;
                    for (int j=1;j<points.Length;j++)
                    {
                        Vector3 a=inverse*(points[j-1]-end), c=inverse*(points[j]-end);
                        if (Intersects(new Vector2(a.x,a.z)-slot.centre,new Vector2(c.x,c.z)-slot.centre,reserve))
                            throw new InvalidOperationException(slot.name+": obstructs convoy route.");
                    }
                    slot.world=end+rotation*new Vector3(slot.centre.x,0f,slot.centre.y);
                    slot.world.y=CheckPad(terrain,trial.OperationBounds,slot.world,rotation,size,slot.name,previews);
                }
                Undo.IncrementCurrentGroup(); int undo=Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("BD03 forward base");
                try
                {
                    var root=new GameObject(RootName);
                    Undo.RegisterCreatedObjectUndo(root,"BD03 root");
                    root.transform.SetPositionAndRotation(end,rotation);
                    foreach (var slot in slots)
                    {
                        var instance=PrefabUtility.InstantiatePrefab(prefabs[slot.type],root.transform) as GameObject;
                        if(instance==null) throw new InvalidOperationException("BD03 prefab instantiation failed.");
                        Undo.RegisterCreatedObjectUndo(instance,"BD03 building");
                        instance.name=slot.name;
                        instance.transform.SetPositionAndRotation(slot.world,rotation);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                    }
                    foreach(var preview in previews) Undo.DestroyObjectImmediate(preview);
                    EditorSceneManager.MarkSceneDirty(scene);
                    if(!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("BD03 scene save failed.");
                    Undo.CollapseUndoOperations(undo); Selection.activeGameObject=root;
                    Debug.Log("BD03: five targets installed at B. Existing route/deployment band reserved. F frames base; BD04 owns new slots.",root);
                }
                catch { Undo.RevertAllDownToGroup(undo); throw; }
            }
            catch(Exception ex) { Debug.LogError("BD03: "+ex); }
        }

        [MenuItem("Air Combat/Missions/M02 - Apply BD04 Base Layout and Trial")]
        private static void ApplyLayoutV2()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != Mission || scene.isDirty)
            { Debug.LogError("BD04: open and save M02 first."); return; }
            try
            {
                var roots = scene.GetRootGameObjects();
                var root = roots.Single(r => r.name == RootName);
                var trial = roots.SelectMany(r => r.GetComponentsInChildren<Mission02RouteTrial>(true)).Single();
                var serialized = new SerializedObject(trial);
                var terrain = serialized.FindProperty("convoyTerrain").objectReferenceValue as Terrain;
                var line = serialized.FindProperty("convoyRoute").objectReferenceValue as LineRenderer;
                if (terrain == null || terrain.terrainData == null || line == null || line.positionCount < 2)
                    throw new InvalidOperationException("Missing terrain or route.");
                var points = new Vector3[line.positionCount]; line.GetPositions(points);
                for (int i = 0; i < points.Length; i++)
                { if (!line.useWorldSpace) points[i] = line.transform.TransformPoint(points[i]); points[i].y = 0f; }
                Vector3 end = points[points.Length-1], delta = end - points[points.Length-2];
                Vector3 b = trial.PointB; b.y = 0f;
                if (delta.magnitude <= 380f || (end-b).sqrMagnitude > 1f)
                    throw new InvalidOperationException("BD04 requires final straight leg >380 and endpoint B.");
                Quaternion rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
                var slots = Slots();
                if (root.transform.childCount != slots.Length)
                    throw new InvalidOperationException("Expected five existing BD03 buildings.");
                var instances = new Transform[slots.Length];
                Physics.SyncTransforms();
                for (int i = 0; i < slots.Length; i++)
                {
                    var slot = slots[i]; var size = Sizes[slot.type];
                    var tr = root.transform.Cast<Transform>().SingleOrDefault(t => t.name == slot.name);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs+Types[slot.type]+".prefab");
                    if (tr == null || prefab == null || PrefabUtility.GetCorrespondingObjectFromSource(tr.gameObject) != prefab ||
                        tr.GetComponent<DepotBuildingTarget>() == null || tr.GetComponent<BoxCollider>() == null)
                        throw new InvalidOperationException("Unexpected building: "+slot.name);
                    instances[i] = tr;
                    for (int j = 0; j < i; j++)
                    {
                        var other = slots[j]; var os = Sizes[other.type]; var d = slot.centre-other.centre;
                        if (Mathf.Abs(d.x) < (size.x+os.x)*.5f+8f && Mathf.Abs(d.y) < (size.z+os.z)*.5f+8f)
                            throw new InvalidOperationException("Building clearance: "+slot.name);
                    }
                    slot.world = end+rotation*new Vector3(slot.centre.x,0f,slot.centre.y);
                    slot.world.y = CheckPad(terrain,trial.OperationBounds,slot.world,rotation,size,slot.name,new List<GameObject>{root});
                }
                Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("BD04 base layout and trial");
                try
                {
                    for (int i = 0; i < slots.Length; i++)
                    {
                        Undo.RecordObject(instances[i], "BD04 move building");
                        instances[i].SetPositionAndRotation(slots[i].world,rotation);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(instances[i]);
                    }
                    Undo.RecordObject(trial,"BD04 enable trial");
                    serialized.Update();
                    serialized.FindProperty("baseDeploymentTrial").boolValue = true;
                    serialized.FindProperty("baseTrialHoldAircraft").boolValue = true;
                    serialized.FindProperty("carryallFiringRange").boolValue = false;
                    serialized.ApplyModifiedProperties();
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("BD04 save failed.");
                    Undo.CollapseUndoOperations(undo); Selection.activeGameObject = root;
                    Debug.Log("BD04: moved five existing buildings. Base Deployment Trial enabled; Play / R.",root);
                }
                catch { Undo.RevertAllDownToGroup(undo); throw; }
            }
            catch (Exception ex) { Debug.LogError("BD04: "+ex); }
        }

        private static bool Intersects(Vector2 a,Vector2 b,Vector2 half)
        {
            float lo=0f,hi=1f; Vector2 d=b-a;
            for(int i=0;i<2;i++)
            {
                if(Mathf.Abs(d[i])<.00001f)
                { if(Mathf.Abs(a[i])>half[i]) return false; continue; }
                float t0=(-half[i]-a[i])/d[i],t1=(half[i]-a[i])/d[i];
                lo=Mathf.Max(lo,Mathf.Min(t0,t1)); hi=Mathf.Min(hi,Mathf.Max(t0,t1));
                if(lo>hi) return false;
            }
            return true;
        }

        private static float CheckPad(Terrain terrain,Rect operation,Vector3 p,Quaternion rotation,
            Vector3 size,string name,List<GameObject> previews)
        {
            var origin=terrain.transform.position; var extent=terrain.terrainData.size;
            float low=float.PositiveInfinity,high=float.NegativeInfinity;
            for(int x=-1;x<=1;x++) for(int z=-1;z<=1;z++)
            {
                Vector3 sample=p+rotation*new Vector3(x*(size.x*.5f+1f),0f,z*(size.z*.5f+1f));
                if(!operation.Contains(new Vector2(sample.x,sample.z)) || sample.x<origin.x || sample.x>origin.x+extent.x ||
                    sample.z<origin.z || sample.z>origin.z+extent.z)
                    throw new InvalidOperationException(name+": outside terrain or operation bounds.");
                float height=terrain.SampleHeight(sample)+origin.y;
                low=Mathf.Min(low,height); high=Mathf.Max(high,height);
            }
            if(high-low>.5f) throw new InvalidOperationException(name+": terrain is not flat enough.");
            Vector3 centre=new Vector3(p.x,high+size.y*.5f+.1f,p.z);
            foreach(var collider in Physics.OverlapBox(centre,size*.5f+new Vector3(1f,0f,1f),rotation,~0,QueryTriggerInteraction.Ignore))
            {
                if(collider==null || collider is TerrainCollider || previews.Any(r=>collider.transform.IsChildOf(r.transform))) continue;
                throw new InvalidOperationException(name+": overlaps "+collider.name);
            }
            return high;
        }
    }
}
