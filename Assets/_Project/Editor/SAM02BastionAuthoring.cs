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
    internal static class SAM02BastionAuthoring
    {
        private const string ScenePath = "Assets/_Project/Scenes/CombatSandbox.unity";
        private const string Geometry = "Assets/_Project/Editor/SAM02Geometry.json";
        private const string Parent = "Assets/_Project/Art/Environment/Dominion";
        private const string Art = Parent + "/SAM02_Bastion";
        private const string Prefab = "Assets/_Project/Prefabs/Environment/SAM02_Bastion.prefab";
        private const string Preview = "SAM02 / Bastion - VISUAL TRIAL";
        private static readonly string[] Groups = { "Fixed", "Yaw", "Left", "Right" };
        [Serializable] private sealed class Paint { public string name; public int[] rgb; }
        [Serializable] private sealed class Part { public string name, group, material; public Vector3[] vertices; public int[] triangles; }
        [Serializable] private sealed class Data { public Paint[] palette; public Part[] parts; }

        [MenuItem("Air Combat/Sandbox/SAM02 - Build Bastion Preview")]
        private static void BuildAndPlace()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Debug.LogError("SAM02: open CombatSandbox first."); return;
            }
            GameObject model = null;
            try
            {
                var text = AssetDatabase.LoadAssetAtPath<TextAsset>(Geometry);
                var data = text != null ? JsonUtility.FromJson<Data>(text.text) : null;
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (data == null || data.palette == null || data.parts == null || data.parts.Length == 0 || shader == null ||
                    !AssetDatabase.IsValidFolder(Parent) ||
                    data.parts.Select(p => p.name).Distinct().Count() != data.parts.Length ||
                    data.palette.Select(p => p.name).Distinct().Count() != data.palette.Length ||
                    data.palette.Any(p => p.rgb == null || p.rgb.Length != 3 || p.rgb.Any(c => c < 0 || c > 255)) ||
                    data.parts.Any(p => !Groups.Contains(p.group) || p.name != p.group + "_" + p.material ||
                        p.vertices == null || p.triangles == null || p.triangles.Length == 0 || p.triangles.Length % 3 != 0 ||
                        p.triangles.Any(i => i < 0 || i >= p.vertices.Length) || !data.palette.Any(m => m.name == p.material)))
                    throw new InvalidOperationException("Invalid SAM02 geometry, palette or URP shader.");
                var existing = scene.GetRootGameObjects().Where(r => r.name == Preview).ToArray();
                if (existing.Length > 1) throw new InvalidOperationException("Duplicate SAM02 previews.");
                var priorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
                if (existing.Length == 1 && (priorPrefab == null || PrefabUtility.GetCorrespondingObjectFromSource(existing[0]) != priorPrefab))
                    throw new InvalidOperationException("Preview is not linked to the expected prefab.");
                if (!AssetDatabase.IsValidFolder(Art)) AssetDatabase.CreateFolder(Parent, "SAM02_Bastion");
                var mats = data.palette.ToDictionary(p => p.name, p =>
                {
                    string path = Art + "/SAM02_" + p.name + ".mat";
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (mat == null)
                    {
                        mat = new Material(shader) { name = "SAM02_" + p.name };
                        AssetDatabase.CreateAsset(mat, path);
                    }
                    mat.SetColor("_BaseColor", new Color32((byte)p.rgb[0], (byte)p.rgb[1], (byte)p.rgb[2], 255));
                    mat.SetFloat("_Metallic", 0f); mat.SetFloat("_Smoothness", .10f);
                    EditorUtility.SetDirty(mat); return mat;
                });
                // Visual nodes have stable names. Yaw and pod groups can be animated later.
                model = new GameObject("SAM02_Bastion");
                var visual = Child(model.transform, "Visual");
                var fixedRoot = Child(visual, "Fixed");
                var yaw = Child(visual, "Yaw");
                var left = Child(yaw, "Left"); var right = Child(yaw, "Right");
                foreach (var part in data.parts)
                {
                    var parent = part.group == "Fixed" ? fixedRoot : part.group == "Yaw" ? yaw : part.group == "Left" ? left : right;
                    string name = "SAM02_" + part.name;
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
                if (prefab == null) throw new InvalidOperationException("SAM02 prefab save failed.");
                Object.DestroyImmediate(model); model = null;
                AssetDatabase.SaveAssets();
                var instance = existing.SingleOrDefault();
                if (instance == null)
                {
                    instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    Undo.RegisterCreatedObjectUndo(instance, "SAM02 preview");
                    instance.name = Preview;
                    // Near the existing aircraft art trials, with a separate footprint.
                    var position = new Vector3(-748f, 0f, -280f);
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
                Debug.Log("SAM02 V2 visual only. Preview selected; save Sandbox if desired. No gameplay components added.", instance);
            }
            catch (Exception ex) { Debug.LogError("SAM02: " + ex); }
            finally { if (model != null) Object.DestroyImmediate(model); }
        }

        private static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false); return child;
        }
    }
}
