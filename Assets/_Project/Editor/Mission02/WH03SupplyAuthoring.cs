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
    // WH03 visual-only trial. No damage, collision, depot or convoy changes.
    internal static class WH03SupplyAuthoring
    {
        private const string Geometry = "Assets/_Project/Editor/Mission02/WH03Geometry.json";
        private const string ArtParent = "Assets/_Project/Art/Environment/Dominion";
        private const string Art = ArtParent + "/WH03_SupplyShelter";
        private const string Prefab = "Assets/_Project/Prefabs/Environment/WH03_SupplyShelter.prefab";
        private const string Mission = "Assets/_Project/Scenes/Mission02_ConvoyInterdiction.unity";
        private const string Preview = "M02 / WH03 supply visual trial";
        [Serializable] private sealed class Paint { public string name; public int[] rgb; }
        [Serializable] private sealed class Slot { public string material; public int[] triangles; }
        [Serializable] private sealed class Part { public string name; public Vector3 pivot; public Vector3[] vertices; public Slot[] slots; }
        [Serializable] private sealed class GeometryData { public Paint[] palette; public Part[] assemblies; }

        [MenuItem("Air Combat/Missions/M02 - Build WH03 Supply Preview")]
        private static void BuildAndPlace()
        {
            if (!TryScene(out var scene)) return;
            try
            {
                var roots = scene.GetRootGameObjects();
                if (roots.Any(r => r.name == Preview)) throw new InvalidOperationException("WH03 preview already exists; no changes made.");
                var trial = roots.SelectMany(r => r.GetComponentsInChildren<Mission02RouteTrial>(true)).Single();
                var terrain = roots.SelectMany(r => r.GetComponentsInChildren<Terrain>(true)).Single();
                if (terrain.terrainData == null) throw new InvalidOperationException("WH03 requires terrain data.");
                var session = trial.GetComponent<FlightSession>();
                var player = session != null ? new SerializedObject(session).FindProperty("player").objectReferenceValue as JetAssembly : null;
                if (player == null) throw new InvalidOperationException("WH03 missing authored player.");
                var flight = player.GetComponent<FlightController>();
                if (flight == null) throw new InvalidOperationException("WH03 missing flight controller.");
                Vector3 forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
                if (forward.sqrMagnitude < .5f) throw new InvalidOperationException("WH03 invalid runway heading.");
                var rotation = Quaternion.LookRotation(forward, Vector3.up);
                var right = Vector3.Cross(Vector3.up, forward);
                // Both existing visual-only comparisons have no colliders.
                // Use conservative centre spacing in addition to the physics pad check.
                var warehouse = roots.SingleOrDefault(r => r.name == "M02 / BD01 warehouse visual trial");
                var equipment = roots.SingleOrDefault(r => r.name == "M02 / WH02 equipment visual trial");
                if (warehouse == null || equipment == null) throw new InvalidOperationException("Install WH01 and WH02 previews first.");
                Vector3 anchor = equipment.transform.position;
                var ca = roots.SingleOrDefault(r => r.name == "M02 / CA01 flight preview");
                bool found = false; Vector3 point = default;
                foreach (var offset in new[] { forward*34f, -forward*34f, right*32f, -right*32f,
                    forward*45f, -forward*45f, right*45f, -right*45f, right*36f+forward*28f })
                {
                    point = anchor + offset;
                    if (Vector3.ProjectOnPlane(point-warehouse.transform.position,Vector3.up).sqrMagnitude < 33f*33f) continue;
                    if (ca != null && Vector3.ProjectOnPlane(point-ca.transform.position,Vector3.up).sqrMagnitude < 45f*45f) continue;
                    if (TryPad(terrain, trial.OperationBounds, point, rotation, out float y))
                    { point.y = y; found = true; break; }
                }
                if (!found) throw new InvalidOperationException("WH03 could not find a flat airport-side test pad. No scene changes.");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
                if (prefab == null) prefab = BuildPrefab();
                Undo.IncrementCurrentGroup();
                int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("WH03 visual trial");
                try
                {
                    var root = new GameObject(Preview);
                    Undo.RegisterCreatedObjectUndo(root, "WH03 preview root");
                    root.transform.SetPositionAndRotation(point, rotation);
                    var instance = PrefabUtility.InstantiatePrefab(prefab, root.transform) as GameObject;
                    if (instance == null) throw new InvalidOperationException("WH03 instantiation failed.");
                    Undo.RegisterCreatedObjectUndo(instance, "WH03 preview instance");
                    instance.transform.localPosition = Vector3.zero;
                    instance.transform.localRotation = Quaternion.identity;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("WH03 scene save failed.");
                    Undo.CollapseUndoOperations(undo);
                    Selection.activeGameObject = root;
                    Debug.Log("WH03 visual preview installed. Select root and press F in Scene view. No combat/depot changes.", root);
                }
                catch { Undo.RevertAllDownToGroup(undo); throw; }
            }
            catch (Exception ex) { Debug.LogError("WH03: " + ex); }
        }

        private static bool TryPad(Terrain terrain, Rect operation, Vector3 p, Quaternion rotation, out float y)
        {
            y = float.NegativeInfinity;
            float low = float.PositiveInfinity;
            var origin = terrain.transform.position; var size = terrain.terrainData.size;
            for (int x = -1; x <= 1; x++)
            for (int z = -1; z <= 1; z++)
            {
                Vector3 sample = p + rotation * new Vector3(9f*x, 0f, 12f*z);
                if (!operation.Contains(new Vector2(sample.x, sample.z)) ||
                    sample.x < origin.x || sample.x > origin.x + size.x ||
                    sample.z < origin.z || sample.z > origin.z + size.z) return false;
                float h = terrain.SampleHeight(sample) + origin.y;
                low = Mathf.Min(low, h); y = Mathf.Max(y, h);
            }
            if (y - low > .5f) return false;
            // The preview is visual-only, but avoid overlapping existing collidable scene art.
            Physics.SyncTransforms();
            foreach (var collider in Physics.OverlapBox(new Vector3(p.x, y+3.65f, p.z),
                new Vector3(8.5f, 3.5f, 11.5f), rotation, ~0, QueryTriggerInteraction.Ignore))
                if (collider != null && !(collider is TerrainCollider)) return false;
            return true;
        }

        [MenuItem("Air Combat/Missions/M02 - Remove WH03 Supply Preview")]
        private static void Remove()
        {
            if (!TryScene(out var scene)) return;
            var root = scene.GetRootGameObjects().SingleOrDefault(r => r.name == Preview);
            if (root == null) return;
            Undo.DestroyObjectImmediate(root);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) Debug.LogError("WH03 preview removal: scene save failed.");
        }

        private static bool TryScene(out Scene scene)
        {
            scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode) return false;
            if (scene.path == Mission && !scene.isDirty) return true;
            Debug.LogError("WH03: open and save the existing M02 scene first."); return false;
        }

        private static GeometryData ReadGeometry()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(Geometry);
            var data = asset != null ? JsonUtility.FromJson<GeometryData>(asset.text) : null;
            if (data == null || data.palette == null || data.assemblies == null || data.assemblies.Length != 9)
                throw new InvalidOperationException("WH03 geometry missing or invalid.");
            var names = new HashSet<string>(data.palette.Select(p => p.name));
            if (names.Count != data.palette.Length || data.palette.Any(p => p.rgb == null || p.rgb.Length != 3 || p.rgb.Any(c => c < 0 || c > 255)))
                throw new InvalidOperationException("WH03 palette invalid.");
            var expected = new HashSet<string> { "Foundation", "LeftWall", "RightWall", "RearWall", "FrontWall", "Roof01", "Roof02", "Cargo01", "Cargo02" };
            if (!expected.SetEquals(data.assemblies.Select(p => p.name))) throw new InvalidOperationException("WH03 assembly contract mismatch.");
            foreach (var part in data.assemblies)
                if (part.vertices == null || part.slots == null || part.slots.Length == 0 ||
                    part.slots.Any(s => !names.Contains(s.material) || s.triangles == null || s.triangles.Length % 3 != 0 ||
                        s.triangles.Any(i => i < 0 || i >= part.vertices.Length)))
                    throw new InvalidOperationException("WH03 invalid mesh: " + part.name);
            var points = data.assemblies.SelectMany(p => p.vertices.Select(v => v + p.pivot)).ToArray();
            if (points.Length == 0 || Mathf.Abs(points.Min(p=>p.x)+8f)>.001f || Mathf.Abs(points.Max(p=>p.x)-8f)>.001f ||
                Mathf.Abs(points.Min(p=>p.y))>.001f || Mathf.Abs(points.Max(p=>p.y)-7f)>.001f ||
                Mathf.Abs(points.Min(p=>p.z)+11f)>.001f || Mathf.Abs(points.Max(p=>p.z)-11f)>.001f)
                throw new InvalidOperationException("WH03 source bounds differ from 16 x 7 x 22.");
            return data;
        }

        private static void FillMesh(Mesh mesh, Part part)
        {
            var vertices = new List<Vector3>(); var triangles = new List<int[]>();
            foreach (var slot in part.slots)
            {
                int start = vertices.Count;
                foreach (int index in slot.triangles) vertices.Add(part.vertices[index]);
                triangles.Add(Enumerable.Range(start, slot.triangles.Length).ToArray());
            }
            mesh.Clear(); mesh.vertices = vertices.ToArray(); mesh.subMeshCount = triangles.Count;
            for (int i = 0; i < triangles.Count; i++) mesh.SetTriangles(triangles[i], i);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
        }

        private static GameObject BuildPrefab()
        {
            var data = ReadGeometry();
            if (!AssetDatabase.IsValidFolder(ArtParent) || AssetDatabase.IsValidFolder(Art) || AssetDatabase.LoadMainAssetAtPath(Prefab) != null)
                throw new InvalidOperationException("WH03 asset path occupied or Dominion art folder missing. No overwrite.");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("WH03 requires URP/Lit.");
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(ArtParent, "WH03_SupplyShelter")))
                throw new InvalidOperationException("WH03 could not create asset folder.");
            GameObject root = null;
            try
            {
                var materials = new Dictionary<string,Material>();
                foreach (var p in data.palette)
                {
                    var mat = new Material(shader) { name = "WH03_"+p.name };
                    mat.SetColor("_BaseColor", new Color32((byte)p.rgb[0],(byte)p.rgb[1],(byte)p.rgb[2],255));
                    mat.SetFloat("_Smoothness", .1f); mat.SetFloat("_Metallic", 0f);
                    AssetDatabase.CreateAsset(mat, Art+"/"+mat.name+".mat"); materials.Add(p.name,mat);
                }
                root = new GameObject("WH03_SupplyShelter");
                var visual = new GameObject("Visual"); visual.transform.SetParent(root.transform,false);
                foreach (var part in data.assemblies)
                {
                    var mesh = new Mesh { name = "WH03_"+part.name };
                    FillMesh(mesh, part);
                    AssetDatabase.CreateAsset(mesh,Art+"/"+mesh.name+".asset");
                    var go = new GameObject(part.name,typeof(MeshFilter),typeof(MeshRenderer));
                    go.transform.SetParent(visual.transform,false); go.transform.localPosition=part.pivot;
                    go.GetComponent<MeshFilter>().sharedMesh=mesh;
                    go.GetComponent<MeshRenderer>().sharedMaterials=part.slots.Select(s=>materials[s.material]).ToArray();
                }
                var prefab = PrefabUtility.SaveAsPrefabAsset(root,Prefab,out bool success);
                if (!success || prefab == null) throw new InvalidOperationException("WH03 prefab save failed.");
                AssetDatabase.SaveAssets(); return prefab;
            }
            catch { AssetDatabase.DeleteAsset(Prefab); AssetDatabase.DeleteAsset(Art); throw; }
            finally { if (root != null) Object.DestroyImmediate(root); }
        }
    }
}
