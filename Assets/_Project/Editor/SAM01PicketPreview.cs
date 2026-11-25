using System;
using UnityEditor;
using UnityEngine;

namespace AirCombat
{
    // Visual-only authoring. No scene edits or combat components.
    internal static class SAM01PicketPreview
    {
        private const string Art = "Assets/_Project/Art/Environment/Dominion/SAM01_Picket/";
        private const string Prefab = "Assets/_Project/Prefabs/Environment/SAM01_Picket.prefab";
        private static readonly string[] Parts = { "Hull", "Equipment", "Metal", "Rubber", "Glass", "Caps" };
        private static readonly Color[] Clay = {
            new Color(.51f,.51f,.51f), new Color(.41f,.41f,.41f),
            new Color(.30f,.30f,.30f), new Color(.16f,.16f,.16f),
            new Color(.22f,.24f,.25f), new Color(.65f,.65f,.65f)
        };

        [MenuItem("Air Combat/Art/Create SAM01 Picket Preview")]
        private static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            if (existing != null) { Selection.activeObject = existing; EditorGUIUtility.PingObject(existing); return; }
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) { Debug.LogError("SAM01: URP Lit missing."); return; }
            var models = new GameObject[Parts.Length];
            for (int i = 0; i < Parts.Length; i++)
            {
                models[i] = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "SAM01_" + Parts[i] + ".obj");
                if (models[i] == null || models[i].GetComponentInChildren<MeshFilter>(true) == null)
                { Debug.LogError("SAM01: missing OBJ: " + Parts[i]); return; }
            }
            GameObject root = null;
            try
            {
                root = new GameObject("SAM01_Picket");
                for (int i = 0; i < Parts.Length; i++)
                {
                    string materialPath = Art + "SAM01_" + Parts[i] + ".mat";
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if (mat == null)
                    {
                        mat = new Material(shader) { name = "SAM01_" + Parts[i] };
                        mat.SetColor("_BaseColor", Clay[i]);
                        mat.SetFloat("_Metallic", 0f);
                        mat.SetFloat("_Smoothness", 0f);
                        mat.SetFloat("_SpecularHighlights", 0f);
                        mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                        mat.SetFloat("_EnvironmentReflections", 0f);
                        mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                        AssetDatabase.CreateAsset(mat, materialPath);
                    }
                    var part = (GameObject)PrefabUtility.InstantiatePrefab(models[i], root.transform);
                    if (part == null) throw new InvalidOperationException("Could not instantiate " + Parts[i]);
                    part.name = Parts[i];
                    part.transform.localPosition = Vector3.zero;
                    part.transform.localRotation = Quaternion.identity;
                    part.transform.localScale = Vector3.one;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(part);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(part.transform);
                    foreach (var renderer in part.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        var materials = renderer.sharedMaterials;
                        for (int j = 0; j < materials.Length; j++) materials[j] = mat;
                        renderer.sharedMaterials = materials;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    }
                }
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, Prefab);
                if (prefab == null) throw new InvalidOperationException("Prefab save failed.");
                AssetDatabase.SaveAssets();
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
                Debug.Log("SAM01: visual prefab created. Open in Prefab Mode or drag into a test scene. No mission changes.");
            }
            catch (Exception ex) { Debug.LogError("SAM01: " + ex); }
            finally { if (root != null) UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
