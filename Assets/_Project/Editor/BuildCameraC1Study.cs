#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    // Non-destructive one-time camera experiment. Copies V19 and leaves it intact.
    public static class BuildCameraC1Study
    {
        private const string Source = "Assets/_Project/Scenes/FrontierPracticalBake.unity";
        private const string Destination = "Assets/_Project/Scenes/FrontierCameraC1.unity";

        [MenuItem("Air Combat/Art/C1 - Create Heading-Locked Camera Study")]
        private static void BuildFromMenu()
        {
            try { Debug.Log(Build()); }
            catch (Exception exception) { Debug.LogError("Camera C1: " + exception); }
        }

        public static string Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before building C1.");
            for (int i = 0; i < SceneManager.sceneCount; ++i)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save or discard scene changes before building C1.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Destination) != null ||
                System.IO.File.Exists(Destination))
                throw new InvalidOperationException("C1 already exists. Do not overwrite: " + Destination);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Source) == null)
                throw new InvalidOperationException("Missing V19 source scene: " + Source);

            var original = EditorSceneManager.OpenScene(Source, OpenSceneMode.Single);
            if (!EditorSceneManager.SaveScene(original, Destination, true))
                throw new InvalidOperationException("Could not save isolated C1 copy.");
            var scene = EditorSceneManager.OpenScene(Destination, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var player = roots.Single(root => root.name == "Player");
            var cameraRoot = roots.Single(root => root.name == "Main Camera");
            var camera = cameraRoot.GetComponent<Camera>();
            var originalFollow = cameraRoot.GetComponent<TopDownCameraFollow>();
            var artController = roots.SelectMany(root => root.GetComponentsInChildren<ArtLabLandscapeFlight>(true)).Single();
            if (camera == null || originalFollow == null || player.GetComponent<FlightController>() == null)
                throw new InvalidOperationException("V19 camera/player contract changed. C1 not compatible.");

            // Only the cloned scene loses its fixed-yaw camera driver.
            originalFollow.enabled = false;
            var oldLead = cameraRoot.GetComponent<CameraLookAhead>();
            if (oldLead != null) oldLead.enabled = false;
            if (cameraRoot.GetComponent<HeadingLockedObliqueCameraStudy>() != null)
                throw new InvalidOperationException("C1 component unexpectedly already exists.");
            cameraRoot.AddComponent<HeadingLockedObliqueCameraStudy>().Configure(player.transform);
            camera.orthographic = true;
            camera.orthographicSize = 80f;
            artController.SetStudyLegend("C1 / HEADING-LOCKED CAMERA (V19 environment)",
                "C: locked / north-up   1/2/3: tilt 50/58/65   H: hide");

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save C1 study scene.");
            Selection.activeGameObject = cameraRoot;
            return "Camera C1 ready: " + Destination +
                   " | Play, turn A/D, toggle C for old vs new, try 1/2/3 tilt.";
        }
    }
}
#endif
