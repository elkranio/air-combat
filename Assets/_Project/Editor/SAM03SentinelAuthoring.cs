using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AirCombat
{
    // Visual-only authoring. Keep the imported mesh/material paths stable across passes.
    internal static class SAM03SentinelAuthoring
    {
        private const string ScenePath = "Assets/_Project/Scenes/CombatSandbox.unity";
        private const string Geometry = "Assets/_Project/Editor/SAM03Geometry.json";
        private const string Parent = "Assets/_Project/Art/Environment/Trident";
        private const string Art = Parent + "/SAM03_Sentinel";
        private const string Prefab = "Assets/_Project/Prefabs/Environment/SAM03_Sentinel.prefab";
        private const string Preview = "SAM03 / Sentinel - VISUAL TRIAL";
        private static readonly string[] Groups = { "Fixed", "Yaw", "Left", "Right", "Radar" };
        [Serializable] private sealed class Paint { public string name; public int[] rgb; }
        [Serializable] private sealed class Part { public string name, group, material; public Vector3[] vertices; public int[] triangles; }
        [Serializable] private sealed class Data { public Paint[] palette; public Part[] parts; }

        [MenuItem("Air Combat/Sandbox/SAM03 - Build Sentinel Preview")]
        private static void BuildAndPlace()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Debug.LogError("SAM03: open CombatSandbox first."); return;
            }
            GameObject model = null;
            try
            {
                var text = AssetDatabase.LoadAssetAtPath<TextAsset>(Geometry);
                var data = text != null ? JsonUtility.FromJson<Data>(text.text) : null;
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (data == null || data.palette == null || data.parts == null || data.parts.Length == 0 || shader == null ||
                    !AssetDatabase.IsValidFolder("Assets/_Project/Art/Environment") ||
                    data.parts.Select(p => p.name).Distinct().Count() != data.parts.Length ||
                    data.palette.Select(p => p.name).Distinct().Count() != data.palette.Length ||
                    data.palette.Any(p => p.rgb == null || p.rgb.Length != 3 || p.rgb.Any(c => c < 0 || c > 255)) ||
                    data.parts.Any(p => !Groups.Contains(p.group) || p.name != p.group + "_" + p.material ||
                        p.vertices == null || p.triangles == null || p.triangles.Length == 0 || p.triangles.Length % 3 != 0 ||
                        p.triangles.Any(i => i < 0 || i >= p.vertices.Length) || !data.palette.Any(m => m.name == p.material)))
                    throw new InvalidOperationException("Invalid SAM03 geometry, palette or URP shader.");
                var existing = scene.GetRootGameObjects().Where(r => r.name == Preview).ToArray();
                if (existing.Length > 1) throw new InvalidOperationException("Duplicate SAM03 previews.");
                var priorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
                if (existing.Length == 1 && (priorPrefab == null || PrefabUtility.GetCorrespondingObjectFromSource(existing[0]) != priorPrefab))
                    throw new InvalidOperationException("Preview is not linked to the expected prefab.");
                if (!AssetDatabase.IsValidFolder(Parent)) AssetDatabase.CreateFolder("Assets/_Project/Art/Environment", "Trident");
                if (!AssetDatabase.IsValidFolder(Art)) AssetDatabase.CreateFolder(Parent, "SAM03_Sentinel");
                var mats = data.palette.ToDictionary(p => p.name, p =>
                {
                    string path = Art + "/SAM03_" + p.name + ".mat";
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (mat == null)
                    {
                        mat = new Material(shader) { name = "SAM03_" + p.name };
                        AssetDatabase.CreateAsset(mat, path);
                    }
                    mat.SetColor("_BaseColor", new Color32((byte)p.rgb[0], (byte)p.rgb[1], (byte)p.rgb[2], 255));
                    mat.SetFloat("_Metallic", 0f); mat.SetFloat("_Smoothness", .10f);
                    EditorUtility.SetDirty(mat); return mat;
                });
                // Visual nodes have stable names. Yaw and pod groups can be animated later.
                model = new GameObject("SAM03_Sentinel");
                var visual = Child(model.transform, "Visual");
                var fixedRoot = Child(visual, "Fixed");
                var yaw = Child(visual, "Yaw");
                var left = Child(yaw, "Left"); var right = Child(yaw, "Right");
                var radar = Child(yaw, "Radar");
                foreach (var part in data.parts)
                {
                    var parent = part.group == "Fixed" ? fixedRoot : part.group == "Yaw" ? yaw : part.group == "Left" ? left : part.group == "Right" ? right : radar;
                    string name = "SAM03_" + part.name;
                    string path = Art + "/" + name + ".asset";
                    var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    bool create = mesh == null;
                    if (create) mesh = new Mesh { name = name };
                    else mesh.Clear();
                    // Expand indexed triangles for flat faceted normals, matching IN01 authoring.
                    mesh.vertices = part.triangles.Select(i => part.vertices[i]).ToArray();
                    mesh.triangles = Enumerable.Range(0, mesh.vertexCount).ToArray();
                    mesh.RecalculateNormals(); mesh.RecalculateBounds();
                    if (create) AssetDatabase.CreateAsset(mesh, path);
                    else EditorUtility.SetDirty(mesh);
                    var go = new GameObject(part.name, typeof(MeshFilter), typeof(MeshRenderer));
                    go.transform.SetParent(parent, false);
                    go.GetComponent<MeshFilter>().sharedMesh = mesh;
                    go.GetComponent<MeshRenderer>().sharedMaterial = mats[part.material];
                }
                var prefab = PrefabUtility.SaveAsPrefabAsset(model, Prefab);
                if (prefab == null) throw new InvalidOperationException("SAM03 prefab save failed.");
                Object.DestroyImmediate(model); model = null;
                AssetDatabase.SaveAssets();
                var instance = existing.SingleOrDefault();
                if (instance == null)
                {
                    instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    Undo.RegisterCreatedObjectUndo(instance, "SAM03 preview");
                    instance.name = Preview;
                    // Near Bastion art trial, with a separate footprint; preserve on rebuild.
                    var position = new Vector3(-722f, 0f, -280f);
                    var terrain = Terrain.activeTerrains.FirstOrDefault(t => t.gameObject.scene == scene &&
                        position.x >= t.transform.position.x && position.z >= t.transform.position.z &&
                        position.x <= t.transform.position.x + t.terrainData.size.x &&
                        position.z <= t.transform.position.z + t.terrainData.size.z);
                    if (terrain != null) position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
                    instance.transform.SetPositionAndRotation(position, Quaternion.identity);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                    EditorSceneManager.MarkSceneDirty(scene);
                }
                Selection.activeGameObject = instance;
                SceneView.lastActiveSceneView?.FrameSelected();
                Debug.Log("SAM03 V1 visual only. Preview selected; save Sandbox if desired. No gameplay components added.", instance);
            }
            catch (Exception ex) { Debug.LogError("SAM03: " + ex); }
            finally { if (model != null) Object.DestroyImmediate(model); }
        }

        private static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false); return child;
        }
    }
}
