using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    // Update the existing scene without rebuilding the foundation.
    internal static class Mission03FriendlyAirfieldAuthoring
    {
        [MenuItem("Air Combat/Missions/M03 - Rotate Friendly Airfield")]
        private static void RotateFriendlyAirfield()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode first.");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/_Project/Scenes/Mission03_SparrowInterception.unity")
                throw new InvalidOperationException("Open Mission03_SparrowInterception first.");

            var roots = scene.GetRootGameObjects();
            var mission = roots.SelectMany(r => r.GetComponentsInChildren<Mission03Flight>(true)).Single();
            var map = mission.GetComponent<TacticalMapView>();
            var airfield = roots.Single(r => r.name == "M03 / Trident airfield").transform;
            var player = roots.Single(r => r.name == "PlayerLynx_Combat").transform;
            var camera = roots.SelectMany(r => r.GetComponentsInChildren<CombatCameraRig>(true)).Single().transform;
            var runway = mission.MapRunway;
            var pavement = airfield.GetComponentsInChildren<MeshFilter>(true).Single(f =>
                f.sharedMesh != null && f.sharedMesh.name == "Mission01_HangarPass").transform;
            if (map == null || runway == null || !runway.IsChildOf(airfield) || mission.MapGround == null)
                throw new InvalidOperationException("M03: missing map, ground or friendly runway reference.");

            // Rotate relative to the current heading, keeping the runway center fixed.
            // Running the command twice produces the same pose.
            var anchor = pavement.TransformPoint(new Vector3(125f, 0f, 0f));
            float yaw = Mathf.Atan2(pavement.forward.x, pavement.forward.z) * Mathf.Rad2Deg;
            Undo.RecordObjects(new UnityEngine.Object[] { airfield, runway, player, camera }, "Rotate M03 friendly airfield");
            airfield.rotation = Quaternion.AngleAxis(45f - yaw, Vector3.up) * airfield.rotation;
            airfield.position += anchor - pavement.TransformPoint(new Vector3(125f, 0f, 0f));
            runway.SetPositionAndRotation(anchor, Quaternion.LookRotation(pavement.forward, Vector3.up));
            Vector3 spawn = pavement.TransformPoint(new Vector3(125f, 0f, -92f));
            spawn.y = airfield.position.y + .8857401f;
            player.SetPositionAndRotation(spawn, runway.rotation);
            camera.position = spawn + new Vector3(0f, 166f, -190f);
            foreach (var t in new[] { airfield, runway, player, camera })
                PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            EditorSceneManager.MarkSceneDirty(scene);
            Mission03AutoMap.Refresh(mission, map, true);
            Debug.Log("M03 friendly airfield: northeast (45 degrees). Save the scene; auto-map will reuse or refresh on Play.");
        }
    }
}
