using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    // All campaign maps share one edit-mode refresh path. Never move scene
    // geometry, save an authored scene silently or regenerate a mission.
    [InitializeOnLoad]
    internal static class MissionAutoMap
    {
        private const string MapFolder = "Assets/_Project/Generated/MissionMaps/";
        static MissionAutoMap() { EditorApplication.playModeStateChanged += OnPlayMode; }

        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode) return;
            try
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    if (!scene.isLoaded || string.IsNullOrEmpty(scene.path)) continue;
                    foreach (var view in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TacticalMapView>()))
                    {
                        if (!view.isActiveAndEnabled || !view.AutoBakeMap) continue;
                        if (!TryResolve(scene, out var ground, out var runway, out var altitude)) continue;
                        Refresh(view, ground, runway, altitude, false);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                Debug.LogError("Mission map refresh failed. Play cancelled; fix map inputs or disable Auto Bake Map on TacticalMapView.");
                EditorApplication.isPlaying = false;
            }
        }

        internal static void Refresh(TacticalMapView view, bool force)
        {
            if (view == null || !TryResolve(view.gameObject.scene, out var ground, out var runway, out var altitude))
                throw new InvalidOperationException("Map refresh requires a supported mission and TacticalMapView.");
            Refresh(view, ground, runway, altitude, force);
        }

        // M03 has explicit references. The two older missions use the same
        // discovery conventions as the existing manual baker; no pose writes.
        internal static bool TryResolve(Scene scene, out Renderer ground, out Transform runway, out float altitude)
        {
            ground = null; runway = null; altitude = 78f;
            var roots = scene.GetRootGameObjects();
            var m03 = roots.SelectMany(r => r.GetComponentsInChildren<Mission03Flight>(true)).SingleOrDefault();
            if (m03 != null)
            {
                ground = m03.MapGround; runway = m03.MapRunway; altitude = m03.CruiseAltitude;
                return true;
            }
            var m01 = roots.SelectMany(r => r.GetComponentsInChildren<Mission01LaunchSequence>(true)).SingleOrDefault();
            if (m01 != null)
            {
                ground = roots.SingleOrDefault(r => r.name == "Mission01 / Faceted surface")?.GetComponent<Renderer>();
                var baseRoot = roots.SingleOrDefault(r => r.name == "Mission01 / Airfield");
                runway = baseRoot != null ? baseRoot.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(t => t.name == "Runway and apron") ?? baseRoot.transform : null;
                altitude = new SerializedObject(m01).FindProperty("cruiseAltitude").floatValue;
                return true;
            }
            var m02 = roots.SelectMany(r => r.GetComponentsInChildren<Mission02RouteTrial>(true)).SingleOrDefault();
            if (m02 == null) return false;
            ground = roots.SingleOrDefault(r => r.name == "M02 / visible sand")?.GetComponent<Renderer>();
            // This is the authored player/runway pose, before any runtime movement.
            runway = roots.SelectMany(r => r.GetComponentsInChildren<JetAssembly>(true))
                .FirstOrDefault(jet => jet.GetComponent<MissionCheats>() != null)?.transform;
            return true;
        }

        private static void Refresh(TacticalMapView view, Renderer ground, Transform runway, float altitude, bool force)
        {
            if (ground == null || runway == null)
                throw new InvalidOperationException("Map refresh: missing ground or friendly runway in " + view.gameObject.scene.name);
            var scene = view.gameObject.scene;
            string guid = AssetDatabase.AssetPathToGUID(scene.path);
            if (string.IsNullOrEmpty(guid)) throw new InvalidOperationException("Save the mission before map bake.");
            string path = $"{MapFolder}{scene.name}_{guid.Substring(0, 8)}.asset";
            var map = AssetDatabase.LoadAssetAtPath<MissionMapData>(path);
            // Dirty scenes are baked from their current transforms, but do not
            // receive a clean disk fingerprint until the next saved Play.
            string fingerprint = scene.isDirty ? "" : Fingerprint(scene.path);
            if (force || string.IsNullOrEmpty(fingerprint) || map == null || map.Geography == null ||
                map.SourceSceneGuid != guid || map.SourceFingerprint != fingerprint)
            {
                map = MissionMapBaker.BakeForRouteTrial(scene, ground, runway, altitude);
                map.SetSourceFingerprint(fingerprint);
                EditorUtility.SetDirty(map); AssetDatabase.SaveAssets();
            }
            if (view.GeographyData != map)
            {
                view.SetGeographyForAuthoring(map);
                EditorUtility.SetDirty(view);
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        private static string Fingerprint(string scenePath)
        {
            var input = new StringBuilder("mission-map-v2\n");
            input.Append(Hash128.Compute(File.ReadAllText(scenePath))).Append('\n');
            // Generated map assets must not invalidate their own cache.
            foreach (string path in AssetDatabase.GetDependencies(scenePath, true).OrderBy(p => p, StringComparer.Ordinal))
            {
                if (path == scenePath || path.StartsWith(MapFolder, StringComparison.Ordinal)) continue;
                input.Append(path).Append(':').Append(AssetDatabase.GetAssetDependencyHash(path)).Append('\n');
            }
            return Hash128.Compute(input.ToString()).ToString();
        }
    }
}
