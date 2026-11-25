using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    internal static class Mission01ReadySetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Mission01_FirstSortie.unity";
        private const string LynxPath = "Assets/_Project/Prefabs/Aircraft/PlayerLynx_Combat.prefab";
        private const string UndoName = "Install Mission 01 ready state";

        [MenuItem("Air Combat/Missions/Install Mission 01 Ready State")]
        private static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Debug.LogWarning("MISSION-01E: open Mission01_FirstSortie first.");
                return;
            }
            try
            {
                var session = scene.GetRootGameObjects().Select(g => g.GetComponent<E1MapFlightSession>())
                    .Single(s => s != null);
                if (session.GetComponent<Mission01LaunchSequence>() != null)
                {
                    Debug.Log("MISSION-01E: ready state already installed.");
                    return;
                }
                var data = new SerializedObject(session);
                var player = data.FindProperty("player").objectReferenceValue as JetAssembly;
                if (player == null || !player.IsConfigured || player.transform.parent != null ||
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(player.gameObject) != LynxPath)
                    throw new InvalidOperationException("Expected the installed player Lynx preview.");
                var flight = player.GetComponent<FlightController>();
                var trigger = player.GetComponent<PlayerWeaponTrigger>();
                if (flight == null || trigger == null || !flight.enabled || trigger.enabled)
                    throw new InvalidOperationException("Expected free flight with weapons disabled.");
                var anchor = scene.GetRootGameObjects().Single(g => g.name == "Mission01 / Airfield");
                var start = anchor.transform.Find("Airfield B - editable layout/Takeoff start - visual placeholder");
                var preview = start != null ? start.Find("Lynx - remove when player is integrated") : null;
                if (preview == null || !preview.gameObject.activeInHierarchy)
                    throw new InvalidOperationException("Missing active runway Lynx placeholder.");
                if (player.VisualRoot.localPosition.sqrMagnitude > .0001f ||
                    Quaternion.Angle(player.VisualRoot.localRotation, Quaternion.identity) > .01f ||
                    (player.transform.lossyScale - preview.lossyScale).sqrMagnitude > .0001f)
                    throw new InvalidOperationException("Unexpected Lynx visual origin or scale.");

                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(UndoName);
                try
                {
                    Undo.RecordObjects(new UnityEngine.Object[] { player.gameObject, player.transform,
                        flight, preview.gameObject, start.gameObject }, UndoName);
                    // Use the existing approved parked pose, including its surface clearance.
                    player.transform.SetPositionAndRotation(preview.position, preview.rotation);
                    player.gameObject.name = "Mission01 / Player Lynx";
                    flight.enabled = false;
                    preview.gameObject.SetActive(false);
                    start.gameObject.name = "Takeoff start";
                    var launch = Undo.AddComponent<Mission01LaunchSequence>(session.gameObject);
                    launch.ConfigureForAuthoring(flight, trigger);
                    EditorUtility.SetDirty(launch);
                    foreach (UnityEngine.Object obj in new UnityEngine.Object[] { player.gameObject,
                        player.transform, flight, preview.gameObject })
                        PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
                    Undo.CollapseUndoOperations(group);
                    EditorSceneManager.MarkSceneDirty(scene);
                    Selection.activeGameObject = player.gameObject;
                    Debug.Log("MISSION-01E: runway ready state installed. Save scene and press Play.");
                }
                catch
                {
                    Undo.RevertAllDownToGroup(group);
                    throw;
                }
            }
            catch (Exception ex) { Debug.LogError("MISSION-01E: " + ex); }
        }
    }
}
