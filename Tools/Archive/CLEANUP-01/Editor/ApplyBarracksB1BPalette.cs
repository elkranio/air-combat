#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace AirCombat
{
    // B1b visual-study palette on the already installed B1a prefab.
    // Material instance GUIDs are preserved; no prefab/scene duplication.
    public static class ApplyBarracksB1BPalette
    {
        private const string Folder = "Assets/_Project/Art/Buildings/Base/Barracks_B1a/";

        [MenuItem("Air Combat/Art/B1b - Apply Barracks Palette")]
        private static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("B1b: exit Play Mode before changing shared barracks materials.");
                return;
            }

            string[] names = { "B1a_Walls.mat", "B1a_Roof.mat", "B1a_Openings.mat", "B1a_Trim.mat" };
            string[] colors = { "#91A9A2", "#385568", "#253945", "#A78C68" };
            var materials = new Material[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                materials[i] = AssetDatabase.LoadAssetAtPath<Material>(Folder + names[i]);
                if (materials[i] == null || !materials[i].HasProperty("_BaseColor"))
                {
                    Debug.LogError("B1b: required URP material unavailable: " + Folder + names[i]);
                    return;
                }
            }

            Undo.RecordObjects(materials, "B1b barracks palette");
            for (int i = 0; i < materials.Length; i++)
            {
                if (!ColorUtility.TryParseHtmlString(colors[i], out var color))
                    throw new InvalidOperationException("Invalid barracks color: " + colors[i]);
                materials[i].SetColor("_BaseColor", color);
                if (materials[i].HasProperty("_Color"))
                    materials[i].SetColor("_Color", color);
                EditorUtility.SetDirty(materials[i]);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("B1b: barracks palette applied to existing four materials. No new assets or prefab IDs.");
        }
    }
}
#endif
