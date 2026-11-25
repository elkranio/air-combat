using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    internal static class Mission01OverlordAuthoring
    {
        private const string Art = "Assets/_Project/Art/Aircraft/Dominion/AE01_Overlord/AE01_Overlord.prefab";
        private const string VisualName = "AE01 Overlord visual";
        private const string PreviewName = "AE01 / Overlord visual trial (remove before save)";

        [MenuItem("Air Combat/Missions/Install Mission01 Overlord v0")]
        private static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            int undo = -1;
            try
            {
                var scene = SceneManager.GetActiveScene();
                if (!scene.isLoaded || scene.name != "Mission01_FirstSortie") throw new InvalidOperationException("Open Mission01_FirstSortie first.");
                var roots = scene.GetRootGameObjects();
                var launch = roots.SelectMany(r => r.GetComponentsInChildren<Mission01LaunchSequence>(true)).Single();
                var recon = launch.GetComponent<Mission01ReconEncounter>();
                var map = roots.SelectMany(r => r.GetComponentsInChildren<TacticalMapView>(true)).Single();
                var marker = new SerializedObject(map).FindProperty("optionalTarget").objectReferenceValue as Transform;
                var config = new SerializedObject(launch);
                var player = config.FindProperty("flight").objectReferenceValue as FlightController;
                float altitude = config.FindProperty("cruiseAltitude").floatValue;
                var floor = roots.Single(r => r.name == "Mission01 / Faceted surface").GetComponent<Renderer>();
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Art);
                if (marker == null || player == null || recon == null || floor == null || prefab == null)
                    throw new InvalidOperationException("Missing optional marker, recon encounter, player, ground or AE01 prefab.");
                if ((marker.lossyScale - Vector3.one).sqrMagnitude > .001f)
                    throw new InvalidOperationException("Optional marker must have unit scale.");
                if (marker.GetComponents<DamageableTarget>().Any(t => !(t is OverlordTarget)))
                    throw new InvalidOperationException("Optional marker already has a different target.");
                undo = Undo.GetCurrentGroup(); Undo.IncrementCurrentGroup(); undo = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Install Mission01 Overlord");
                foreach (var root in roots) if (root.name == PreviewName) Undo.DestroyObjectImmediate(root);
                var target = marker.GetComponent<OverlordTarget>();
                // Only seed new encounters; repeat installation keeps authored tuning and position.
                if (target == null)
                {
                    Undo.RecordObject(marker, "Place Overlord patrol");
                    marker.position = new Vector3(1610f, altitude, 100f);
                    marker.rotation = Quaternion.identity;
                }
                var visual = marker.Find(VisualName);
                if (visual == null)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, marker);
                    Undo.RegisterCreatedObjectUndo(go, "Create Overlord model"); go.name = VisualName; visual = go.transform;
                }
                Undo.RecordObject(visual, "Align Overlord model");
                visual.localPosition = Vector3.zero; visual.localRotation = Quaternion.identity; visual.localScale = Vector3.one;
                PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
                var boxes = marker.GetComponents<BoxCollider>().ToList();
                while (boxes.Count < 4) boxes.Add(Undo.AddComponent<BoxCollider>(marker.gameObject));
                if (boxes.Count != 4) throw new InvalidOperationException("Expected four Overlord hit zones.");
                var centers = new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, -2.5f), new Vector3(-5.15f,.28f,-5f), new Vector3(5.15f,.28f,-5f) };
                var sizes = new[] { new Vector3(3.6f, 5f, 23f), new Vector3(28f, 4f, 5f), new Vector3(3.6f, 4f, 13f), new Vector3(3.6f, 4f, 13f) };
                for (int i=0; i<boxes.Count; i++)
                {
                    Undo.RecordObject(boxes[i], "Configure Overlord hit zone");
                    boxes[i].center = centers[i]; boxes[i].size = sizes[i]; boxes[i].enabled = true; boxes[i].isTrigger = false;
                    EditorUtility.SetDirty(boxes[i]);
                }
                if (target == null) target = Undo.AddComponent<OverlordTarget>(marker.gameObject);
                Undo.RecordObject(target, "Bind Overlord encounter");
                target.ConfigureForAuthoring(launch, recon, player, visual, floor.bounds.center.y);
                var team = marker.GetComponent<AircraftTeam>();
                if (team == null) team = Undo.AddComponent<AircraftTeam>(marker.gameObject);
                Undo.RecordObject(team, "Set Overlord faction"); team.SetSide(AircraftSide.Hostile);
                EditorUtility.SetDirty(target); EditorUtility.SetDirty(team);
                EditorSceneManager.MarkSceneDirty(scene); Undo.CollapseUndoOperations(undo);
                Selection.activeGameObject = marker.gameObject;
                Debug.Log("AE01: live Overlord installed on optional map marker. Save scene after review. Debug Near Player is an Editor-only combat trial.");
            }
            catch (Exception ex)
            {
                if (undo >= 0) Undo.RevertAllDownToGroup(undo);
                Debug.LogError("AE01: " + ex);
            }
        }
    }
}
