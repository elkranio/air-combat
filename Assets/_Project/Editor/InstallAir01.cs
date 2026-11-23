#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    // Author once; the result is a regular prefab and scene reference.
    public static class InstallAir01
    {
        private const string MainPath = "Assets/_Project/Scenes/ArcadeDesertE1.unity";
        private const string FighterPath = "Assets/_Project/Prefabs/Aircraft/EF01_Tiger_Fighter.prefab";
        private const string ChassisPath = "Assets/_Project/Prefabs/Aircraft/EF01_Tiger_D1_Chassis.prefab";
        private const string GunPath = "Assets/_Project/Prefabs/Weapons/AC01_Autocannon.prefab";

        [MenuItem("Air Combat/Combat/AIR-01 - Install Tiger Flight Test")]
        private static void Install()
        {
            try { Debug.Log(InstallScene()); }
            catch (Exception e) { Debug.LogError("AIR-01: " + e); }
        }

        private static string InstallScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.isLoaded || scene.path != MainPath || scene.isDirty)
                throw new InvalidOperationException("Open and save ArcadeDesertE1, outside Play Mode.");
            E1MapFlightSession session = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                session = root.GetComponentInChildren<E1MapFlightSession>(true);
                if (session != null) break;
            }
            if (session == null) throw new InvalidOperationException("Main-map session is missing.");
            if (session.GetComponent<Air01Encounter>() != null) return "AIR-01 already installed; skipped.";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(FighterPath) != null)
                throw new InvalidOperationException("Fighter prefab already exists; refusing to overwrite it.");

            var player = new SerializedObject(session).FindProperty("player").objectReferenceValue as JetAssembly;
            var chassisPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChassisPath);
            var gunPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GunPath);
            if (player == null || !player.IsConfigured || chassisPrefab == null || gunPrefab == null ||
                gunPrefab.GetComponent<AutocannonWeapon>() == null)
                throw new InvalidOperationException("Player, Tiger chassis or AC-01 module is missing.");
            var playerFlight = player.GetComponent<FlightController>();
            var playerBoost = player.GetComponent<Afterburner>();
            var playerBreak = player.GetComponent<BreakTurn>();
            var playerVfx = player.GetComponentInChildren<AfterburnerVfx>(true);
            if (playerFlight == null || playerBoost == null || playerBreak == null || playerVfx == null)
                throw new InvalidOperationException("Player flight/boost setup is incomplete.");

            var fighter = new GameObject("EF01_Tiger_Fighter");
            try
            {
                var visual = new GameObject("JetVisual").transform;
                visual.SetParent(fighter.transform, false);
                var chassisObject = (GameObject)PrefabUtility.InstantiatePrefab(chassisPrefab, visual);
                var chassis = chassisObject.GetComponent<JetChassis>();
                if (chassis == null || !chassis.HasRequiredSockets)
                    throw new InvalidOperationException("Tiger sockets are incomplete.");
                var assembly = fighter.AddComponent<JetAssembly>();
                assembly.SetAuthoringReferences(visual, chassis);
                var boost = fighter.AddComponent<Afterburner>();
                EditorUtility.CopySerialized(playerBoost, boost);
                var breakTurn = fighter.AddComponent<BreakTurn>();
                EditorUtility.CopySerialized(playerBreak, breakTurn);
                var vfx = visual.gameObject.AddComponent<AfterburnerVfx>();
                EditorUtility.CopySerialized(playerVfx, vfx);
                var vfxData = new SerializedObject(vfx);
                vfxData.FindProperty("afterburner").objectReferenceValue = boost;
                vfxData.FindProperty("chassis").objectReferenceValue = chassis;
                vfxData.FindProperty("playIgnitionSound").boolValue = false;
                vfxData.ApplyModifiedPropertiesWithoutUndo();
                var flight = fighter.AddComponent<CombatAircraftFlight>();
                var flightData = new SerializedObject(flight);
                var source = new SerializedObject(playerFlight);
                foreach (string name in new[] { "cruiseSpeed", "acceleration", "deceleration", "turnRate", "maxBank", "bankRate" })
                    flightData.FindProperty(name).floatValue = source.FindProperty(name).floatValue;
                flightData.FindProperty("aircraft").objectReferenceValue = assembly;
                flightData.FindProperty("boost").objectReferenceValue = boost;
                flightData.FindProperty("breakTurn").objectReferenceValue = breakTurn;
                flightData.ApplyModifiedPropertiesWithoutUndo();
                fighter.AddComponent<AircraftTeam>().SetSide(AircraftSide.Hostile);
                fighter.AddComponent<PlayerHitPoints>();
                fighter.AddComponent<RadarAirContact>();
                var gun = (GameObject)PrefabUtility.InstantiatePrefab(gunPrefab, fighter.transform);
                gun.transform.localPosition = Vector3.zero;
                gun.transform.localRotation = Quaternion.identity;
                gun.transform.localScale = Vector3.one;
                PrefabUtility.SaveAsPrefabAsset(fighter, FighterPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(fighter); }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FighterPath);
            if (prefab == null) throw new InvalidOperationException("Fighter prefab creation failed.");
            var encounter = Undo.AddComponent<Air01Encounter>(session.gameObject);
            var data = new SerializedObject(encounter);
            data.FindProperty("enemyPrefab").objectReferenceValue = prefab;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, scene.path))
                throw new InvalidOperationException("Could not save E1 scene.");
            AssetDatabase.SaveAssets();
            return "AIR-01 installed. Play: random Tiger fly-through; R: new encounter. No enemy firing yet.";
        }
    }
}
#endif
