using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AirCombat
{
    // Decorative art only. Never regenerate M03 or reposition existing instances.
    internal static class PG04SwitchyardAuthoring
    {
        private const string Root = "Assets/_Project/";
        private const string ScenePath = Root + "Scenes/Mission03_SparrowInterception.unity";
        private const string Geometry = Root + "Editor/PG04Geometry.json";
        private const string Parent = Root + "Art/Environment/Infrastructure";
        private const string Art = Parent + "/PG04_Switchyard";
        private const string Prefab = Root + "Prefabs/Environment/PG04_Switchyard.prefab";
        private static readonly string[] Names = { "M03 / PG04 Portal A - VISUAL TRIAL", "M03 / PG04 Portal B - VISUAL TRIAL" };
        private static readonly Vector3[] Positions = { new Vector3(-200f, -.04f, -752f), new Vector3(-164f, -.04f, -752f) };
        private static readonly string[] Groups = { "Foundation", "Frame", "Details", "Cabinets" };
        [Serializable] private sealed class Paint { public string name; public int[] rgb; }
        [Serializable] private sealed class Part { public string name, group, material; public Vector3[] vertices; public int[] triangles; }
        [Serializable] private sealed class Data { public Paint[] palette; public Part[] parts; }

        [MenuItem("Air Combat/Missions/M03 - Build PG04 Switchyard Preview")]
        private static void BuildAndPlace()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Debug.LogError("PG04: open Mission03_SparrowInterception first."); return;
            }
            GameObject model = null;
            try
            {
                var source = AssetDatabase.LoadAssetAtPath<TextAsset>(Geometry);
                var data = source != null ? JsonUtility.FromJson<Data>(source.text) : null;
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (data == null || data.palette == null || data.parts == null || data.parts.Length == 0 || shader == null ||
                    data.palette.Any(p => p == null || !SafeName(p.name) || p.rgb == null || p.rgb.Length != 3 || p.rgb.Any(c => c < 0 || c > 255)) ||
                    data.palette.Select(p => p.name).Distinct().Count() != data.palette.Length ||
                    data.parts.Any(p => p == null || !Groups.Contains(p.group) || !SafeName(p.material) ||
                        p.name != p.group + "_" + p.material || p.vertices == null || p.vertices.Length == 0 ||
                        p.triangles == null || p.triangles.Length == 0 || p.triangles.Length % 3 != 0 ||
                        p.triangles.Any(i => i < 0 || i >= p.vertices.Length) ||
                        !data.palette.Any(m => m.name == p.material)) ||
                    data.parts.Select(p => p.name).Distinct().Count() != data.parts.Length)
                    throw new InvalidOperationException("Invalid PG04 geometry, palette or shader.");

                var prior = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
                var instances = new GameObject[Names.Length];
                for (int i = 0; i < Names.Length; i++)
                {
                    var matches = scene.GetRootGameObjects().Where(r => r.name == Names[i]).ToArray();
                    if (matches.Length > 1 || (matches.Length == 1 && (prior == null ||
                        PrefabUtility.GetCorrespondingObjectFromSource(matches[0]) != prior)))
                        throw new InvalidOperationException("Unexpected or duplicate PG04 preview; scene unchanged.");
                    instances[i] = matches.SingleOrDefault();
                }
                if (!AssetDatabase.IsValidFolder(Parent)) AssetDatabase.CreateFolder(Root + "Art/Environment", "Infrastructure");
                if (!AssetDatabase.IsValidFolder(Art)) AssetDatabase.CreateFolder(Parent, "PG04_Switchyard");
                var mats = data.palette.ToDictionary(p => p.name, p =>
                {
                    string path = Art + "/PG04_" + p.name + ".mat";
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (mat == null)
                    {
                        mat = new Material(shader) { name = "PG04_" + p.name };
                        AssetDatabase.CreateAsset(mat, path);
                    }
                    mat.SetColor("_BaseColor", new Color32((byte)p.rgb[0], (byte)p.rgb[1], (byte)p.rgb[2], 255));
                    mat.SetFloat("_Metallic", 0f); mat.SetFloat("_Smoothness", .08f);
                    EditorUtility.SetDirty(mat); return mat;
                });
                model = new GameObject("PG04_Switchyard");
                var visual = Child(model.transform, "Visual");
                var groups = Groups.ToDictionary(g => g, g => Child(visual, g));
                foreach (var part in data.parts)
                {
                    string name = "PG04_" + part.name;
                    string path = Art + "/" + name + ".asset";
                    var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    bool create = mesh == null;
                    if (create) mesh = new Mesh { name = name }; else mesh.Clear();
                    mesh.indexFormat = part.triangles.Length > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
                    mesh.vertices = part.triangles.Select(i => part.vertices[i]).ToArray();
                    mesh.triangles = Enumerable.Range(0, mesh.vertexCount).ToArray();
                    mesh.RecalculateNormals(); mesh.RecalculateBounds();
                    if (create) AssetDatabase.CreateAsset(mesh, path); else EditorUtility.SetDirty(mesh);
                    var go = new GameObject(part.name, typeof(MeshFilter), typeof(MeshRenderer));
                    go.transform.SetParent(groups[part.group], false);
                    go.GetComponent<MeshFilter>().sharedMesh = mesh;
                    go.GetComponent<MeshRenderer>().sharedMaterial = mats[part.material];
                }
                var prefab = PrefabUtility.SaveAsPrefabAsset(model, Prefab);
                if (prefab == null) throw new InvalidOperationException("PG04 prefab save failed.");
                Object.DestroyImmediate(model); model = null;
                AssetDatabase.SaveAssets();
                for (int i = 0; i < Names.Length; i++)
                {
                    if (instances[i] != null) continue;
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    Undo.RegisterCreatedObjectUndo(instance, "PG04 preview");
                    instance.name = Names[i];
                    instance.transform.SetPositionAndRotation(Positions[i], Quaternion.identity);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                    instances[i] = instance;
                    EditorSceneManager.MarkSceneDirty(scene);
                }
                Selection.objects = instances.Cast<Object>().ToArray();
                SceneView.lastActiveSceneView?.FrameSelected();
                Debug.Log("PG04 visual trial: two decorative portals. Save M03 if desired. Existing scene transforms untouched.", instances[0]);
            }
            catch (Exception ex) { Debug.LogError("PG04: " + ex); }
            finally { if (model != null) Object.DestroyImmediate(model); }
        }

        private static bool SafeName(string value) => !string.IsNullOrEmpty(value) && value.All(c => char.IsLetterOrDigit(c) || c == '_');
        private static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false); return child;
        }
    }
}
