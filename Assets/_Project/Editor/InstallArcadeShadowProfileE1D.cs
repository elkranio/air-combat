#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    // E1d: an isolated, possibly future-main URP profile. Do not alter the
    // existing CombatLightingRig prefab or the profile used by the combat labs.
    public static class InstallArcadeShadowProfileE1D
    {
        private const string ScenePath = "Assets/_Project/Scenes/ArcadeDesertE1.unity";
        private const string SourcePath = "Assets/Settings/ScorchedFrontier_URP.asset";
        private const string ProfileFolder = "Assets/_Project/Settings";
        private const string ProfilePath = ProfileFolder + "/ArcadeCombat_URP.asset";
        private const float ShadowDistance = 600f;

        [MenuItem("Air Combat/Art/E1d - Install Long-Range Shadows")]
        private static void InstallFromMenu()
        {
            try { Debug.Log(Install()); }
            catch (Exception error) { Debug.LogError("E1d shadows: " + error); }
        }

        public static string Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before installing E1d.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save or discard open scene changes first.");

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                throw new InvalidOperationException("The authored E1 map scene is missing. Do not regenerate it.");
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(ProfilePath) != null || File.Exists(ProfilePath))
                throw new InvalidOperationException("The E1d profile already exists; never overwrite a tuned profile: " + ProfilePath);
            var baseline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(SourcePath);
            if (baseline == null)
                throw new InvalidOperationException("Missing current shared URP profile: " + SourcePath);

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var rigRoot = roots.Single(x => x.name == "E1 / CombatLightingRig");
            var rig = rigRoot.GetComponent<CombatLightingRig>();
            var cameraRoot = roots.Single(x => x.name == "E1 / Main Camera");
            var view = cameraRoot.GetComponent<Camera>();
            if (rig == null || view == null || !PrefabUtility.IsPartOfPrefabInstance(rig))
                throw new InvalidOperationException("E1 must contain its prefab-instance light rig and camera.");
            if (view.farClipPlane < ShadowDistance)
                throw new InvalidOperationException("E1 camera far clip is shorter than the requested shadow distance.");

            var rigData = new SerializedObject(rig);
            var rigProfile = rigData.FindProperty("pipeline");
            if (rigProfile == null || rigProfile.objectReferenceValue != baseline)
                throw new InvalidOperationException("E1 lighting has changed. Inspect its existing pipeline override before E1d.");

            // Copy, rather than create a fresh URP asset: renderer features,
            // renderer data and all existing URP settings stay identical.
            if (!AssetDatabase.IsValidFolder(ProfileFolder))
                AssetDatabase.CreateFolder("Assets/_Project", "Settings");
            if (!AssetDatabase.CopyAsset(SourcePath, ProfilePath))
                throw new InvalidOperationException("Unable to duplicate the baseline URP asset.");
            var profile = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(ProfilePath);
            if (profile == null)
                throw new InvalidOperationException("URP copy was not imported: " + ProfilePath);
            profile.name = "ArcadeCombat_URP";
            var settings = new SerializedObject(profile);
            var distance = settings.FindProperty("m_ShadowDistance");
            var cascades = settings.FindProperty("m_ShadowCascadeCount");
            var splits = settings.FindProperty("m_Cascade4Split");
            if (distance == null || cascades == null || splits == null)
                throw new InvalidOperationException("URP shadow serialization changed; inspect the copied asset manually.");
            distance.floatValue = ShadowDistance;
            cascades.intValue = 4;
            splits.vector3Value = new Vector3(0.13f, 0.32f, 0.62f);
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            // Only the authored E1 scene overrides this one prefab instance.
            // The prefab asset and both combat lab scenes retain the old profile.
            rigData.Update();
            rigProfile.objectReferenceValue = profile;
            rigData.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(rig);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Failed to save the E1 lighting override.");

            Selection.activeObject = profile;
            return "E1d installed: " + ProfilePath +
                " | 600-unit shadows, 4 cascades, 4096 atlas (unchanged). " +
                "Only ArcadeDesertE1 uses this profile. Play and check Y78/110/150.";
        }
    }
}
#endif
