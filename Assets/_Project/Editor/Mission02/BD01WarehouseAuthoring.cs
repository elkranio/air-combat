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
    // BD01 visual-only trial. No damage, collision, depot or convoy changes.
    internal static class BD01WarehouseAuthoring
    {
        private const string Geometry = "Assets/_Project/Editor/Mission02/BD01Geometry.json";
        private const string ArtParent = "Assets/_Project/Art/Environment/Dominion";
        private const string Art = ArtParent + "/BD01_SupplyWarehouse";
        private const string Prefab = "Assets/_Project/Prefabs/Environment/BD01_SupplyWarehouse.prefab";
        private const string Mission = "Assets/_Project/Scenes/Mission02_ConvoyInterdiction.unity";
        private const string Preview = "M02 / BD01 warehouse visual trial";
        [Serializable] private sealed class Paint { public string name; public int[] rgb; }
        [Serializable] private sealed class Slot { public string material; public int[] triangles; }
        [Serializable] private sealed class Part { public string name; public Vector3 pivot; public Vector3[] vertices; public Slot[] slots; }
        [Serializable] private sealed class GeometryData { public Paint[] palette; public Part[] assemblies; }

        [MenuItem("Air Combat/Missions/M02 - Build BD01 Warehouse Preview")]
        private static void BuildAndPlace()
        {
            if (!TryScene(out var scene)) return;
            try
            {
                var roots = scene.GetRootGameObjects();
                if (roots.Any(r => r.name == Preview)) throw new InvalidOperationException("BD01 preview already exists; no changes made.");
                var trial = roots.SelectMany(r => r.GetComponentsInChildren<Mission02RouteTrial>(true)).Single();
                var terrain = roots.SelectMany(r => r.GetComponentsInChildren<Terrain>(true)).Single();
                if (terrain.terrainData == null) throw new InvalidOperationException("BD01 requires terrain data.");
                var session = trial.GetComponent<FlightSession>();
                var player = session != null ? new SerializedObject(session).FindProperty("player").objectReferenceValue as JetAssembly : null;
                if (player == null) throw new InvalidOperationException("BD01 missing authored player.");
                var flight = player.GetComponent<FlightController>();
                if (flight == null) throw new InvalidOperationException("BD01 missing flight controller.");
                Vector3 forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
                if (forward.sqrMagnitude < .5f) throw new InvalidOperationException("BD01 invalid runway heading.");
                var rotation = Quaternion.LookRotation(forward, Vector3.up);
                var right = Vector3.Cross(Vector3.up, forward);
                // Prefer clear terrain to the right of the airborne-start area,
                // outside the existing CA01 pickup footprint. Trial only, not B.
                Vector3 anchor = player.transform.position + forward * (flight.CruiseSpeed * 3.6f + 45f);
                var ca = roots.SingleOrDefault(r => r.name == "M02 / CA01 flight preview");
                if (ca != null) anchor = ca.transform.position;
                bool found = false; Vector3 point = default;
                foreach (float offset in new[] { 76f, 100f, 125f, 150f, -100f, -125f, -150f })
                {
                    point = anchor + right * offset;
                    if (TryPad(terrain, trial.OperationBounds, point, rotation, out float y))
                    { point.y = y; found = true; break; }
                }
                if (!found) throw new InvalidOperationException("BD01 could not find a flat airport-side test pad. No scene changes.");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
                if (prefab == null) prefab = BuildPrefab();
                Undo.IncrementCurrentGroup();
                int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("BD01 visual trial");
                try
                {
                    var root = new GameObject(Preview);
                    Undo.RegisterCreatedObjectUndo(root, "BD01 preview root");
                    root.transform.SetPositionAndRotation(point, rotation);
                    var instance = PrefabUtility.InstantiatePrefab(prefab, root.transform) as GameObject;
                    if (instance == null) throw new InvalidOperationException("BD01 instantiation failed.");
                    Undo.RegisterCreatedObjectUndo(instance, "BD01 preview instance");
                    instance.transform.localPosition = Vector3.zero;
                    instance.transform.localRotation = Quaternion.identity;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("BD01 scene save failed.");
                    Undo.CollapseUndoOperations(undo);
                    Selection.activeGameObject = root;
                    Debug.Log("BD01 visual preview installed. Select root and press F in Scene view. No combat/depot changes.", root);
                }
                catch { Undo.RevertAllDownToGroup(undo); throw; }
            }
            catch (Exception ex) { Debug.LogError("BD01: " + ex); }
        }

        private static bool TryPad(Terrain terrain, Rect operation, Vector3 p, Quaternion rotation, out float y)
        {
            y = float.NegativeInfinity;
            float low = float.PositiveInfinity;
            var origin = terrain.transform.position; var size = terrain.terrainData.size;
            for (int x = -1; x <= 1; x++)
            for (int z = -1; z <= 1; z++)
            {
                Vector3 sample = p + rotation * new Vector3(23f*x, 0f, 33f*z);
                if (!operation.Contains(new Vector2(sample.x, sample.z)) ||
                    sample.x < origin.x || sample.x > origin.x + size.x ||
                    sample.z < origin.z || sample.z > origin.z + size.z) return false;
                float h = terrain.SampleHeight(sample) + origin.y;
                low = Mathf.Min(low, h); y = Mathf.Max(y, h);
            }
            if (y - low > .5f) return false;
            // The preview is visual-only, but avoid overlapping existing collidable scene art.
            Physics.SyncTransforms();
            foreach (var collider in Physics.OverlapBox(new Vector3(p.x, y+6.15f, p.z),
                new Vector3(22.5f, 6f, 32.5f), rotation, ~0, QueryTriggerInteraction.Ignore))
                if (collider != null && !(collider is TerrainCollider)) return false;
            return true;
        }

        [MenuItem("Air Combat/Missions/M02 - Remove BD01 Warehouse Preview")]
        private static void Remove()
        {
            if (!TryScene(out var scene)) return;
            var root = scene.GetRootGameObjects().SingleOrDefault(r => r.name == Preview);
            if (root == null) return;
            Undo.DestroyObjectImmediate(root);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) Debug.LogError("BD01 preview removal: scene save failed.");
        }

        private static bool TryScene(out Scene scene)
        {
            scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode) return false;
            if (scene.path == Mission && !scene.isDirty) return true;
            Debug.LogError("BD01: open and save the existing M02 scene first."); return false;
        }

        private static GameObject BuildPrefab()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(Geometry);
            var data = asset != null ? JsonUtility.FromJson<GeometryData>(asset.text) : null;
            if (data == null || data.palette == null || data.assemblies == null || data.assemblies.Length != 8)
                throw new InvalidOperationException("BD01 geometry missing or invalid.");
            if (!AssetDatabase.IsValidFolder(ArtParent) || AssetDatabase.IsValidFolder(Art) || AssetDatabase.LoadMainAssetAtPath(Prefab) != null)
                throw new InvalidOperationException("BD01 asset path occupied or Dominion art folder missing. No overwrite.");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("BD01 requires URP/Lit.");
            var names = new HashSet<string>(data.palette.Select(p => p.name));
            if (names.Count != data.palette.Length || data.palette.Any(p => p.rgb == null || p.rgb.Length != 3 || p.rgb.Any(c => c < 0 || c > 255)))
                throw new InvalidOperationException("BD01 palette invalid.");
            var expected = new HashSet<string> { "Foundation", "LeftWall", "RightWall", "RearWall", "FrontWall", "Roof01", "Roof02", "Roof03" };
            if (!expected.SetEquals(data.assemblies.Select(p => p.name))) throw new InvalidOperationException("BD01 assembly contract mismatch.");
            foreach (var part in data.assemblies)
                if (part.vertices == null || part.slots == null || part.slots.Length == 0 ||
                    part.slots.Any(s => !names.Contains(s.material) || s.triangles == null || s.triangles.Length % 3 != 0 ||
                        s.triangles.Any(i => i < 0 || i >= part.vertices.Length)))
                    throw new InvalidOperationException("BD01 invalid mesh: " + part.name);
            var points = data.assemblies.SelectMany(p => p.vertices.Select(v => v + p.pivot)).ToArray();
            if (points.Length == 0 || Mathf.Abs(points.Min(p=>p.x)+22f)>.001f || Mathf.Abs(points.Max(p=>p.x)-22f)>.001f ||
                Mathf.Abs(points.Min(p=>p.y))>.001f || Mathf.Abs(points.Max(p=>p.y)-12f)>.001f ||
                Mathf.Abs(points.Min(p=>p.z)+32f)>.001f || Mathf.Abs(points.Max(p=>p.z)-32f)>.001f)
                throw new InvalidOperationException("BD01 source bounds differ from 44 x 12 x 64.");
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(ArtParent, "BD01_SupplyWarehouse")))
                throw new InvalidOperationException("BD01 could not create asset folder.");
            GameObject root = null;
            try
            {
                var materials = new Dictionary<string,Material>();
                foreach (var p in data.palette)
                {
                    var mat = new Material(shader) { name = "BD01_"+p.name };
                    mat.SetColor("_BaseColor", new Color32((byte)p.rgb[0],(byte)p.rgb[1],(byte)p.rgb[2],255));
                    mat.SetFloat("_Smoothness", .1f); mat.SetFloat("_Metallic", 0f);
                    AssetDatabase.CreateAsset(mat, Art+"/"+mat.name+".mat"); materials.Add(p.name,mat);
                }
                root = new GameObject("BD01_SupplyWarehouse");
                var visual = new GameObject("Visual"); visual.transform.SetParent(root.transform,false);
                foreach (var part in data.assemblies)
                {
                    var vertices = new List<Vector3>(); var triangles = new List<int[]>();
                    foreach (var slot in part.slots)
                    {
                        int start = vertices.Count;
                        foreach (int index in slot.triangles) vertices.Add(part.vertices[index]);
                        triangles.Add(Enumerable.Range(start,slot.triangles.Length).ToArray());
                    }
                    var mesh = new Mesh { name = "BD01_"+part.name, vertices = vertices.ToArray(), subMeshCount = triangles.Count };
                    for (int i=0;i<triangles.Count;i++) mesh.SetTriangles(triangles[i],i);
                    mesh.RecalculateNormals(); mesh.RecalculateBounds();
                    AssetDatabase.CreateAsset(mesh,Art+"/"+mesh.name+".asset");
                    var go = new GameObject(part.name,typeof(MeshFilter),typeof(MeshRenderer));
                    go.transform.SetParent(visual.transform,false); go.transform.localPosition=part.pivot;
                    go.GetComponent<MeshFilter>().sharedMesh=mesh;
                    go.GetComponent<MeshRenderer>().sharedMaterials=part.slots.Select(s=>materials[s.material]).ToArray();
                }
                var prefab = PrefabUtility.SaveAsPrefabAsset(root,Prefab,out bool success);
                if (!success || prefab == null) throw new InvalidOperationException("BD01 prefab save failed.");
                AssetDatabase.SaveAssets(); return prefab;
            }
            catch { AssetDatabase.DeleteAsset(Prefab); AssetDatabase.DeleteAsset(Art); throw; }
            finally { if (root != null) Object.DestroyImmediate(root); }
        }
    }
}
