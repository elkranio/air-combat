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
    // WH02 visual-only trial. No damage, collision, depot or convoy changes.
    internal static class WH02EquipmentAuthoring
    {
        private const string Geometry = "Assets/_Project/Editor/Mission02/WH02Geometry.json";
        private const string ArtParent = "Assets/_Project/Art/Environment/Dominion";
        private const string Art = ArtParent + "/WH02_EquipmentShelter";
        private const string Prefab = "Assets/_Project/Prefabs/Environment/WH02_EquipmentShelter.prefab";
        private const string Mission = "Assets/_Project/Scenes/Mission02_ConvoyInterdiction.unity";
        private const string Preview = "M02 / WH02 equipment visual trial";
        [Serializable] private sealed class Paint { public string name; public int[] rgb; }
        [Serializable] private sealed class Slot { public string material; public int[] triangles; }
        [Serializable] private sealed class Part { public string name; public Vector3 pivot; public Vector3[] vertices; public Slot[] slots; }
        [Serializable] private sealed class GeometryData { public Paint[] palette; public Part[] assemblies; }

        [MenuItem("Air Combat/Missions/M02 - Build WH02 Equipment Preview")]
        private static void BuildAndPlace()
        {
            if (!TryScene(out var scene)) return;
            try
            {
                var roots = scene.GetRootGameObjects();
                if (roots.Any(r => r.name == Preview)) throw new InvalidOperationException("WH02 preview already exists; no changes made.");
                var trial = roots.SelectMany(r => r.GetComponentsInChildren<Mission02RouteTrial>(true)).Single();
                var terrain = roots.SelectMany(r => r.GetComponentsInChildren<Terrain>(true)).Single();
                if (terrain.terrainData == null) throw new InvalidOperationException("WH02 requires terrain data.");
                var session = trial.GetComponent<FlightSession>();
                var player = session != null ? new SerializedObject(session).FindProperty("player").objectReferenceValue as JetAssembly : null;
                if (player == null) throw new InvalidOperationException("WH02 missing authored player.");
                var flight = player.GetComponent<FlightController>();
                if (flight == null) throw new InvalidOperationException("WH02 missing flight controller.");
                Vector3 forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
                if (forward.sqrMagnitude < .5f) throw new InvalidOperationException("WH02 invalid runway heading.");
                var rotation = Quaternion.LookRotation(forward, Vector3.up);
                var right = Vector3.Cross(Vector3.up, forward);
                // Keep the accepted WH01 comparison untouched. Test candidate pads
                // at least 30 units away, and avoid the CA01 pickup pad.
                var warehouse = roots.SingleOrDefault(r => r.name == "M02 / BD01 warehouse visual trial");
                if (warehouse == null) throw new InvalidOperationException("Install BD01 warehouse preview first.");
                Vector3 anchor = warehouse.transform.position;
                var ca = roots.SingleOrDefault(r => r.name == "M02 / CA01 flight preview");
                bool found = false; Vector3 point = default;
                foreach (var offset in new[] { right*30f, forward*34f, -forward*34f, right*45f, -right*30f, forward*48f })
                {
                    point = anchor + offset;
                    if (ca != null && Vector3.ProjectOnPlane(point-ca.transform.position,Vector3.up).sqrMagnitude < 45f*45f) continue;
                    if (TryPad(terrain, trial.OperationBounds, point, rotation, out float y))
                    { point.y = y; found = true; break; }
                }
                if (!found) throw new InvalidOperationException("WH02 could not find a flat airport-side test pad. No scene changes.");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
                if (prefab == null) prefab = BuildPrefab();
                Undo.IncrementCurrentGroup();
                int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("WH02 visual trial");
                try
                {
                    var root = new GameObject(Preview);
                    Undo.RegisterCreatedObjectUndo(root, "WH02 preview root");
                    root.transform.SetPositionAndRotation(point, rotation);
                    var instance = PrefabUtility.InstantiatePrefab(prefab, root.transform) as GameObject;
                    if (instance == null) throw new InvalidOperationException("WH02 instantiation failed.");
                    Undo.RegisterCreatedObjectUndo(instance, "WH02 preview instance");
                    instance.transform.localPosition = Vector3.zero;
                    instance.transform.localRotation = Quaternion.identity;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("WH02 scene save failed.");
                    Undo.CollapseUndoOperations(undo);
                    Selection.activeGameObject = root;
                    Debug.Log("WH02 visual preview installed. Select root and press F in Scene view. No combat/depot changes.", root);
                }
                catch { Undo.RevertAllDownToGroup(undo); throw; }
            }
            catch (Exception ex) { Debug.LogError("WH02: " + ex); }
        }

        private static bool TryPad(Terrain terrain, Rect operation, Vector3 p, Quaternion rotation, out float y)
        {
            y = float.NegativeInfinity;
            float low = float.PositiveInfinity;
            var origin = terrain.transform.position; var size = terrain.terrainData.size;
            for (int x = -1; x <= 1; x++)
            for (int z = -1; z <= 1; z++)
            {
                Vector3 sample = p + rotation * new Vector3(8f*x, 0f, 11f*z);
                if (!operation.Contains(new Vector2(sample.x, sample.z)) ||
                    sample.x < origin.x || sample.x > origin.x + size.x ||
                    sample.z < origin.z || sample.z > origin.z + size.z) return false;
                float h = terrain.SampleHeight(sample) + origin.y;
                low = Mathf.Min(low, h); y = Mathf.Max(y, h);
            }
            if (y - low > .5f) return false;
            // The preview is visual-only, but avoid overlapping existing collidable scene art.
            Physics.SyncTransforms();
            foreach (var collider in Physics.OverlapBox(new Vector3(p.x, y+3.15f, p.z),
                new Vector3(7.5f, 3f, 10.5f), rotation, ~0, QueryTriggerInteraction.Ignore))
                if (collider != null && !(collider is TerrainCollider)) return false;
            return true;
        }

        [MenuItem("Air Combat/Missions/M02 - Remove WH02 Equipment Preview")]
        private static void Remove()
        {
            if (!TryScene(out var scene)) return;
            var root = scene.GetRootGameObjects().SingleOrDefault(r => r.name == Preview);
            if (root == null) return;
            Undo.DestroyObjectImmediate(root);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) Debug.LogError("WH02 preview removal: scene save failed.");
        }

        private static bool TryScene(out Scene scene)
        {
            scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode) return false;
            if (scene.path == Mission && !scene.isDirty) return true;
            Debug.LogError("WH02: open and save the existing M02 scene first."); return false;
        }

        private static GeometryData ReadGeometry()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(Geometry);
            var data = asset != null ? JsonUtility.FromJson<GeometryData>(asset.text) : null;
            if (data == null || data.palette == null || data.assemblies == null || data.assemblies.Length != 8)
                throw new InvalidOperationException("WH02 geometry missing or invalid.");
            var names = new HashSet<string>(data.palette.Select(p => p.name));
            if (names.Count != data.palette.Length || data.palette.Any(p => p.rgb == null || p.rgb.Length != 3 || p.rgb.Any(c => c < 0 || c > 255)))
                throw new InvalidOperationException("WH02 palette invalid.");
            var expected = new HashSet<string> { "Foundation", "LeftWall", "RightWall", "RearWall", "FrontWall", "Roof01", "Roof02", "Roof03" };
            if (!expected.SetEquals(data.assemblies.Select(p => p.name))) throw new InvalidOperationException("WH02 assembly contract mismatch.");
            foreach (var part in data.assemblies)
                if (part.vertices == null || part.slots == null || part.slots.Length == 0 ||
                    part.slots.Any(s => !names.Contains(s.material) || s.triangles == null || s.triangles.Length % 3 != 0 ||
                        s.triangles.Any(i => i < 0 || i >= part.vertices.Length)))
                    throw new InvalidOperationException("WH02 invalid mesh: " + part.name);
            var points = data.assemblies.SelectMany(p => p.vertices.Select(v => v + p.pivot)).ToArray();
            if (points.Length == 0 || Mathf.Abs(points.Min(p=>p.x)+7f)>.001f || Mathf.Abs(points.Max(p=>p.x)-7f)>.001f ||
                Mathf.Abs(points.Min(p=>p.y))>.001f || Mathf.Abs(points.Max(p=>p.y)-6f)>.001f ||
                Mathf.Abs(points.Min(p=>p.z)+10f)>.001f || Mathf.Abs(points.Max(p=>p.z)-10f)>.001f)
                throw new InvalidOperationException("WH02 source bounds differ from 14 x 6 x 20.");
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
                throw new InvalidOperationException("WH02 asset path occupied or Dominion art folder missing. No overwrite.");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("WH02 requires URP/Lit.");
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(ArtParent, "WH02_EquipmentShelter")))
                throw new InvalidOperationException("WH02 could not create asset folder.");
            GameObject root = null;
            try
            {
                var materials = new Dictionary<string,Material>();
                foreach (var p in data.palette)
                {
                    var mat = new Material(shader) { name = "WH02_"+p.name };
                    mat.SetColor("_BaseColor", new Color32((byte)p.rgb[0],(byte)p.rgb[1],(byte)p.rgb[2],255));
                    mat.SetFloat("_Smoothness", .1f); mat.SetFloat("_Metallic", 0f);
                    AssetDatabase.CreateAsset(mat, Art+"/"+mat.name+".mat"); materials.Add(p.name,mat);
                }
                root = new GameObject("WH02_EquipmentShelter");
                var visual = new GameObject("Visual"); visual.transform.SetParent(root.transform,false);
                foreach (var part in data.assemblies)
                {
                    var mesh = new Mesh { name = "WH02_"+part.name };
                    FillMesh(mesh, part);
                    AssetDatabase.CreateAsset(mesh,Art+"/"+mesh.name+".asset");
                    var go = new GameObject(part.name,typeof(MeshFilter),typeof(MeshRenderer));
                    go.transform.SetParent(visual.transform,false); go.transform.localPosition=part.pivot;
                    go.GetComponent<MeshFilter>().sharedMesh=mesh;
                    go.GetComponent<MeshRenderer>().sharedMaterials=part.slots.Select(s=>materials[s.material]).ToArray();
                }
                var prefab = PrefabUtility.SaveAsPrefabAsset(root,Prefab,out bool success);
                if (!success || prefab == null) throw new InvalidOperationException("WH02 prefab save failed.");
                AssetDatabase.SaveAssets(); return prefab;
            }
            catch { AssetDatabase.DeleteAsset(Prefab); AssetDatabase.DeleteAsset(Art); throw; }
            finally { if (root != null) Object.DestroyImmediate(root); }
        }
    }
}
