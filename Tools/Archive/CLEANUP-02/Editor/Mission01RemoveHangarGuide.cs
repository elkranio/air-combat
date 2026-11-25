using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    internal static class Mission01RemoveHangarGuide
    {
        [MenuItem("Air Combat/Missions/Remove Mission 01 Hangar Guide")]
        private static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/_Project/Scenes/Mission01_FirstSortie.unity") return;
            var anchor = scene.GetRootGameObjects().SingleOrDefault(g => g.name == "Mission01 / Airfield");
            var pavement = anchor != null
                ? anchor.transform.Find("Airfield B - editable layout/Unified pavement C6") : null;
            if (pavement == null)
            {
                Debug.LogWarning("C8c: missing C6 pavement; nothing changed.");
                return;
            }
            var guides = new[] { pavement.Find("Hangar guide - three dashes"),
                pavement.Find("Hangar guide - two dashes") };
            bool changed = false;
            foreach (var guide in guides)
            {
                if (guide == null || !guide.gameObject.activeSelf) continue;
                Undo.RecordObject(guide.gameObject, "Remove Mission 01 hangar guide");
                guide.gameObject.SetActive(false);
                changed = true;
            }
            if (changed) EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("C8c: hangar guide hidden. Save the mission scene.");
        }
    }
}
