#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AirCombat
{
    // Cleanup A: extract the accepted camera from an E1 scene-only experiment
    // into a reusable prefab. One-time migration; Cleanup B retires this menu.
    public static class PrepareArcadeCameraPrefab
    {
        private const string Map = "Assets/_Project/Scenes/ArcadeDesertE1.unity";
        private const string Source = "Assets/_Project/Prefabs/Camera/CombatCameraRig.prefab";
        private const string Output = "Assets/_Project/Prefabs/Camera/ArcadeCameraRig.prefab";

        [MenuItem("Air Combat/Maintenance/Cleanup A - Extract Arcade Camera Prefab")]
        private static void FromMenu()
        {
            try { Debug.Log(Install()); }
            catch (Exception exception) { Debug.LogError("Cleanup A: " + exception); }
        }

        public static string Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save or discard all dirty scenes before migration.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Map) == null)
                throw new InvalidOperationException("Active map missing: " + Map);
            var sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
            if (sourcePrefab == null) throw new InvalidOperationException("Base camera prefab missing: " + Source);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Output) != null || System.IO.File.Exists(Output))
                throw new InvalidOperationException("Cleanup A already started. Inspect the saved camera prefab and map; never overwrite them.");

            var scene = EditorSceneManager.OpenScene(Map, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var oldRoot = roots.Single(x => x.name == "E1 / Main Camera");
            var playerRoot = roots.Single(x => x.GetComponent<JetAssembly>() != null);
            var player = playerRoot.GetComponent<JetAssembly>();
            var session = roots.SelectMany(x => x.GetComponentsInChildren<E1MapFlightSession>(true)).Single();
            var oldCamera = oldRoot.GetComponent<Camera>();
            var oldRig = oldRoot.GetComponent<CombatCameraRig>();
            var oldStudy = oldRoot.GetComponent<HeadingLockedObliqueCameraStudy>();
            if (oldCamera == null || oldRig == null || oldStudy == null ||
                oldRoot.GetComponent<ArcadeCameraFollow>() != null || !player.IsConfigured)
                throw new InvalidOperationException("The active map camera or aircraft differs from E1c; inspect before migration.");

            // The scene session is the only intentional external camera link.
            // Stop rather than silently destroy references from other gameplay.
            foreach (var root in roots)
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null || component == session || component.gameObject == oldRoot) continue;
                var serialized = new SerializedObject(component);
                var property = serialized.GetIterator();
                while (property.NextVisible(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var reference = property.objectReferenceValue;
                    if (reference == oldRoot || (reference is Component linked && linked.gameObject == oldRoot))
                        throw new InvalidOperationException("Unexpected scene camera reference in " +
                            component.GetType().Name + "." + property.propertyPath +
                            "; migrate that link explicitly before removing the old camera.");
                }
            }

            // Make a standalone prefab from the existing tested camera prefab,
            // without scene-owned player links or the two old pose writers.
            var prefabRoot = PrefabUtility.LoadPrefabContents(Source);
            try
            {
                prefabRoot.name = "ArcadeCameraRig";
                var lookAhead = prefabRoot.GetComponent<CameraLookAhead>();
                if (lookAhead != null) Object.DestroyImmediate(lookAhead);
                var oldFollow = prefabRoot.GetComponent<TopDownCameraFollow>();
                if (oldFollow != null) Object.DestroyImmediate(oldFollow);
                if (prefabRoot.GetComponent<CombatCameraRig>() == null ||
                    prefabRoot.GetComponent<TargetLock>() == null)
                    throw new InvalidOperationException("Source camera prefab changed; no stable rig/target lock.");
                if (prefabRoot.GetComponent<ArcadeCameraFollow>() != null)
                    throw new InvalidOperationException("Source camera already has the arcade follow component.");
                prefabRoot.AddComponent<ArcadeCameraFollow>(); // aircraft intentionally unset on asset.
                var prefabCamera = prefabRoot.GetComponent<Camera>();
                prefabCamera.orthographic = true;
                prefabCamera.orthographicSize = 80f;
                prefabCamera.farClipPlane = 700f;
                if (PrefabUtility.SaveAsPrefabAsset(prefabRoot, Output) == null)
                    throw new InvalidOperationException("Could not save arcade camera prefab: " + Output);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefabRoot); }

            var productPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Output);
            if (productPrefab == null) throw new InvalidOperationException("Camera prefab was not imported.");
            var replacement = PrefabUtility.InstantiatePrefab(productPrefab, scene) as GameObject;
            if (replacement == null) throw new InvalidOperationException("Could not instantiate arcade camera in E1.");
            replacement.name = oldRoot.name;
            replacement.tag = oldRoot.tag;
            replacement.transform.SetPositionAndRotation(oldRoot.transform.position, oldRoot.transform.rotation);
            var newCamera = replacement.GetComponent<Camera>();
            var newRig = replacement.GetComponent<CombatCameraRig>();
            var newFollow = replacement.GetComponent<ArcadeCameraFollow>();
            if (newCamera == null || newRig == null || newFollow == null)
                throw new InvalidOperationException("New camera prefab is missing required components.");
            newCamera.orthographic = oldCamera.orthographic;
            newCamera.orthographicSize = oldCamera.orthographicSize;
            newCamera.nearClipPlane = oldCamera.nearClipPlane;
            newCamera.farClipPlane = oldCamera.farClipPlane;
            newCamera.backgroundColor = oldCamera.backgroundColor;
            newCamera.clearFlags = oldCamera.clearFlags;
            newFollow.Configure(player.transform, player.GetComponent<Afterburner>());

            // The active E1 scene is an art-only flight test. Preserve disabled
            // target-lock/HUD state, but keep those components in the prefab for
            // eventual production combat scene composition.
            var oldLock = oldRoot.GetComponent<TargetLock>();
            var newLock = replacement.GetComponent<TargetLock>();
            if (oldLock != null && newLock != null) newLock.enabled = oldLock.enabled;
            var oldHud = oldRoot.GetComponent<TargetLockHud>();
            var newHud = replacement.GetComponent<TargetLockHud>();
            if (oldHud != null && newHud != null) newHud.enabled = oldHud.enabled;

            var settings = new SerializedObject(session);
            float boundary = settings.FindProperty("resetHalfExtent").floatValue;
            session.Configure(player, newRig, boundary);
            EditorUtility.SetDirty(session);
            PrefabUtility.RecordPrefabInstancePropertyModifications(replacement.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(newCamera);
            PrefabUtility.RecordPrefabInstancePropertyModifications(newFollow);
            if (newLock != null) PrefabUtility.RecordPrefabInstancePropertyModifications(newLock);
            if (newHud != null) PrefabUtility.RecordPrefabInstancePropertyModifications(newHud);
            Object.DestroyImmediate(oldRoot);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save E1 camera migration.");
            Selection.activeGameObject = replacement;
            return "Cleanup A done: " + Output + " | E1 uses a prefab camera with " +
                   "fixed 50-degree yaw / 48+14 forward lead. Play, test 7/8/9, shadows, W/Q/E and R.";
        }
    }
}
#endif
