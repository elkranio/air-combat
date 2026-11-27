using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    // Opt-in M03 only. Bake in edit mode before Play, using the existing
    // geography pipeline; never scan/bake per frame or in a player build.
    [InitializeOnLoad]
    internal static class Mission03AutoMap
    {
        private const string MapFolder = "Assets/_Project/Generated/MissionMaps/";
        static Mission03AutoMap() { EditorApplication.playModeStateChanged += OnPlayMode; }

        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode) return;
            try
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    if (!scene.isLoaded || string.IsNullOrEmpty(scene.path)) continue;
                    foreach (var mission in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Mission03Flight>()))
                    {
                        if (!mission.isActiveAndEnabled || !mission.AutoBakeMap) continue;
                        var view = mission.GetComponent<TacticalMapView>();
                        Refresh(mission, view, false);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                Debug.LogError("M03 map refresh failed. Play cancelled; fix map inputs or disable Auto Bake Map for this test.");
                EditorApplication.isPlaying = false;
            }
        }

        internal static void Refresh(Mission03Flight mission, TacticalMapView view, bool force)
        {
            if (mission == null || view == null || mission.MapGround == null || mission.MapRunway == null)
                throw new InvalidOperationException("M03 map: missing ground, runway or TacticalMapView.");
            var scene = mission.gameObject.scene;
            string guid = AssetDatabase.AssetPathToGUID(scene.path);
            if (string.IsNullOrEmpty(guid)) throw new InvalidOperationException("Save M03 before map bake.");
            string path = $"{MapFolder}{scene.name}_{guid.Substring(0, 8)}.asset";
            var map = AssetDatabase.LoadAssetAtPath<MissionMapData>(path);
            // Dirty scene: bake current transforms, but never record a clean disk
            // signature or silently save the scene. Next saved Play bakes once.
            string fingerprint = scene.isDirty ? "" : Fingerprint(scene.path);
            if (force || string.IsNullOrEmpty(fingerprint) || map == null || map.Geography == null ||
                map.SourceSceneGuid != guid || map.SourceFingerprint != fingerprint)
            {
                map = MissionMapBaker.BakeForRouteTrial(scene, mission.MapGround, mission.MapRunway);
                map.SetSourceFingerprint(fingerprint);
                EditorUtility.SetDirty(map); AssetDatabase.SaveAssets();
            }
            if (view.GeographyData != map)
            {
                view.SetGeographyForAuthoring(map);
                EditorUtility.SetDirty(view);
                // A missing reference is repaired in memory, not saved automatically.
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        private static string Fingerprint(string scenePath)
        {
            // Exclude map output itself: otherwise saving the generated texture
            // would invalidate its own cache on every Play. Include all other
            // saved dependencies conservatively (mesh, prefab, material, script).
            var input = new StringBuilder("M03-map-v1\n");
            input.Append(Hash128.Compute(File.ReadAllText(scenePath))).Append('\n');
            foreach (string path in AssetDatabase.GetDependencies(scenePath, true).OrderBy(p => p, StringComparer.Ordinal))
            {
                if (path == scenePath || path.StartsWith(MapFolder, StringComparison.Ordinal)) continue;
                input.Append(path).Append(':').Append(AssetDatabase.GetAssetDependencyHash(path)).Append('\n');
            }
            return Hash128.Compute(input.ToString()).ToString();
        }
    }
}
