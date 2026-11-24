using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    // Separate imported model. The old C7 meshes are not reused or rewritten.
    internal static class Mission01CrewQuarters
    {
        private const string ScenePath = "Assets/_Project/Scenes/Mission01_FirstSortie.unity";
        private const string Art = "Assets/_Project/Art/Buildings/Base/CQ01_CrewQuarters/";
        private const string PrefabPath = "Assets/_Project/Prefabs/Environment/Base/CQ01_CrewQuarters.prefab";
        private const string UndoName = "Install CQ01 crew quarters";
        private static readonly string[] Parts = { "Walls", "Roof", "Trim", "Glass", "Door", "Step" };

        [MenuItem("Air Combat/Missions/Install CQ01 Crew Quarters")]
        private static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) { Debug.LogWarning("CQ01: open Mission01_FirstSortie first."); return; }
            var anchor = scene.GetRootGameObjects().SingleOrDefault(g => g.name == "Mission01 / Airfield");
            var layout = Child(anchor != null ? anchor.transform : null, "Airfield B - editable layout");
            var buildings = Child(layout, "Buildings");
            var old = Child(buildings, "Pilot barracks");
            var hangar = Child(buildings, "Arch hangar");
            if (old == null || !old.gameObject.activeSelf || hangar == null || Child(buildings, "CQ01 crew quarters") != null)
            {
                Debug.LogWarning("CQ01: expected installed C7 barracks and no CQ01 instance. Nothing changed.");
                return;
            }
            var models = Parts.Select(p => AssetDatabase.LoadAssetAtPath<GameObject>(Art + "CQ01_" + p + ".obj")).ToArray();
            if (models.Any(m => m == null || m.GetComponentsInChildren<MeshFilter>(true).All(f => f.sharedMesh == null)))
            { Debug.LogError("CQ01: missing imported model parts. Nothing changed."); return; }
            GameObject prefab;
            try
            {
                Material walls = MaterialOf(hangar,"Arch_Walls");
                Material roof = MaterialOf(hangar,"Arch_Roof");
                Material dark = MaterialOf(hangar,"Arch_DarkDetails");
                prefab = CreateOrLoadPrefab(models,new[] {walls,roof,dark,dark,dark,walls});
            }
            catch (Exception ex) { Debug.LogError("CQ01: asset preparation failed. " + ex); return; }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, buildings);
                if (instance == null) throw new InvalidOperationException("Prefab instantiation failed.");
                Undo.RegisterCreatedObjectUndo(instance, UndoName);
                instance.name = "CQ01 crew quarters";
                instance.transform.localPosition = new Vector3(232f,0f,-96f);
                // Resolve the actual imported door direction (OBJ import can mirror X).
                var door = Child(instance.transform,"Door").GetComponentInChildren<MeshRenderer>();
                float localX = instance.transform.InverseTransformPoint(door.bounds.center).x;
                if (Mathf.Abs(localX) < 9f) throw new InvalidOperationException("Unexpected door position.");
                instance.transform.localRotation = Quaternion.Euler(0f, localX > 0f ? 180f : 0f, 0f);
                instance.transform.localScale = Vector3.one;
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                Undo.RecordObject(old.gameObject,UndoName);
                old.gameObject.SetActive(false);
                Undo.CollapseUndoOperations(group);
                EditorSceneManager.MarkSceneDirty(scene);
                Selection.activeGameObject = instance;
                Debug.Log("CQ01: one crew quarters installed, C7 buildings and path disabled. Save scene and prefab.");
            }
            catch (Exception ex)
            {
                Undo.RevertAllDownToGroup(group);
                Debug.LogError("CQ01: scene change reverted. " + ex);
            }
        }

        private static GameObject CreateOrLoadPrefab(GameObject[] models, Material[] materials)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null) return existing;
            if (AssetDatabase.LoadMainAssetAtPath(PrefabPath) != null) throw new InvalidOperationException("Unexpected prefab path asset.");
            var root = new GameObject("CQ01_CrewQuarters");
            try
            {
                for (int i=0;i<Parts.Length;i++)
                {
                    var part=(GameObject)PrefabUtility.InstantiatePrefab(models[i],root.transform);
                    if (part == null) throw new InvalidOperationException("OBJ instantiation failed: " + Parts[i]);
                    part.name=Parts[i];
                    part.transform.localPosition=Vector3.zero;
                    part.transform.localRotation=Quaternion.identity;
                    part.transform.localScale=Vector3.one;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(part);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(part.transform);
                    foreach (var renderer in part.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        renderer.sharedMaterials=Enumerable.Repeat(materials[i],renderer.sharedMaterials.Length).ToArray();
                        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    }
                }
                var prefab=PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
                if (prefab == null) throw new InvalidOperationException("Could not save CQ01 prefab.");
                AssetDatabase.SaveAssets();
                return prefab;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static Transform Child(Transform parent,string name) => parent == null ? null : parent.Cast<Transform>().SingleOrDefault(t=>t.name==name);
        private static Material MaterialOf(Transform parent,string name)
        {
            var child=Child(parent,name);
            var renderer=child != null ? child.GetComponentInChildren<MeshRenderer>() : null;
            if (renderer == null || renderer.sharedMaterial == null) throw new InvalidOperationException("Missing hangar material: " + name);
            return renderer.sharedMaterial;
        }
    }
}
