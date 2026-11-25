using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    // The B1f accent mesh contains two independent boxes: the portal stripe
    // and the service-annex door stripe. C7 removed the annex but not its stripe.
    internal static class Mission01HangarAccentCleanup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Mission01_FirstSortie.unity";
        private const string PortalModel = "Assets/_Project/Art/Environment/Mission01_FirstSortie/Arch_PortalAccent.obj";
        private const string UndoName = "Remove orphan hangar annex accent";

        [MenuItem("Air Combat/Missions/Clean Up Mission 01 Hangar Accent")]
        private static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Debug.LogWarning("C8a: open Mission01_FirstSortie first.");
                return;
            }
            try
            {
                var anchor = scene.GetRootGameObjects().Single(g => g.name == "Mission01 / Airfield");
                var layout = Child(anchor.transform, "Airfield B - editable layout");
                var hangar = Child(Child(layout, "Buildings"), "Arch hangar");
                if (Child(hangar, "Arch_SideAnnex").gameObject.activeSelf)
                    throw new InvalidOperationException("Expected the annex to be disabled by C7.");

                var filter = Child(hangar, "Arch_Accent").GetComponentsInChildren<MeshFilter>(true).Single();
                var replacement = AssetDatabase.LoadAllAssetsAtPath(PortalModel).OfType<Mesh>().Single();
                if (filter.sharedMesh == replacement)
                {
                    Debug.Log("C8a: orphan annex stripe already removed.");
                    return;
                }
                if (filter.sharedMesh == null || filter.sharedMesh.subMeshCount != 1 ||
                    filter.sharedMesh.GetIndexCount(0) != 72 || replacement.subMeshCount != 1 ||
                    replacement.GetIndexCount(0) != 36)
                    throw new InvalidOperationException("Unexpected original or replacement accent mesh.");

                Undo.RecordObject(filter, UndoName);
                filter.sharedMesh = replacement;
                PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log("C8a: removed the annex stripe; portal stripe and CQ01 unchanged. Save the scene.");
            }
            catch (Exception ex)
            {
                Debug.LogError("C8a: " + ex.Message);
            }
        }

        private static Transform Child(Transform parent, string name) =>
            parent.Cast<Transform>().Single(t => t.name == name);
    }
}
