using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AirCombat
{
    // Decorative art trial only. Rebuild assets, never move existing scene objects.
    internal static class PG05PowerLineAuthoring
    {
        private const string Root = "Assets/_Project/";
        private const string ScenePath = Root + "Scenes/Mission03_SparrowInterception.unity";
        private const string Geometry = Root + "Editor/PG05Geometry.json";
        private const string Parent = Root + "Art/Environment/Infrastructure";
        private const string Art = Parent + "/PG05_PowerLine";
        private const string Pylon = Root + "Prefabs/Environment/PG05_Pylon.prefab";
        private const string Span = Root + "Prefabs/Environment/PG05_LineSpan.prefab";
        private const string Preview = "M03 / PG05 Power Line - VISUAL TRIAL";
        private const float SpanLength = 58f;
        [Serializable] private sealed class Paint { public string name; public int[] rgb; }
        [Serializable] private sealed class Part { public string name, group, material; public Vector3[] vertices; public int[] triangles; }
        [Serializable] private sealed class Data { public Paint[] palette; public Part[] parts; }

        [MenuItem("Air Combat/Missions/M03 - Build PG05 Power Line Preview")]
        private static void BuildAndPlace()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) { Debug.LogError("PG05: open Mission03 first."); return; }
            try
            {
                var source = AssetDatabase.LoadAssetAtPath<TextAsset>(Geometry);
                var data = source != null ? JsonUtility.FromJson<Data>(source.text) : null;
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (data == null || data.palette == null || data.parts == null || shader == null ||
                    data.palette.Any(p => p == null || !SafeName(p.name) || p.rgb == null || p.rgb.Length != 3 || p.rgb.Any(c => c < 0 || c > 255)) ||
                    data.palette.Select(p => p.name).Distinct().Count() != data.palette.Length ||
                    data.parts.Any(p => p == null || (p.group != "Pylon" && p.group != "Span") || !SafeName(p.material) ||
                        p.name != p.group + "_" + p.material || p.vertices == null || p.vertices.Length == 0 ||
                        p.triangles == null || p.triangles.Length == 0 || p.triangles.Length % 3 != 0 ||
                        p.triangles.Any(i => i < 0 || i >= p.vertices.Length) || !data.palette.Any(m => m.name == p.material)) ||
                    data.parts.Select(p => p.name).Distinct().Count() != data.parts.Length ||
                    !data.parts.Any(p => p.group == "Pylon") || !data.parts.Any(p => p.group == "Span"))
                    throw new InvalidOperationException("Invalid PG05 geometry, palette or shader.");

                var existing = scene.GetRootGameObjects().Where(r => r.name == Preview).ToArray();
                if (existing.Length > 1) throw new InvalidOperationException("Duplicate PG05 preview roots.");
                if (existing.Length == 1)
                {
                    var p = AssetDatabase.LoadAssetAtPath<GameObject>(Pylon);
                    var s = AssetDatabase.LoadAssetAtPath<GameObject>(Span);
                    var children = existing[0].transform.Cast<Transform>().Select(t =>
                        PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)).ToArray();
                    if (p == null || s == null || children.Length != 5 || children.Count(x => x == p) != 3 || children.Count(x => x == s) != 2)
                        throw new InvalidOperationException("Unexpected PG05 preview contents; scene unchanged.");
                }
                if (!AssetDatabase.IsValidFolder(Parent)) AssetDatabase.CreateFolder(Root + "Art/Environment", "Infrastructure");
                if (!AssetDatabase.IsValidFolder(Art)) AssetDatabase.CreateFolder(Parent, "PG05_PowerLine");
                var mats = data.palette.ToDictionary(p => p.name, p =>
                {
                    string path = Art + "/PG05_" + p.name + ".mat";
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (mat == null) { mat = new Material(shader) { name = "PG05_" + p.name }; AssetDatabase.CreateAsset(mat, path); }
                    mat.SetColor("_BaseColor", new Color32((byte)p.rgb[0], (byte)p.rgb[1], (byte)p.rgb[2], 255));
                    mat.SetFloat("_Metallic", 0f); mat.SetFloat("_Smoothness", .08f);
                    EditorUtility.SetDirty(mat); return mat;
                });
                var pylon = BuildPrefab(data, "Pylon", Pylon, mats);
                var span = BuildPrefab(data, "Span", Span, mats);
                AssetDatabase.SaveAssets();
                var preview = existing.SingleOrDefault();
                if (preview == null)
                {
                    preview = new GameObject(Preview);
                    Undo.RegisterCreatedObjectUndo(preview, "PG05 preview");
                    // Preliminary composition, entirely inside the existing flat site reserve.
                    preview.transform.position = new Vector3(-182f, -.04f, -720f);
                    try
                    {
                        for (int i = 0; i < 3; i++) Place(pylon, scene, preview.transform, "PG05 / Pylon " + (i + 1), i * SpanLength);
                        for (int i = 0; i < 2; i++) Place(span, scene, preview.transform, "PG05 / Span " + (i + 1), i * SpanLength);
                    }
                    catch { Object.DestroyImmediate(preview); throw; }
                    EditorSceneManager.MarkSceneDirty(scene);
                }
                Selection.activeGameObject = preview;
                SceneView.lastActiveSceneView?.FrameSelected();
                Debug.Log("PG05 visual trial: three pylons, two decorative spans. Save M03 if desired. No collisions or combat logic. Existing transforms preserved.", preview);
            }
            catch (Exception ex) { Debug.LogError("PG05: " + ex); }
        }

        private static GameObject BuildPrefab(Data data, string group, string path,
            System.Collections.Generic.Dictionary<string, Material> mats)
        {
            var model = new GameObject(group == "Pylon" ? "PG05_Pylon" : "PG05_LineSpan");
            try
            {
                foreach (var part in data.parts.Where(p => p.group == group))
                {
                    string name = "PG05_" + part.name;
                    string meshPath = Art + "/" + name + ".asset";
                    var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    bool create = mesh == null;
                    if (create) mesh = new Mesh { name = name }; else mesh.Clear();
                    mesh.indexFormat = part.triangles.Length > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
                    mesh.vertices = part.triangles.Select(i => part.vertices[i]).ToArray();
                    mesh.triangles = Enumerable.Range(0, mesh.vertexCount).ToArray();
                    mesh.RecalculateNormals(); mesh.RecalculateBounds();
                    if (create) AssetDatabase.CreateAsset(mesh, meshPath); else EditorUtility.SetDirty(mesh);
                    var go = new GameObject(part.name, typeof(MeshFilter), typeof(MeshRenderer));
                    go.transform.SetParent(model.transform, false);
                    go.GetComponent<MeshFilter>().sharedMesh = mesh;
                    go.GetComponent<MeshRenderer>().sharedMaterial = mats[part.material];
                    if (group == "Span") go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                var prefab = PrefabUtility.SaveAsPrefabAsset(model, path);
                if (prefab == null) throw new InvalidOperationException("Failed to save " + path);
                return prefab;
            }
            finally { Object.DestroyImmediate(model); }
        }

        private static void Place(GameObject prefab, Scene scene, Transform parent, string name, float z)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = new Vector3(0f, 0f, z);
            instance.transform.localRotation = Quaternion.identity;
            instance.name = name;
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
        }

        private static bool SafeName(string value) => !string.IsNullOrEmpty(value) && value.All(c => char.IsLetterOrDigit(c) || c == '_');
    }
}
