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
                if (existing != null) { Selection.activeGameObject = existing; SceneView.lastActiveSceneView?.FrameSelected(); return; }
                if (AssetDatabase.LoadMainAssetAtPath(Prefab) != null || AssetDatabase.IsValidFolder(Art))
                    throw new InvalidOperationException("IN01 art exists without preview. Refusing to overwrite it.");
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
                        var vertices = part.triangles.Select(i => part.vertices[i]).ToArray();
                        var mesh = new Mesh { name = "IN01_" + part.name.Replace(' ', '_'), vertices = vertices };
                        mesh.triangles = Enumerable.Range(0, vertices.Length).ToArray();
                        mesh.RecalculateNormals(); mesh.RecalculateBounds();
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
    }
}
