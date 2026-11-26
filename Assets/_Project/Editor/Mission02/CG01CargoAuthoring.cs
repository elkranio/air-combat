using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AirCombat
{
    // Visual-only cargo. Source geometry is shared with the editable Blender model.
    internal static class CG01CargoAuthoring
    {
        private const string Geometry = "Assets/_Project/Editor/Mission02/CG01Geometry.json";
        private const string ArtParent = "Assets/_Project/Art/Environment/Dominion";
        private const string Art = ArtParent + "/CG01_SpecialCargo";
        private const string Prefab = "Assets/_Project/Prefabs/Environment/CG01_SpecialCargo.prefab";
        private const string Mission = "Assets/_Project/Scenes/Mission02_ConvoyInterdiction.unity";
        private const string Preview = "M02 / CG01 cargo visual trial";
        [Serializable] private sealed class Paint { public string name; public int[] rgb; }
        [Serializable] private sealed class Slot { public string material; public int[] triangles; }
        [Serializable] private sealed class Part { public string name; public Vector3 pivot; public Vector3[] vertices; public Slot[] slots; }
        [Serializable] private sealed class GeometryData { public Paint[] palette; public Part[] assemblies; public Vector3[] sockets; }

        [MenuItem("Air Combat/Missions/M02 - Build CG01 Cargo Preview")]
        private static void BuildAndPlace()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != Mission || scene.isDirty)
            { Debug.LogError("CG01: open and save the existing M02 scene first."); return; }
            try
            {
                var roots = scene.GetRootGameObjects();
                var trial = roots.SelectMany(r => r.GetComponentsInChildren<Mission02RouteTrial>(true)).Single();
                if (!trial.HasSecondPoint) throw new InvalidOperationException("CG01 requires the two-target M02 layout.");
                if (roots.Any(r => r.name == Preview)) throw new InvalidOperationException("CG01 preview already exists; no changes made.");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
                if (prefab == null) prefab = BuildPrefab();
                if (prefab == null) throw new InvalidOperationException("CG01 prefab creation failed.");
                var terrain = roots.SelectMany(r => r.GetComponentsInChildren<Terrain>(true)).Single();
                if (terrain.terrainData == null) throw new InvalidOperationException("CG01 requires terrain data.");
                Undo.IncrementCurrentGroup();
                int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("CG01 visual trial");
                try
                {
                    var root = new GameObject(Preview);
                    Undo.RegisterCreatedObjectUndo(root, "CG01 preview root");
                    Place(prefab, root.transform, terrain, trial.PointC, 24f, "C1 / Special Cargo");
                    Place(prefab, root.transform, terrain, trial.PointC2, -38f, "C2 / Special Cargo");
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("CG01 scene save failed.");
                    Undo.CollapseUndoOperations(undo);
                    Selection.activeGameObject = root;
                    Debug.Log("CG01 installed at C1/C2. Visual only: no collider, damage, deadline or Carryall yet.", root);
                }
                catch { Undo.RevertAllDownToGroup(undo); throw; }
            }
            catch (Exception ex) { Debug.LogError("CG01: " + ex); }
        }

        private static void Place(GameObject prefab, Transform parent, Terrain terrain, Vector3 point, float yaw, string name)
        {
            var go = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (go == null) throw new InvalidOperationException("CG01 instantiation failed.");
            Undo.RegisterCreatedObjectUndo(go, "CG01 preview instance");
            go.name = name;
            point.y = terrain.SampleHeight(point) + terrain.transform.position.y;
            go.transform.SetPositionAndRotation(point, Quaternion.Euler(0f,yaw,0f));
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        }

        private static GameObject BuildPrefab()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(Geometry);
            var data = asset != null ? JsonUtility.FromJson<GeometryData>(asset.text) : null;
            if (data == null || data.palette == null || data.assemblies == null || data.assemblies.Length != 13 ||
                data.sockets == null || data.sockets.Length != 4)
                throw new InvalidOperationException("CG01 geometry missing or invalid.");
            if (!AssetDatabase.IsValidFolder(ArtParent) || AssetDatabase.IsValidFolder(Art) || AssetDatabase.LoadMainAssetAtPath(Prefab) != null)
                throw new InvalidOperationException("CG01 asset path occupied or Dominion art folder missing. No overwrite.");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("CG01 requires URP/Lit.");
            // Validate the whole source before creating assets.
            var names = new HashSet<string>(data.palette.Select(p => p.name));
            if (names.Count != data.palette.Length || data.palette.Any(p => p.rgb == null || p.rgb.Length != 3 || p.rgb.Any(c => c < 0 || c > 255)))
                throw new InvalidOperationException("CG01 palette invalid.");
            if (data.assemblies.Select(p => p.name).Distinct().Count() != data.assemblies.Length)
                throw new InvalidOperationException("CG01 duplicate assembly.");
            foreach (var part in data.assemblies)
                if (part.vertices == null || part.slots == null || part.slots.Length == 0 ||
                    part.slots.Any(s => !names.Contains(s.material) || s.triangles == null || s.triangles.Length % 3 != 0 ||
                        s.triangles.Any(i => i < 0 || i >= part.vertices.Length)))
                    throw new InvalidOperationException("CG01 invalid mesh: " + part.name);
            var points = data.assemblies.SelectMany(p => p.vertices.Select(v => v + p.pivot)).ToArray();
            if (points.Length == 0 || Mathf.Abs(points.Min(p=>p.x)+4f)>.001f || Mathf.Abs(points.Max(p=>p.x)-4f)>.001f ||
                Mathf.Abs(points.Min(p=>p.y))>.001f || Mathf.Abs(points.Max(p=>p.y)-5f)>.001f ||
                Mathf.Abs(points.Min(p=>p.z)+8f)>.001f || Mathf.Abs(points.Max(p=>p.z)-8f)>.001f)
                throw new InvalidOperationException("CG01 source bounds differ from 8 x 5 x 16.");
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(ArtParent, "CG01_SpecialCargo")))
                throw new InvalidOperationException("CG01 could not create asset folder.");
            GameObject root = null;
            try
            {
                var materials = new Dictionary<string,Material>();
                foreach (var p in data.palette)
                {
                    var mat = new Material(shader) { name = "CG01_"+p.name };
                    mat.SetColor("_BaseColor", new Color32((byte)p.rgb[0],(byte)p.rgb[1],(byte)p.rgb[2],255));
                    mat.SetFloat("_Smoothness", .12f); mat.SetFloat("_Metallic", 0f);
                    AssetDatabase.CreateAsset(mat, Art+"/"+mat.name+".mat"); materials.Add(p.name,mat);
                }
                root = new GameObject("CG01_SpecialCargo");
                var visual = new GameObject("Visual"); visual.transform.SetParent(root.transform,false);
                foreach (var part in data.assemblies)
                {
                    // Duplicate vertices per triangle: intentionally flat shading, no smoothed armor corners.
                    var vertices = new List<Vector3>(); var triangles = new List<int[]>();
                    foreach (var slot in part.slots)
                    {
                        int start = vertices.Count;
                        foreach (int index in slot.triangles) vertices.Add(part.vertices[index]);
                        triangles.Add(Enumerable.Range(start,slot.triangles.Length).ToArray());
                    }
                    var mesh = new Mesh { name = "CG01_"+part.name, vertices = vertices.ToArray(), subMeshCount = triangles.Count };
                    for (int i=0;i<triangles.Count;i++) mesh.SetTriangles(triangles[i],i);
                    mesh.RecalculateNormals(); mesh.RecalculateBounds();
                    AssetDatabase.CreateAsset(mesh,Art+"/"+mesh.name+".asset");
                    var go = new GameObject(part.name,typeof(MeshFilter),typeof(MeshRenderer));
                    go.transform.SetParent(visual.transform,false); go.transform.localPosition=part.pivot;
                    go.GetComponent<MeshFilter>().sharedMesh=mesh;
                    go.GetComponent<MeshRenderer>().sharedMaterials=part.slots.Select(s=>materials[s.material]).ToArray();
                }
                var capture = new GameObject("CaptureSockets"); capture.transform.SetParent(root.transform,false);
                for (int i=0;i<data.sockets.Length;i++)
                {
                    var socket = new GameObject($"Grab_{i+1:00}"); socket.transform.SetParent(capture.transform,false);
                    socket.transform.localPosition=data.sockets[i];
                }
                var prefab = PrefabUtility.SaveAsPrefabAsset(root,Prefab,out bool success);
                if (!success || prefab == null) throw new InvalidOperationException("CG01 prefab save failed.");
                AssetDatabase.SaveAssets(); return prefab;
            }
            catch
            {
                AssetDatabase.DeleteAsset(Prefab); AssetDatabase.DeleteAsset(Art); throw;
            }
            finally { if (root != null) Object.DestroyImmediate(root); }
        }
    }
}
