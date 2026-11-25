using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AirCombat
{
    // Art-only migration. Existing site components, socket and scene overrides
    // are deliberately not edited. All new meshes are separate visual objects.
    internal static class SAM01TwinRailVisual
    {
        private const string Art = "Assets/_Project/Art/Environment/Dominion/SAM01_Picket/";
        private const string Prefab = "Assets/_Project/Prefabs/Environment/SAM01_Picket.prefab";
        private static readonly string[] Names = { "LauncherDark", "MissileL", "MissileR" };
        private static readonly string[] Paint = { "Metal", "Caps", "Caps" };

        [MenuItem("Air Combat/Art/Apply SAM01 Twin Rail Visual")]
        private static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            GameObject root = null;
            try
            {
                var meshes = Names.Select(name =>
                {
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "SAM01_" + name + ".obj");
                    var filters = asset == null ? Array.Empty<MeshFilter>() : asset.GetComponentsInChildren<MeshFilter>(true);
                    if (filters.Length != 1 || filters[0].sharedMesh == null)
                        throw new InvalidOperationException("Missing mesh: " + name);
                    return filters[0].sharedMesh;
                }).ToArray();
                var materials = Paint.Select(name => AssetDatabase.LoadAssetAtPath<Material>(Art + "SAM01_" + name + ".mat")).ToArray();
                if (materials.Any(m => m == null)) throw new InvalidOperationException("Missing existing Metal/Caps palette.");
                if (AssetDatabase.LoadAssetAtPath<GameObject>(Prefab) == null)
                    throw new InvalidOperationException("Existing Picket prefab missing.");
                root = PrefabUtility.LoadPrefabContents(Prefab);
                if (root.transform.Find("Launcher") == null || root.transform.Find("Accent") == null)
                    throw new InvalidOperationException("Expected SAM01 C visual groups.");
                // Validate all existing children before modifying the prefab contents.
                for (int i = 0; i < Names.Length; i++)
                {
                    var existing = root.transform.Find(Names[i]);
                    if (existing != null && (existing.GetComponent<MeshFilter>() == null ||
                        existing.GetComponent<MeshRenderer>() == null || existing.childCount != 0))
                        throw new InvalidOperationException("Unexpected visual child: " + Names[i]);
                }
                for (int i = 0; i < Names.Length; i++)
                {
                    var existing = root.transform.Find(Names[i]);
                    var go = existing != null ? existing.gameObject : new GameObject(Names[i], typeof(MeshFilter), typeof(MeshRenderer));
                    if (existing == null) go.transform.SetParent(root.transform, false);
                    go.transform.localPosition = Vector3.zero;
                    go.transform.localRotation = Quaternion.identity;
                    go.transform.localScale = Vector3.one;
                    go.GetComponent<MeshFilter>().sharedMesh = meshes[i];
                    var renderer = go.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = materials[i];
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                    go.SetActive(true);
                }
                var saved = PrefabUtility.SaveAsPrefabAsset(root, Prefab);
                if (saved == null) throw new InvalidOperationException("Could not save Picket prefab.");
                AssetDatabase.SaveAssets();
                Selection.activeObject = saved;
                EditorGUIUtility.PingObject(saved);
                Debug.Log("SAM01C2: Twin rail visual installed. Existing prefab instances inherit the new art. No scene or combat edits.");
            }
            catch (Exception ex) { Debug.LogError("SAM01C2: " + ex); }
            finally { if (root != null) PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
