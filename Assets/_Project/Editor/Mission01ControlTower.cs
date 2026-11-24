using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    // Explicit art authoring command. No runtime generation and no shared prefab changes.
    internal static class Mission01ControlTower
    {
        private const string ScenePath = "Assets/_Project/Scenes/Mission01_FirstSortie.unity";
        private const string Art = "Assets/_Project/Art/Buildings/Base/CT01_ControlTower/";
        private const string PrefabPath = "Assets/_Project/Prefabs/Environment/Base/CT01_ControlTower.prefab";
        private const string UndoName = "Install CT01 control tower";
        private static readonly string[] Parts = { "Concrete", "Trim", "Glass", "Roof", "Beacon" };
        private static readonly Color32[] Colors = {
            new Color32(174,166,145,255), new Color32(80,91,91,255),
            new Color32(52,95,105,255), new Color32(129,143,140,255),
            new Color32(224,90,58,255)
        };

        [MenuItem("Air Combat/Missions/Install CT01 Control Tower")]
        private static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Debug.LogWarning("CT01: open Mission01_FirstSortie first.");
                return;
            }
            var anchor = scene.GetRootGameObjects().SingleOrDefault(g => g.name == "Mission01 / Airfield");
            var layout = anchor != null ? Child(anchor.transform, "Airfield B - editable layout") : null;
            var buildings = layout != null ? Child(layout, "Buildings") : null;
            var old = buildings != null ? Child(buildings, "Control tower") : null;
            if (old == null || !old.gameObject.activeSelf || Child(buildings, "CT01 control tower") != null)
            {
                Debug.LogWarning("CT01: expected the active original tower and no CT01 instance. Nothing changed.");
                return;
            }
            var models = Parts.Select(p => AssetDatabase.LoadAssetAtPath<GameObject>(Art + "CT01_" + p + ".obj")).ToArray();
            if (models.Any(m => m == null || m.GetComponentsInChildren<MeshFilter>(true).All(f => f.sharedMesh == null)))
            {
                Debug.LogError("CT01: missing imported OBJ meshes. Nothing changed.");
                return;
            }
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) { Debug.LogError("CT01: URP Lit shader missing."); return; }
            GameObject prefab;
            try { prefab = CreateOrLoadPrefab(models, shader); }
            catch (Exception ex) { Debug.LogError("CT01: could not prepare assets. " + ex); return; }
            if (prefab == null) return;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, buildings);
                if (instance == null) throw new InvalidOperationException("Prefab instantiation failed.");
                Undo.RegisterCreatedObjectUndo(instance, UndoName);
                instance.name = "CT01 control tower";
                instance.transform.localPosition = old.localPosition;
                instance.transform.localRotation = old.localRotation;
                instance.transform.localScale = Vector3.one;
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                Undo.RecordObject(old.gameObject, UndoName);
                old.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(old.gameObject);
                Undo.CollapseUndoOperations(group);
                EditorSceneManager.MarkSceneDirty(scene);
                Selection.activeGameObject = instance;
                Debug.Log("CT01: installed. Original tower disabled; review and save the scene.");
            }
            catch (Exception ex)
            {
                Undo.RevertAllDownToGroup(group);
                Debug.LogError("CT01: scene change reverted. " + ex);
            }
        }

        private static GameObject CreateOrLoadPrefab(GameObject[] models, Shader shader)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null) return existing;
            if (AssetDatabase.LoadMainAssetAtPath(PrefabPath) != null)
                throw new InvalidOperationException("Unexpected asset at CT01 prefab path.");
            var root = new GameObject("CT01_ControlTower");
            try
            {
                for (int i = 0; i < Parts.Length; i++)
                {
                    string path = Art + "CT01_" + Parts[i] + ".mat";
                    var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (material == null)
                    {
                        if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                            throw new InvalidOperationException("Unexpected asset at " + path);
                        material = new Material(shader) { name = "CT01_" + Parts[i] };
                        material.SetColor("_BaseColor", Colors[i]);
                        material.SetFloat("_Metallic", 0f);
                        material.SetFloat("_Smoothness", 0f);
                        material.SetFloat("_SpecularHighlights", 0f);
                        material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                        material.SetFloat("_EnvironmentReflections", 0f);
                        material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                        AssetDatabase.CreateAsset(material, path);
                    }
                    var part = (GameObject)PrefabUtility.InstantiatePrefab(models[i], root.transform);
                    if (part == null) throw new InvalidOperationException("OBJ instantiation failed.");
                    part.transform.localPosition = Vector3.zero;
                    part.transform.localRotation = Quaternion.identity;
                    part.transform.localScale = Vector3.one;
                    foreach (var renderer in part.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    }
                    PrefabUtility.RecordPrefabInstancePropertyModifications(part.transform);
                }
                // Same visual-only scope as the current mission. No new collision logic.
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                if (prefab == null) throw new InvalidOperationException("Could not save CT01 prefab.");
                return prefab;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static Transform Child(Transform parent, string name) =>
            parent.Cast<Transform>().SingleOrDefault(t => t.name == name);
    }
}
