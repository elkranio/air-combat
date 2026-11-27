using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AirCombat
{
    // Visual trial only. Stable paths allow later model passes without moving
    // the existing scene instance or breaking its mesh/material references.
    internal static class PG03TransformerLongAuthoring
    {
        private const string Root = "Assets/_Project/";
        private const string ScenePath = Root + "Scenes/Mission03_SparrowInterception.unity";
        private const string Geometry = Root + "Editor/PG03Geometry.json";
        private const string Parent = Root + "Art/Environment/Infrastructure";
        private const string Art = Parent + "/PG03_TransformerLong";
        private const string Prefab = Root + "Prefabs/Environment/PG03_TransformerLong.prefab";
        private const string Preview = "M03 / PG03 Transformer Long - VISUAL TRIAL";
        private static readonly string[] Groups = { "Foundation", "Body", "Cooling", "Crown" };
        [Serializable] private sealed class Paint { public string name; public int[] rgb; }
        [Serializable] private sealed class Part { public string name, group, material; public Vector3[] vertices; public int[] triangles; }
        [Serializable] private sealed class Data { public Paint[] palette; public Part[] parts; }

        [MenuItem("Air Combat/Missions/M03 - Build PG03 Transformer Long Preview")]
        private static void BuildAndPlace()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Debug.LogError("PG03: open Mission03_SparrowInterception first."); return;
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
                    throw new InvalidOperationException("Invalid PG03 geometry, palette or shader.");

                var existing = scene.GetRootGameObjects().Where(r => r.name == Preview).ToArray();
                var prior = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
                if (existing.Length > 1 || (existing.Length == 1 && (prior == null ||
                    PrefabUtility.GetCorrespondingObjectFromSource(existing[0]) != prior)))
                    throw new InvalidOperationException("Unexpected or duplicate PG03 preview; scene unchanged.");

                if (!AssetDatabase.IsValidFolder(Parent)) AssetDatabase.CreateFolder(Root + "Art/Environment", "Infrastructure");
                if (!AssetDatabase.IsValidFolder(Art)) AssetDatabase.CreateFolder(Parent, "PG03_TransformerLong");
                var mats = data.palette.ToDictionary(p => p.name, p =>
                {
                    string path = Art + "/PG03_" + p.name + ".mat";
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (mat == null)
                    {
                        mat = new Material(shader) { name = "PG03_" + p.name };
                        AssetDatabase.CreateAsset(mat, path);
                    }
                    mat.SetColor("_BaseColor", new Color32((byte)p.rgb[0], (byte)p.rgb[1], (byte)p.rgb[2], 255));
                    mat.SetFloat("_Metallic", 0f); mat.SetFloat("_Smoothness", .08f);
                    EditorUtility.SetDirty(mat); return mat;
                });

                model = new GameObject("PG03_TransformerLong");
                var visual = Child(model.transform, "Visual");
                var groups = Groups.ToDictionary(g => g, g => Child(visual, g));
                foreach (var part in data.parts)
                {
                    string name = "PG03_" + part.name;
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
                if (prefab == null) throw new InvalidOperationException("PG03 prefab save failed.");
                Object.DestroyImmediate(model); model = null;
                AssetDatabase.SaveAssets();
                var instance = existing.SingleOrDefault();
                if (instance == null)
                {
                    instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    Undo.RegisterCreatedObjectUndo(instance, "PG03 preview");
                    instance.name = Preview;
                    // Existing flat reserved M03 site, east of PG01; temporary placement only.
                    instance.transform.SetPositionAndRotation(new Vector3(-164f, -.04f, -780f), Quaternion.identity);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                    EditorSceneManager.MarkSceneDirty(scene);
                }
                Selection.activeGameObject = instance;
                SceneView.lastActiveSceneView?.FrameSelected();
                Debug.Log("PG03 visual trial. Save M03 if desired. No collision, objectives or damage logic; existing scene transforms untouched.", instance);
            }
            catch (Exception ex) { Debug.LogError("PG03: " + ex); }
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
