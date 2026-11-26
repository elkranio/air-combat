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
    // Visual-only heavy transport. Source geometry is shared with the editable Blender model.
    internal static class CA01CarryallAuthoring
    {
        private const string Geometry = "Assets/_Project/Editor/Mission02/CA01Geometry.json";
        private const string ArtParent = "Assets/_Project/Art/Aircraft/Dominion";
        private const string Art = ArtParent + "/CA01_Carryall";
        private const string Prefab = "Assets/_Project/Prefabs/Aircraft/CA01_Carryall.prefab";
        private const string Mission = "Assets/_Project/Scenes/Mission02_ConvoyInterdiction.unity";
        private const string Preview = "M02 / CA01 comparison preview";
        [Serializable] private sealed class Paint { public string name; public int[] rgb; }
        [Serializable] private sealed class Slot { public string material; public int[] triangles; }
        [Serializable] private sealed class Part { public string name; public Vector3 pivot; public Vector3[] vertices; public Slot[] slots; }
        [Serializable] private sealed class GeometryData { public Paint[] palette; public Part[] assemblies; public Vector3[] sockets; }

        [MenuItem("Air Combat/Missions/M02 - Build CA01 Carryall Preview")]
        private static void BuildAndPlace()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != Mission || scene.isDirty)
            { Debug.LogError("CA01: open and save the existing M02 scene first."); return; }
            try
            {
                var roots = scene.GetRootGameObjects();
                var trial = roots.SelectMany(r => r.GetComponentsInChildren<Mission02RouteTrial>(true)).Single();
                if (!trial.HasSecondPoint) throw new InvalidOperationException("CA01 requires the two-target M02 layout.");
                if (roots.Any(r => r.name == Preview)) throw new InvalidOperationException("CA01 preview already exists; no changes made.");
                var cargo = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Environment/CG01_SpecialCargo.prefab");
                if (cargo == null) throw new InvalidOperationException("Install CG01 first.");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
                if (prefab == null) prefab = BuildPrefab();
                ValidateInterface(prefab, cargo);
                var terrain = roots.SelectMany(r => r.GetComponentsInChildren<Terrain>(true)).Single();
                if (terrain.terrainData == null) throw new InvalidOperationException("CA01 requires terrain data.");
                Undo.IncrementCurrentGroup();
                int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("CA01 visual comparison");
                try
                {
                    var root = new GameObject(Preview);
                    Undo.RegisterCreatedObjectUndo(root, "CA01 preview root");
                    // Compare in the existing C1 clearing; do not modify either mission cargo.
                    root.transform.position = trial.PointC;
                    Place(prefab, cargo, root.transform, terrain, trial.PointC + new Vector3(-28f,0f,0f), false);
                    Place(prefab, cargo, root.transform, terrain, trial.PointC + new Vector3(28f,0f,0f), true);
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("CA01 scene save failed.");
                    Undo.CollapseUndoOperations(undo);
                    Selection.activeGameObject = root;
                    Debug.Log("CA01: empty/loaded comparison near C1. Select root and frame (F). Remove comparison before gameplay integration.", root);
                }
                catch { Undo.RevertAllDownToGroup(undo); throw; }
            }
            catch (Exception ex) { Debug.LogError("CA01: " + ex); }
        }

        [MenuItem("Air Combat/Missions/M02 - Remove CA01 Comparison Preview")]
        private static void RemoveComparison()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetActiveScene();
            if (scene.path != Mission || scene.isDirty)
            { Debug.LogError("CA01: open and save M02 first."); return; }
            var root = scene.GetRootGameObjects().SingleOrDefault(r => r.name == Preview);
            if (root == null) return;
            Undo.DestroyObjectImmediate(root);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) Debug.LogError("CA01: scene save failed.");
        }

        [MenuItem("Air Combat/Missions/M02 - Apply CA01 Carryall V7")]
        private static void UpgradeVisual()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetActiveScene();
            if (scene.path != Mission || scene.isDirty)
            { Debug.LogError("CA01 V7: open and save M02 first."); return; }
            GameObject contents = null;
            var generated = new List<Mesh>();
            var backups = new List<Mesh>();
            var originals = new List<Mesh>();
            bool editing = false;
            try
            {
                var cargo = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Environment/CG01_SpecialCargo.prefab");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
                if (prefab == null) throw new InvalidOperationException("Build CA01 preview first.");
                ValidateInterface(prefab, cargo);
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(Geometry);
                var data = asset != null ? JsonUtility.FromJson<GeometryData>(asset.text) : null;
                if (data == null || data.assemblies == null || data.assemblies.Length != 12 || data.sockets == null || data.sockets.Length != 4 ||
                    data.assemblies.Select(p=>p.name).Distinct().Count() != 12)
                    throw new InvalidOperationException("Invalid CA01 V7 source.");
                contents = PrefabUtility.LoadPrefabContents(Prefab);
                ValidateInterface(contents, cargo);
                var visual = contents.transform.Find("Visual");
                if (visual == null || visual.childCount != 12) throw new InvalidOperationException("Unexpected CA01 visual hierarchy.");
                for (int i=0;i<4;i++)
                    if ((contents.transform.Find($"ClampSockets/Clamp_{i+1:00}").localPosition-data.sockets[i]).sqrMagnitude > .000001f)
                        throw new InvalidOperationException("V7 source capture socket mismatch.");
                var mounts = new List<Transform>(); var renderers = new List<MeshRenderer>();
                var materials = new List<Material[]>();
                // Preflight every source, target asset and material before changing anything.
                foreach (var part in data.assemblies)
                {
                    var tr = visual.Find(part.name);
                    var filter = tr != null ? tr.GetComponent<MeshFilter>() : null;
                    var renderer = tr != null ? tr.GetComponent<MeshRenderer>() : null;
                    var original = AssetDatabase.LoadAssetAtPath<Mesh>(Art+"/CA01_"+part.name+".asset");
                    if (filter == null || renderer == null || original == null || filter.sharedMesh != original ||
                        part.vertices == null || part.slots == null || part.slots.Length == 0 ||
                        part.slots.Any(s=>s.triangles == null || s.triangles.Length % 3 != 0 || s.triangles.Any(i=>i<0 || i>=part.vertices.Length)))
                        throw new InvalidOperationException("Invalid CA01 V7 assembly: "+part.name);
                    var mats = part.slots.Select(s=>AssetDatabase.LoadAssetAtPath<Material>(Art+"/CA01_"+s.material+".mat")).ToArray();
                    if (mats.Any(m=>m==null)) throw new InvalidOperationException("Missing material: "+part.name);
                    var vertices = new List<Vector3>(); var triangles = new List<int[]>();
                    foreach (var slot in part.slots)
                    {
                        int start=vertices.Count;
                        foreach (int index in slot.triangles) vertices.Add(part.vertices[index]);
                        triangles.Add(Enumerable.Range(start,slot.triangles.Length).ToArray());
                    }
                    var mesh = new Mesh { name = original.name, vertices = vertices.ToArray(), subMeshCount = triangles.Count };
                    for (int i=0;i<triangles.Count;i++) mesh.SetTriangles(triangles[i],i);
                    mesh.RecalculateNormals(); mesh.RecalculateBounds();
                    var backup = Object.Instantiate(original); backup.name = original.name;
                    generated.Add(mesh); originals.Add(original); backups.Add(backup);
                    mounts.Add(tr); renderers.Add(renderer); materials.Add(mats);
                }
                var engineSockets = new List<Transform>(); var enginePositions = new List<Vector3>();
                foreach (int x in new[] {-1,1}) foreach (int z in new[] {-1,1})
                {
                    var socket = contents.transform.Find($"EngineSockets/Exhaust_{(x < 0 ? "L" : "R")}_{(z < 0 ? "Rear" : "Front")}");
                    if (socket == null) throw new InvalidOperationException("Missing exhaust socket.");
                    engineSockets.Add(socket); enginePositions.Add(new Vector3(x*10.2f,.75f,z*5.4f));
                }
                editing = true;
                for (int i=0;i<generated.Count;i++)
                {
                    // Keep existing mesh asset GUIDs and the prefab hierarchy, so the scene instances update.
                    EditorUtility.CopySerialized(generated[i],originals[i]); EditorUtility.SetDirty(originals[i]);
                    if (originals[i].vertexCount != generated[i].vertexCount ||
                        (originals[i].bounds.size - generated[i].bounds.size).sqrMagnitude > .000001f)
                        throw new InvalidOperationException("V7 mesh update did not persist: " + originals[i].name);
                    mounts[i].localPosition=data.assemblies[i].pivot;
                    renderers[i].sharedMaterials=materials[i];
                }
                for (int i=0;i<engineSockets.Count;i++) engineSockets[i].localPosition=enginePositions[i];
                ValidateInterface(contents,cargo);
                PrefabUtility.SaveAsPrefabAsset(contents,Prefab,out bool success);
                if (!success) throw new InvalidOperationException("CA01 V7 prefab save failed.");
                AssetDatabase.SaveAssets();
                // Release the isolated prefab scene before refreshing the serialized meshes.
                // V3 was correct on disk but the Scene view kept old GPU geometry until restart.
                PrefabUtility.UnloadPrefabContents(contents);
                contents = null;
                foreach (var part in data.assemblies)
                    AssetDatabase.ImportAsset(Art+"/CA01_"+part.name+".asset", ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset(Prefab, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                var updated = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
                ValidateInterface(updated, cargo);
                foreach (var part in data.assemblies)
                {
                    var tr = updated.transform.Find("Visual/" + part.name);
                    var mesh = tr != null ? tr.GetComponent<MeshFilter>().sharedMesh : null;
                    if (mesh == null || mesh.vertexCount != part.slots.Sum(s => s.triangles.Length) ||
                        (tr.localPosition - part.pivot).sqrMagnitude > .000001f)
                        throw new InvalidOperationException("V7 saved prefab verification failed: " + part.name);
                }
                SceneView.RepaintAll();
                Debug.Log("CA01 V7 applied in place. Verified 12 saved assemblies and CG01 interface. Existing comparisons stay linked. Restart Unity before evaluating the silhouette if the Scene view retains stale geometry.");
            }
            catch (Exception ex)
            {
                if (editing)
                {
                    for (int i=0;i<backups.Count;i++) { EditorUtility.CopySerialized(backups[i], originals[i]); EditorUtility.SetDirty(originals[i]); }
                    AssetDatabase.SaveAssets();
                    Debug.LogError("CA01 V7 upgrade interrupted. Check prefab and mesh changes in git before continuing.");
                }
                Debug.LogError("CA01 V7: "+ex);
            }
            finally
            {
                if (contents != null) PrefabUtility.UnloadPrefabContents(contents);
                foreach (var mesh in generated.Concat(backups)) Object.DestroyImmediate(mesh);
            }
        }

        private static void ValidateInterface(GameObject lifter, GameObject cargo)
        {
            if (lifter == null || cargo == null) throw new InvalidOperationException("Missing comparison prefab.");
            var anchor = lifter.transform.Find("CargoMount");
            if (anchor == null || (anchor.localPosition - new Vector3(0,-5,0)).sqrMagnitude > .000001f)
                throw new InvalidOperationException("CA01 cargo mount differs from CG01 dimensions.");
            for (int i=1;i<=4;i++)
            {
                var clamp = lifter.transform.Find($"ClampSockets/Clamp_{i:00}");
                var grab = cargo.transform.Find($"CaptureSockets/Grab_{i:00}");
                if (clamp == null || grab == null || (clamp.localPosition - (grab.localPosition + anchor.localPosition)).sqrMagnitude > .000001f)
                    throw new InvalidOperationException("CA01 / CG01 capture socket mismatch: " + i);
            }
        }

        private static void Place(GameObject prefab, GameObject cargo, Transform parent, Terrain terrain, Vector3 point, bool loaded)
        {
            var stand = new GameObject(loaded ? "Loaded comparison" : "Empty comparison");
            Undo.RegisterCreatedObjectUndo(stand, "CA01 comparison stand");
            stand.transform.SetParent(parent,false);
            point.y = terrain.SampleHeight(point) + terrain.transform.position.y + 8f;
            stand.transform.SetPositionAndRotation(point, Quaternion.Euler(0f,24f,0f));
            var go = PrefabUtility.InstantiatePrefab(prefab, stand.transform) as GameObject;
            if (go == null) throw new InvalidOperationException("CA01 instantiation failed.");
            Undo.RegisterCreatedObjectUndo(go, "CA01 preview instance");
            go.name = loaded ? "CA01 / Loaded" : "CA01 / Empty";
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
            if (!loaded) return;
            // Separate sibling copy rather than nesting scene overrides inside the transport prefab.
            // Align with the transport mount using the identical position and rotation.
            var payload = PrefabUtility.InstantiatePrefab(cargo, stand.transform) as GameObject;
            if (payload == null) throw new InvalidOperationException("CA01 comparison cargo instantiation failed.");
            Undo.RegisterCreatedObjectUndo(payload, "CA01 loaded payload");
            payload.name = "CG01 / Comparison Payload";
            var mount = go.transform.Find("CargoMount");
            payload.transform.SetPositionAndRotation(mount.position, mount.rotation);
            PrefabUtility.RecordPrefabInstancePropertyModifications(payload.transform);
        }

        private static GameObject BuildPrefab()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(Geometry);
            var data = asset != null ? JsonUtility.FromJson<GeometryData>(asset.text) : null;
            if (data == null || data.palette == null || data.assemblies == null || data.assemblies.Length != 12 ||
                data.sockets == null || data.sockets.Length != 4)
                throw new InvalidOperationException("CA01 geometry missing or invalid.");
            if (!AssetDatabase.IsValidFolder(ArtParent) || AssetDatabase.IsValidFolder(Art) || AssetDatabase.LoadMainAssetAtPath(Prefab) != null)
                throw new InvalidOperationException("CA01 asset path occupied or Dominion art folder missing. No overwrite.");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("CA01 requires URP/Lit.");
            // Validate the whole source before creating assets.
            var names = new HashSet<string>(data.palette.Select(p => p.name));
            if (names.Count != data.palette.Length || data.palette.Any(p => p.rgb == null || p.rgb.Length != 3 || p.rgb.Any(c => c < 0 || c > 255)))
                throw new InvalidOperationException("CA01 palette invalid.");
            if (data.assemblies.Select(p => p.name).Distinct().Count() != data.assemblies.Length)
                throw new InvalidOperationException("CA01 duplicate assembly.");
            foreach (var part in data.assemblies)
                if (part.vertices == null || part.slots == null || part.slots.Length == 0 ||
                    part.slots.Any(s => !names.Contains(s.material) || s.triangles == null || s.triangles.Length % 3 != 0 ||
                        s.triangles.Any(i => i < 0 || i >= part.vertices.Length)))
                    throw new InvalidOperationException("CA01 invalid mesh: " + part.name);
            var points = data.assemblies.SelectMany(p => p.vertices.Select(v => v + p.pivot)).ToArray();
            if (points.Length == 0 || Mathf.Abs(points.Min(p=>p.x)+13.65f)>.001f || Mathf.Abs(points.Max(p=>p.x)-13.65f)>.001f ||
                Mathf.Abs(points.Min(p=>p.y))>.001f || Mathf.Abs(points.Max(p=>p.y)-6.075f)>.001f ||
                Mathf.Abs(points.Min(p=>p.z)+10.5f)>.001f || Mathf.Abs(points.Max(p=>p.z)-12.72f)>.001f)
                throw new InvalidOperationException("CA01 source bounds differ from 27.3 x 6.075 x 23.22.");
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(ArtParent, "CA01_Carryall")))
                throw new InvalidOperationException("CA01 could not create asset folder.");
            GameObject root = null;
            try
            {
                var materials = new Dictionary<string,Material>();
                foreach (var p in data.palette)
                {
                    var mat = new Material(shader) { name = "CA01_"+p.name };
                    mat.SetColor("_BaseColor", new Color32((byte)p.rgb[0],(byte)p.rgb[1],(byte)p.rgb[2],255));
                    mat.SetFloat("_Smoothness", .12f); mat.SetFloat("_Metallic", 0f);
                    AssetDatabase.CreateAsset(mat, Art+"/"+mat.name+".mat"); materials.Add(p.name,mat);
                }
                root = new GameObject("CA01_Carryall");
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
                    var mesh = new Mesh { name = "CA01_"+part.name, vertices = vertices.ToArray(), subMeshCount = triangles.Count };
                    for (int i=0;i<triangles.Count;i++) mesh.SetTriangles(triangles[i],i);
                    mesh.RecalculateNormals(); mesh.RecalculateBounds();
                    AssetDatabase.CreateAsset(mesh,Art+"/"+mesh.name+".asset");
                    var go = new GameObject(part.name,typeof(MeshFilter),typeof(MeshRenderer));
                    go.transform.SetParent(visual.transform,false); go.transform.localPosition=part.pivot;
                    go.GetComponent<MeshFilter>().sharedMesh=mesh;
                    go.GetComponent<MeshRenderer>().sharedMaterials=part.slots.Select(s=>materials[s.material]).ToArray();
                }
                var capture = new GameObject("ClampSockets"); capture.transform.SetParent(root.transform,false);
                for (int i=0;i<data.sockets.Length;i++)
                {
                    var socket = new GameObject($"Clamp_{i+1:00}"); socket.transform.SetParent(capture.transform,false);
                    socket.transform.localPosition=data.sockets[i];
                }
                var mount = new GameObject("CargoMount"); mount.transform.SetParent(root.transform,false);
                mount.transform.localPosition = new Vector3(0,-5,0);
                var engines = new GameObject("EngineSockets"); engines.transform.SetParent(root.transform,false);
                foreach (int x in new[] {-1,1}) foreach (int z in new[] {-1,1})
                {
                    var socket = new GameObject($"Exhaust_{(x < 0 ? "L" : "R")}_{(z < 0 ? "Rear" : "Front")}");
                    socket.transform.SetParent(engines.transform,false);
                    socket.transform.localPosition = new Vector3(x*10.2f,.75f,z*5.4f);
                    socket.transform.localRotation = Quaternion.Euler(90f,0f,0f);
                }
                var prefab = PrefabUtility.SaveAsPrefabAsset(root,Prefab,out bool success);
                if (!success || prefab == null) throw new InvalidOperationException("CA01 prefab save failed.");
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
