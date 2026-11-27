using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    // Read-only guard against accidentally copying settings into a new mission.
    internal static class SharedMissionAudit
    {
        private const string Root = "Assets/_Project/";
        [MenuItem("Air Combat/Missions/Audit Shared Settings (open scene)")]
        private static void Audit()
        {
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            T[] Find<T>() where T : Component => roots.SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
            int problems = 0;
            void Check(bool ok, string text)
            {
                if (ok) return;
                problems++; Debug.LogWarning(scene.name + ": " + text);
            }
            bool sandbox = Find<CombatSandboxEncounter>().Length != 0;
            var light = Find<CombatLightingRig>();
            var cameras = Find<CombatCameraRig>();
            var hud = Find<CombatHud>();
            var session = Find<FlightSession>();
            bool IsShared(Component c, string path) => c != null &&
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(c.gameObject) == Root + path;
            Check(light.Length == 1 && IsShared(light.FirstOrDefault(), "Prefabs/Environment/CombatLightingRig.prefab"), "Expected one shared lighting prefab.");
            Check(cameras.Length == 1 && IsShared(cameras.FirstOrDefault(), "Prefabs/Camera/ArcadeCameraRig.prefab"), "Expected one shared camera prefab.");
            Check(hud.Length == 1 && IsShared(hud.FirstOrDefault(), "Prefabs/UI/CombatHUD.prefab"), "Expected one shared combat HUD prefab.");
            Check(session.Length == 1, "Expected one FlightSession.");
            if (light.Length == 1)
            {
                var so = new SerializedObject(light[0]);
                foreach (string name in new[] { "pipeline", "azimuth", "elevationOffset", "ambientMultiplier", "timeOfDay" })
                    Check(!so.FindProperty(name).prefabOverride, "Lighting override: " + name);
            }
            if (!sandbox)
            {
                var audio = Find<Mission01EngineAudio>();
                var maps = Find<TacticalMapView>();
                var player = Find<MissionCheats>();
                Check(player.Length == 1 && IsShared(player.FirstOrDefault(), "Prefabs/Aircraft/PlayerLynx_Combat.prefab"), "Expected Lynx campaign prefab with MissionCheats.");
                if (player.Length == 1)
                {
                    var flight = player[0].GetComponent<FlightController>();
                    var boost = player[0].GetComponent<Afterburner>();
                    Check(flight != null && boost != null, "Missing common flight or boost controller.");
                    if (flight != null)
                    {
                        var so = new SerializedObject(flight);
                        foreach (string name in new[] { "minSpeed", "cruiseSpeed", "maxSpeed", "turnRate", "precisionTurnMultiplier", "maxBank", "bankRate", "tightSpeed", "boostTurnMultiplier", "tightTurnMultiplier", "maneuverResponse" })
                            Check(!so.FindProperty(name).prefabOverride, "Player flight override: " + name);
                    }
                    if (boost != null)
                    {
                        var so = new SerializedObject(boost);
                        foreach (string name in new[] { "boostSpeed", "boostAcceleration", "capacitySeconds", "rechargeSeconds", "rechargeDelay" })
                            Check(!so.FindProperty(name).prefabOverride, "Player boost override: " + name);
                    }
                }
                Check(audio.Length == 1 && AssetDatabase.GetAssetPath(new SerializedObject(audio[0]).FindProperty("profile").objectReferenceValue) == Root + "Settings/Lynx_EngineAudio.asset", "Expected shared Lynx engine profile.");
                Check(maps.Length == 1, "Expected one TacticalMapView.");
                if (maps.Length == 1)
                {
                    Check(maps[0].AutoBakeMap, "Auto Bake Map disabled (may be intentional).");
                    Check(MissionAutoMap.TryResolve(scene, out var ground, out var runway, out _) && ground != null && runway != null, "Unresolved map inputs.");
                }
                if (cameras.Length == 1)
                {
                    var follow = cameras[0].GetComponent<ArcadeCameraFollow>();
                    if (follow == null) Check(false, "Missing camera follow.");
                    else
                    {
                        var so = new SerializedObject(follow);
                        foreach (string name in new[] { "pitch", "yaw", "orthographicSize", "cruiseLead", "boostExtraLead", "leadTransitionSeconds", "aircraftViewportMargin", "positionSmoothTime" })
                            Check(!so.FindProperty(name).prefabOverride, "Camera override: " + name);
                    }
                }
            }
            foreach (var m03 in Find<Mission03Flight>())
                Check(m03.MapRunway != null && Vector3.Angle(m03.MapRunway.forward, Quaternion.Euler(0f,Mission03FriendlyAirfieldAuthoring.RunwayHeading,0f) * Vector3.forward) < .1f,
                    "M03 friendly runway is not northeast (45 degrees). No automatic rotation performed.");
            Debug.Log($"Shared mission audit: {scene.name}, {problems} warning(s). Read-only; scene unchanged.");
        }
    }
}
