using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    // One-shot scene authoring; the existing HUD and aircraft prefabs stay unchanged.
    internal static class Mission01HudSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Mission01_FirstSortie.unity";
        private const string HudPath = "Assets/_Project/Prefabs/UI/CombatHUD.prefab";
        private const string UndoName = "Install Mission 01 Combat HUD";

        [MenuItem("Air Combat/Missions/Install Mission 01 Combat HUD")]
        private static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Debug.LogWarning("MISSION-01G: open Mission01_FirstSortie first.");
                return;
            }
            try
            {
                var roots = scene.GetRootGameObjects();
                var session = roots.Select(r => r.GetComponent<E1MapFlightSession>()).Single(s => s != null);
                var launch = session.GetComponent<Mission01LaunchSequence>();
                var sessionData = new SerializedObject(session);
                var player = sessionData.FindProperty("player").objectReferenceValue as JetAssembly;
                var rig = sessionData.FindProperty("cameraRig").objectReferenceValue as CombatCameraRig;
                var trigger = player != null ? player.GetComponent<PlayerWeaponTrigger>() : null;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
                if (launch == null || player == null || rig == null || trigger == null ||
                    player.GetComponent<AircraftRadar>() == null || player.GetComponent<PlayerHitPoints>() == null ||
                    player.GetComponent<Afterburner>() == null || prefab == null || prefab.GetComponent<CombatHud>() == null ||
                    session.GetComponent<Air01Encounter>() != null)
                    throw new InvalidOperationException("Expected the saved launch sequence, combat Lynx, camera and HUD prefab without a test encounter.");
                var primary = new SerializedObject(trigger).FindProperty("weaponComponent").objectReferenceValue as AutocannonWeapon;
                if (primary == null || !primary.enabled || !primary.gameObject.activeInHierarchy)
                    throw new InvalidOperationException("Expected the authored active autocannon as the primary weapon.");
                var existing = roots.SelectMany(r => r.GetComponentsInChildren<CombatHud>(true)).ToArray();
                if (existing.Length != 0)
                {
                    if (existing.Length == 1 && sessionData.FindProperty("combatHud").objectReferenceValue == existing[0] &&
                        new SerializedObject(launch).FindProperty("missionHud").objectReferenceValue == existing[0])
                    {
                        Debug.Log("MISSION-01G: HUD already installed.");
                        return;
                    }
                    throw new InvalidOperationException("Unexpected existing HUD or binding; nothing changed.");
                }
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(UndoName);
                try
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    if (instance == null) throw new InvalidOperationException("HUD instantiation failed.");
                    Undo.RegisterCreatedObjectUndo(instance, UndoName);
                    var hud = instance.GetComponent<CombatHud>();
                    Undo.RecordObjects(new UnityEngine.Object[] { session, launch }, UndoName);
                    session.ConfigureHud(hud, Array.Empty<RadarAirContact>());
                    launch.ConfigureHudForAuthoring(hud);
                    EditorUtility.SetDirty(session);
                    EditorUtility.SetDirty(launch);
                    Undo.CollapseUndoOperations(group);
                    EditorSceneManager.MarkSceneDirty(scene);
                    Selection.activeGameObject = instance;
                    Debug.Log("MISSION-01G: HUD installed. Save scene; Play, W, check cannon and empty RMB after takeoff.");
                }
                catch
                {
                    Undo.RevertAllDownToGroup(group);
                    throw;
                }
            }
            catch (Exception ex) { Debug.LogError("MISSION-01G: " + ex); }
        }
    }
}
