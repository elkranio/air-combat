using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    internal static class CA01FlightPreviewAuthoring
    {
        private const string Mission = "Assets/_Project/Scenes/Mission02_ConvoyInterdiction.unity";
        private const string Root = "M02 / CA01 flight preview";
        private const string Carrier = "Assets/_Project/Prefabs/Aircraft/CA01_Carryall.prefab";
        private const string Cargo = "Assets/_Project/Prefabs/Environment/CG01_SpecialCargo.prefab";

        [MenuItem("Air Combat/Missions/M02 - Install CA01 Flight Preview")]
        private static void Install()
        {
            if (!TryScene(out var scene)) return;
            GameObject root = null;
            int undo = -1;
            try
            {
                var roots = scene.GetRootGameObjects();
                if (roots.Any(r => r.name == Root || r.name == "M02 / CA01 comparison preview"))
                    throw new InvalidOperationException("Remove the existing CA01 preview first.");
                var trial = roots.SelectMany(r => r.GetComponentsInChildren<Mission02RouteTrial>(true)).Single();
                var session = trial.GetComponent<FlightSession>();
                if (session == null) throw new InvalidOperationException("Missing FlightSession.");
                var player = new SerializedObject(session).FindProperty("player").objectReferenceValue as JetAssembly;
                var flight = player != null ? player.GetComponent<FlightController>() : null;
                var carrier = AssetDatabase.LoadAssetAtPath<GameObject>(Carrier);
                var cargo = AssetDatabase.LoadAssetAtPath<GameObject>(Cargo);
                if (flight == null || carrier == null || cargo == null) throw new InvalidOperationException("Missing player or CA01/CG01 prefab.");
                var mount = carrier.transform.Find("CargoMount");
                if (mount == null || (mount.localPosition - new Vector3(0f,-5f,0f)).sqrMagnitude > .000001f)
                    throw new InvalidOperationException("Unexpected CA01 mount.");
                for (int i = 1; i <= 4; i++)
                {
                    var clamp = carrier.transform.Find($"ClampSockets/Clamp_{i:00}");
                    var grab = cargo.transform.Find($"CaptureSockets/Grab_{i:00}");
                    if (clamp == null || grab == null || (clamp.localPosition - (grab.localPosition + mount.localPosition)).sqrMagnitude > .000001f)
                        throw new InvalidOperationException("CA01/CG01 socket mismatch.");
                }
                var serialized = new SerializedObject(trial);
                var previewProperty = serialized.FindProperty("carryallFlightPreview");
                if (previewProperty == null || previewProperty.objectReferenceValue != null)
                    throw new InvalidOperationException("Unexpected flight preview reference.");
                Vector3 forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
                if (forward.sqrMagnitude < .5f) throw new InvalidOperationException("Invalid runway heading.");
                Vector3 right = Vector3.Cross(Vector3.up, forward);
                // Same reference position as M02's existing Start Airborne switch.
                Vector3 center = player.transform.position + forward * (flight.CruiseSpeed * 3.6f + 30f) + right * 24f;
                center.y = 78f;
                var bounds = trial.OperationBounds;
                if (!bounds.Contains(new Vector2(center.x-45f,center.z-45f)) || !bounds.Contains(new Vector2(center.x+45f,center.z+45f)))
                    throw new InvalidOperationException("Flight preview circle would leave operation bounds.");
                Undo.IncrementCurrentGroup(); undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("CA01 flight preview");
                root = new GameObject(Root);
                Undo.RegisterCreatedObjectUndo(root,"CA01 flight preview root");
                root.transform.SetPositionAndRotation(center,Quaternion.LookRotation(forward,Vector3.up));
                var instance = PrefabUtility.InstantiatePrefab(carrier,root.transform) as GameObject;
                if (instance == null) throw new InvalidOperationException("Could not instantiate CA01.");
                Undo.RegisterCreatedObjectUndo(instance,"CA01 preview carrier");
                instance.transform.localPosition = new Vector3(0f,0f,-32f);
                instance.transform.localRotation = Quaternion.Euler(0f,90f,-5f);
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                Undo.AddComponent<CarryallRotorVisual>(instance);
                var attached = PrefabUtility.InstantiatePrefab(cargo,instance.transform.Find("CargoMount")) as GameObject;
                if (attached == null) throw new InvalidOperationException("Could not instantiate CG01.");
                Undo.RegisterCreatedObjectUndo(attached,"CA01 test cargo");
                attached.transform.localPosition = Vector3.zero; attached.transform.localRotation = Quaternion.identity;
                PrefabUtility.RecordPrefabInstancePropertyModifications(attached.transform);
                var preview = Undo.AddComponent<CarryallFlightPreview>(root);
                preview.Configure(instance.transform,attached); EditorUtility.SetDirty(preview);
                previewProperty.objectReferenceValue = preview; serialized.ApplyModifiedProperties();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Scene save failed.");
                Undo.CollapseUndoOperations(undo);
                Selection.activeGameObject = root;
                Debug.Log("CA01 flight preview installed near airborne start. Optional Start Airborne on M02; F on this root in Scene view. R resets preview.",root);
            }
            catch (Exception ex)
            {
                if (undo >= 0) Undo.RevertAllDownToGroup(undo);
                Debug.LogError("CA01 flight preview: " + ex);
            }
        }

        [MenuItem("Air Combat/Missions/M02 - Remove CA01 Flight Preview")]
        private static void Remove()
        {
            if (!TryScene(out var scene)) return;
            var root = scene.GetRootGameObjects().SingleOrDefault(r => r.name == Root);
            if (root == null) return;
            var trial = scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Mission02RouteTrial>(true)).Single();
            var serialized = new SerializedObject(trial);
            var property = serialized.FindProperty("carryallFlightPreview");
            if (property == null || property.objectReferenceValue != root.GetComponent<CarryallFlightPreview>())
            { Debug.LogError("CA01 flight preview reference mismatch. Nothing removed."); return; }
            property.objectReferenceValue = null; serialized.ApplyModifiedProperties();
            Undo.DestroyObjectImmediate(root);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) Debug.LogError("CA01 flight preview: scene save failed.");
        }

        private static bool TryScene(out Scene scene)
        {
            scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode) return false;
            if (scene.path == Mission && !scene.isDirty) return true;
            Debug.LogError("CA01 flight preview: open and save M02 first."); return false;
        }
    }
}
