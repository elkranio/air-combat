using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    internal static class BD05PicketSamAuthoring
    {
        [MenuItem("Air Combat/Missions/M02 - Enable BD05 Picket SAM")]
        private static void Enable()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/_Project/Scenes/Mission02_ConvoyInterdiction.unity" || scene.isDirty)
            { Debug.LogError("BD05: open and save M02 first."); return; }
            var trials = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Mission02RouteTrial>(true)).ToArray();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Environment/SAM01_Picket.prefab");
            var template = prefab != null ? prefab.GetComponentInChildren<SurfaceToAirLauncher>(true) : null;
            if (trials.Length != 1 || template == null || !template.IsConfigured)
            { Debug.LogError("BD05: expected one M02 trial and a configured stationary Picket prefab."); return; }
            var trial = trials[0];
            var so = new SerializedObject(trial);
            Undo.RecordObject(trial, "BD05 Picket SAM trial");
            so.FindProperty("picketSamTemplate").objectReferenceValue = template;
            so.FindProperty("picketSamEnabled").boolValue = true;
            so.FindProperty("baseDeploymentTrial").boolValue = true;
            so.FindProperty("baseTrialHoldAircraft").boolValue = false; // free flight: stationary jet is an easy target
            so.FindProperty("carryallFiringRange").boolValue = false;
            so.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            { Debug.LogError("BD05: scene save failed; review Inspector settings.", trial); return; }
            Selection.activeGameObject = trial.gameObject;
            Debug.Log("BD05: SAM enabled; base trial / free flight. Toggle Hold Aircraft or Picket Sam Enabled and R to inspect.", trial);
        }
    }
}
