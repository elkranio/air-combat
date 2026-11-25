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
        private static readonly string[] Parts = { "Hull", "Equipment", "Metal", "Rubber", "Glass", "Caps", "Launcher", "Accent" };
        private static readonly Color32[] ImperialOlive = {
            new Color32(98,107,66,255), new Color32(75,80,59,255),
            new Color32(52,59,50,255), new Color32(35,37,34,255),
            new Color32(40,53,58,255), new Color32(182,180,156,255),
            new Color32(160,161,124,255), new Color32(184,59,50,255)
        };

        private static readonly Color32[] IndustrialEmpire = {
            new Color32(102,107,104,255), new Color32(73,78,77,255),
            new Color32(41,45,48,255), new Color32(30,32,33,255),
            new Color32(35,48,54,255), new Color32(194,197,186,255),
            new Color32(174,181,177,255), new Color32(184,48,43,255)
        };

        [MenuItem("Air Combat/Art/Create SAM01 Picket Preview")]
        private static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            if (existing != null) { Selection.activeObject = existing; EditorGUIUtility.PingObject(existing); return; }
            ApplyImperialOlive();
        }

        [MenuItem("Air Combat/Art/Apply SAM01 Imperial Olive")]
        private static void ApplyImperialOlive() => ApplyPalette(ImperialOlive, "Imperial Olive (A)");

        [MenuItem("Air Combat/Art/Apply SAM01 Industrial Empire")]
        private static void ApplyIndustrialEmpire() => ApplyPalette(IndustrialEmpire, "Industrial Empire (B)");

        private static void ApplyPalette(Color32[] palette, string paletteName)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) { Debug.LogError("SAM01: URP Lit missing."); return; }
            var models = new GameObject[Parts.Length];
            for (int i = 0; i < Parts.Length; i++)
            {
                models[i] = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "SAM01_" + Parts[i] + ".obj");
                if (models[i] == null || models[i].GetComponentInChildren<MeshFilter>(true) == null)
                { Debug.LogError("SAM01: missing OBJ: " + Parts[i]); return; }
            }
            bool existing = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab) != null;
            GameObject root = null;
            try
            {
                root = existing ? PrefabUtility.LoadPrefabContents(Prefab) : new GameObject("SAM01_Picket");
                // Verify existing parts before touching any materials. Only the two
                // new paint groups may be absent from the previous visual prefab.
                for (int i = 0; i < Parts.Length; i++)
                {
                    var child = root.transform.Find(Parts[i]);
                    if (existing && i < 6 && child == null)
                        throw new InvalidOperationException("Missing existing part " + Parts[i]);
                    if (child != null && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject) != AssetDatabase.GetAssetPath(models[i]))
                        throw new InvalidOperationException("Unexpected mesh source for " + Parts[i]);
                }
                for (int i = 0; i < Parts.Length; i++)
                {
                    string materialPath = Art + "SAM01_" + Parts[i] + ".mat";
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if (mat == null)
                    {
                        mat = new Material(shader) { name = "SAM01_" + Parts[i] };
                        AssetDatabase.CreateAsset(mat, materialPath);
                    }
                    mat.SetColor("_BaseColor", palette[i]);
                    mat.SetFloat("_Metallic", 0f);
                    mat.SetFloat("_Smoothness", 0f);
                    mat.SetFloat("_SpecularHighlights", 0f);
                    mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                    mat.SetFloat("_EnvironmentReflections", 0f);
                    mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                    EditorUtility.SetDirty(mat);
                    var child = root.transform.Find(Parts[i]);
                    var part = child != null ? child.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(models[i], root.transform);
                    if (part == null) throw new InvalidOperationException("Could not instantiate " + Parts[i]);
                    if (child == null)
                    {
                        part.name = Parts[i];
                        part.transform.localPosition = Vector3.zero;
                        part.transform.localRotation = Quaternion.identity;
                        part.transform.localScale = Vector3.one;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(part);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(part.transform);
                    }
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
                Debug.Log($"SAM01: {paletteName} applied. Existing scene instances use updated prefab unless overridden. No scene edits.");
            }
            catch (Exception ex) { Debug.LogError("SAM01: " + ex); }
            finally
            {
                if (root != null)
                {
                    if (existing) PrefabUtility.UnloadPrefabContents(root);
                    else UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }
    }
}
