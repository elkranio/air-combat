using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AirCombat
{
    // Visual trial only. Geometry is shared with the editable Blender source.
    internal static class IN01InterceptorAuthoring
    {
        private const string ScenePath = "Assets/_Project/Scenes/CombatSandbox.unity";
        private const string Geometry = "Assets/_Project/Editor/IN01Geometry.json";
        private const string Parent = "Assets/_Project/Art/Aircraft/Dominion";
        private const string Art = Parent + "/IN01_Interceptor";
        private const string Prefab = "Assets/_Project/Prefabs/Aircraft/IN01_Interceptor.prefab";
        private const string Preview = "IN01 / Interceptor - VISUAL TRIAL";
        private const string LiveryRoot = "IN01 / Livery comparison (temporary)";
        private const string LiveryArt = Art + "/LiveryTrials";
        [Serializable] private sealed class Paint { public string name; public int[] rgb; }
        [Serializable] private sealed class Part { public string name, material; public Vector3[] vertices; public int[] triangles; }
        [Serializable] private sealed class Data { public Paint[] palette; public Part[] parts; }

        [MenuItem("Air Combat/Sandbox/IN01 - Build Interceptor Preview")]
        private static void BuildAndPlace()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            try
            {
                if (scene.isDirty) throw new InvalidOperationException("Save CombatSandbox first.");
                var roots = scene.GetRootGameObjects();
                var lynx = roots.SingleOrDefault(r => r.name == "PF02_Lynx");
                if (lynx == null) throw new InvalidOperationException("Existing PF02_Lynx comparison not found.");
                var existing = roots.SingleOrDefault(r => r.name == Preview);
                var text = AssetDatabase.LoadAssetAtPath<TextAsset>(Geometry);
                var data = text != null ? JsonUtility.FromJson<Data>(text.text) : null;
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (data == null || data.palette == null || data.parts == null || data.parts.Length == 0 || shader == null ||
                    !AssetDatabase.IsValidFolder(Parent) || data.parts.Select(p => p.name).Distinct().Count() != data.parts.Length ||
                    data.palette.Select(p => p.name).Distinct().Count() != data.palette.Length ||
                    data.palette.Any(p => p.rgb == null || p.rgb.Length != 3 || p.rgb.Any(c => c < 0 || c > 255)) ||
                    data.parts.Any(p => p.vertices == null || p.triangles == null || p.triangles.Length % 3 != 0 ||
                        p.triangles.Any(i => i < 0 || i >= p.vertices.Length) || !data.palette.Any(m => m.name == p.material)))
                    throw new InvalidOperationException("Invalid IN01 geometry, palette or URP shader.");
                if (existing != null)
                {
                    var comparison = roots.SingleOrDefault(r => r.name == LiveryRoot);
                    if (comparison == null)
                    {
                        RefreshExisting(existing, data);
                        comparison = BuildLiveryComparison(existing, data, scene);
                    }
                    Selection.objects = new Object[] { existing, comparison };
                    SceneView.lastActiveSceneView?.FrameSelected();
                    return;
                }
                if (AssetDatabase.LoadMainAssetAtPath(Prefab) != null || AssetDatabase.IsValidFolder(Art))
                    throw new InvalidOperationException("IN01 art exists without preview. Refusing to overwrite it.");
                GameObject model = null;
                bool folderCreated = false, prefabCreated = false;
                Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("IN01 interceptor comparison");
                try
                {
                    AssetDatabase.CreateFolder(Parent, "IN01_Interceptor"); folderCreated = true;
                    var mats = data.palette.ToDictionary(p => p.name, p =>
                    {
                        var mat = new Material(shader) { name = "IN01_" + p.name };
                        mat.SetColor("_BaseColor", new Color32((byte)p.rgb[0], (byte)p.rgb[1], (byte)p.rgb[2], 255));
                        mat.SetFloat("_Metallic", 0f); mat.SetFloat("_Smoothness", p.name == "Glass" ? .32f : .10f);
                        AssetDatabase.CreateAsset(mat, Art + "/" + mat.name + ".mat");
                        return mat;
                    });
                    model = new GameObject("IN01_Interceptor");
                    var visual = new GameObject("Visual").transform; visual.SetParent(model.transform, false);
                    foreach (var part in data.parts)
                    {
                        // Flat facets, same outward triangle winding as other authored meshes.
                        var mesh = BuildMesh(part);
                        AssetDatabase.CreateAsset(mesh, Art + "/" + mesh.name + ".asset");
                        var go = new GameObject(part.name, typeof(MeshFilter), typeof(MeshRenderer));
                        go.transform.SetParent(visual, false);
                        go.GetComponent<MeshFilter>().sharedMesh = mesh;
                        go.GetComponent<MeshRenderer>().sharedMaterial = mats[part.material];
                    }
                    var prefab = PrefabUtility.SaveAsPrefabAsset(model, Prefab);
                    prefabCreated = AssetDatabase.LoadMainAssetAtPath(Prefab) != null;
                    if (prefab == null) throw new InvalidOperationException("IN01 prefab save failed.");
                    Object.DestroyImmediate(model); model = null;
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    Undo.RegisterCreatedObjectUndo(instance, "IN01 preview");
                    instance.name = Preview;
                    // Existing Lynx is at (-691.5, 0, -279.8); leave Tiger and parked Kestrels untouched.
                    // Align nose direction with Lynx and use the same origin height for a fair scale comparison.
                    instance.transform.SetPositionAndRotation(lynx.transform.position + new Vector3(-18f, 0f, 0f), lynx.transform.rotation);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("IN01 scene save failed.");
                    AssetDatabase.SaveAssets(); Undo.CollapseUndoOperations(undo);
                    Selection.activeGameObject = instance; SceneView.lastActiveSceneView?.FrameSelected();
                    Debug.Log("IN01 visual-only preview beside Lynx. No combat components, colliders or flight behavior.", instance);
                }
                catch
                {
                    if (model != null) Object.DestroyImmediate(model);
                    Undo.RevertAllDownToGroup(undo);
                    if (prefabCreated) AssetDatabase.DeleteAsset(Prefab);
                    if (folderCreated) AssetDatabase.DeleteAsset(Art);
                    throw;
                }
            }
            catch (Exception ex) { Debug.LogError("IN01: " + ex); }
        }

        // Same accepted V2 mesh assets, three scene-only prefab instances and
        // separate trial materials. Base palette and prefab stay untouched.
        private static GameObject BuildLiveryComparison(GameObject baseline, Data data, Scene scene)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            if (prefab == null || AssetDatabase.IsValidFolder(LiveryArt))
                throw new InvalidOperationException("IN01 livery assets already exist without their comparison root, or prefab missing.");
            string[] keys = { "Hull", "Wing", "Dark", "Steel", "Glass", "Mark" };
            if (data.palette.Length != keys.Length || keys.Any(k => !data.palette.Any(p => p.name == k)))
                throw new InvalidOperationException("IN01 livery comparison requires the V2 material slots.");
            string[] names = { "B - Titanium", "C - Graphite", "D - Dust Olive" };
            Color32[][] colors = {
                new Color32[] { new Color32(166,175,179,255), new Color32(89,103,115,255), new Color32(39,48,58,255), new Color32(141,151,159,255), new Color32(42,105,130,255), new Color32(192,57,44,255) },
                new Color32[] { new Color32(49,55,62,255), new Color32(61,68,75,255), new Color32(25,31,38,255), new Color32(119,129,139,255), new Color32(42,106,133,255), new Color32(202,65,48,255) },
                new Color32[] { new Color32(129,132,106,255), new Color32(101,111,91,255), new Color32(51,57,48,255), new Color32(145,148,127,255), new Color32(39,100,113,255), new Color32(192,67,46,255) }
            };
            Vector3[] offsets = { new Vector3(-18f,0f,0f), new Vector3(0f,0f,-23f), new Vector3(-18f,0f,-23f) };
            var originals = keys.ToDictionary(k => k,
                k => AssetDatabase.LoadAssetAtPath<Material>(Art + "/IN01_" + k + ".mat"));
            if (originals.Values.Any(m => m == null || !m.HasProperty("_BaseColor")))
                throw new InvalidOperationException("IN01 livery base materials missing.");
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("IN01 livery comparison");
            bool folderCreated = false;
            try
            {
                AssetDatabase.CreateFolder(Art, "LiveryTrials");
                folderCreated = AssetDatabase.IsValidFolder(LiveryArt);
                if (!folderCreated) throw new InvalidOperationException("IN01 livery folder creation failed.");
                var root = new GameObject(LiveryRoot);
                Undo.RegisterCreatedObjectUndo(root, "IN01 livery root");
                for (int v = 0; v < names.Length; v++)
                {
                    var mats = new System.Collections.Generic.Dictionary<string, Material>();
                    for (int k = 0; k < keys.Length; k++)
                    {
                        var mat = new Material(originals[keys[k]]) { name = "IN01_Trial_" + (char)('B' + v) + "_" + keys[k] };
                        mat.SetColor("_BaseColor", colors[v][k]);
                        AssetDatabase.CreateAsset(mat, LiveryArt + "/" + mat.name + ".mat");
                        mats.Add(keys[k], mat);
                    }
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    Undo.RegisterCreatedObjectUndo(instance, "IN01 livery instance");
                    instance.name = "IN01 / " + names[v];
                    instance.transform.SetParent(root.transform, false);
                    instance.transform.SetPositionAndRotation(baseline.transform.position + offsets[v], baseline.transform.rotation);
                    instance.transform.localScale = baseline.transform.localScale;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                    var visual = instance.transform.Find("Visual");
                    foreach (var part in data.parts)
                    {
                        var renderer = visual?.Find(part.name)?.GetComponent<MeshRenderer>();
                        if (renderer == null) throw new InvalidOperationException("IN01 missing trial part: " + part.name);
                        renderer.sharedMaterial = mats[part.material];
                        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    }
                }
                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("IN01 livery scene save failed.");
                Undo.CollapseUndoOperations(undo);
                Debug.Log("IN01 V2 palette comparison: A existing; B Titanium west; C Graphite south; D Dust Olive southwest. Same meshes and pose.", root);
                return root;
            }
            catch
            {
                Undo.RevertAllDownToGroup(undo);
                if (folderCreated) AssetDatabase.DeleteAsset(LiveryArt);
                throw;
            }
        }

        private static Mesh BuildMesh(Part part)
        {
            var vertices = part.triangles.Select(i => part.vertices[i]).ToArray();
            var mesh = new Mesh { name = "IN01_" + part.name.Replace(' ', '_'), vertices = vertices };
            mesh.triangles = Enumerable.Range(0, vertices.Length).ToArray();
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        // Visual revisions update the existing assets, preserving prefab and mesh GUIDs.
        private static void RefreshExisting(GameObject instance, Data data)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            if (prefab == null || PrefabUtility.GetCorrespondingObjectFromSource(instance) != prefab)
                throw new InvalidOperationException("IN01 preview is not linked to the expected prefab.");
            var visual = prefab.transform.Find("Visual");
            if (visual == null || visual.childCount != data.parts.Length)
                throw new InvalidOperationException("IN01 visual hierarchy changed; cannot refresh safely.");
            var mats = data.palette.ToDictionary(p => p.name,
                p => AssetDatabase.LoadAssetAtPath<Material>(Art + "/IN01_" + p.name + ".mat"));
            if (mats.Values.Any(m => m == null || !m.HasProperty("_BaseColor")))
                throw new InvalidOperationException("IN01 material missing.");
            var originals = new Mesh[data.parts.Length];
            var generated = new Mesh[data.parts.Length];
            var backups = new Mesh[data.parts.Length];
            var oldColors = data.palette.Select(p => mats[p.name].GetColor("_BaseColor")).ToArray();
            bool editing = false;
            try
            {
                // Preflight every part before modifying persistent assets.
                for (int i = 0; i < data.parts.Length; i++)
                {
                    var part = data.parts[i];
                    var child = visual.Find(part.name);
                    var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Art + "/IN01_" + part.name.Replace(' ', '_') + ".asset");
                    if (child == null || mesh == null || child.GetComponent<MeshFilter>()?.sharedMesh != mesh ||
                        child.GetComponent<MeshRenderer>()?.sharedMaterial != mats[part.material])
                        throw new InvalidOperationException("IN01 asset/hierarchy mismatch: " + part.name);
                    originals[i] = mesh;
                    generated[i] = BuildMesh(part);
                    backups[i] = Object.Instantiate(mesh); backups[i].name = mesh.name;
                }
                editing = true;
                for (int i = 0; i < originals.Length; i++)
                {
                    EditorUtility.CopySerialized(generated[i], originals[i]);
                    EditorUtility.SetDirty(originals[i]);
                }
                foreach (var paint in data.palette)
                {
                    mats[paint.name].SetColor("_BaseColor", new Color32((byte)paint.rgb[0], (byte)paint.rgb[1], (byte)paint.rgb[2], 255));
                    EditorUtility.SetDirty(mats[paint.name]);
                }
                AssetDatabase.SaveAssets();
                SceneView.RepaintAll();
                Debug.Log("IN01 V2 refreshed in place. Existing prefab, comparison pose and GUIDs preserved.", instance);
            }
            catch
            {
                if (editing)
                {
                    for (int i = 0; i < originals.Length; i++)
                    {
                        EditorUtility.CopySerialized(backups[i], originals[i]);
                        EditorUtility.SetDirty(originals[i]);
                    }
                    for (int i = 0; i < data.palette.Length; i++)
                    {
                        var mat = mats[data.palette[i].name];
                        mat.SetColor("_BaseColor", oldColors[i]); EditorUtility.SetDirty(mat);
                    }
                    AssetDatabase.SaveAssets();
                }
                throw;
            }
            finally
            {
                foreach (var mesh in generated.Concat(backups))
                    if (mesh != null) Object.DestroyImmediate(mesh);
            }
        }
    }
}
