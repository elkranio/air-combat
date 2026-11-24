using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    // One-time prefab authoring and mission free-flight preview. No runtime code.
    internal static class Mission01PlayerLynx
    {
        private const string Aircraft = "Assets/_Project/Prefabs/Aircraft/";
        private const string KestrelPath = Aircraft + "PlayerKestrel_Combat.prefab";
        private const string VisualPath = Aircraft + "PF02_Lynx.prefab";
        private const string LynxPath = Aircraft + "PlayerLynx_Combat.prefab";
        private const string ScenePath = "Assets/_Project/Scenes/Mission01_FirstSortie.unity";
        private const string UndoName = "Install Mission 01 player Lynx";

        [MenuItem("Air Combat/Missions/Install Player Lynx Preview")]
        private static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Debug.LogWarning("MISSION-01D: open Mission01_FirstSortie first.");
                return;
            }
            try
            {
                var session = scene.GetRootGameObjects().Select(g => g.GetComponent<E1MapFlightSession>())
                    .Single(s => s != null);
                var sessionData = new SerializedObject(session);
                var old = sessionData.FindProperty("player").objectReferenceValue as JetAssembly;
                var rig = sessionData.FindProperty("cameraRig").objectReferenceValue as CombatCameraRig;
                if (old == null || rig == null || old.transform.parent != null)
                    throw new InvalidOperationException("Expected the mission's root player and camera.");
                var sourcePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(old.gameObject);
                if (sourcePath == LynxPath)
                {
                    Debug.Log("MISSION-01D: Lynx preview already installed.");
                    return;
                }
                if (sourcePath != KestrelPath || old.GetComponent<PlayerWeaponTrigger>() == null ||
                    old.GetComponent<PlayerWeaponTrigger>().enabled)
                    throw new InvalidOperationException("Expected the temporary, weapons-disabled Kestrel preview.");
                var follow = rig.GetComponent<ArcadeCameraFollow>();
                if (follow == null) throw new InvalidOperationException("Missing arcade camera follow.");
                var cameraData = new SerializedObject(follow);
                var cameraTarget = cameraData.FindProperty("aircraft");
                if (cameraTarget == null || cameraTarget.objectReferenceValue != old.transform)
                    throw new InvalidOperationException("Unexpected camera target. Nothing changed.");

                var prefab = CreateOrLoad();
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(UndoName);
                try
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    if (instance == null) throw new InvalidOperationException("Could not instantiate Lynx.");
                    Undo.RegisterCreatedObjectUndo(instance, UndoName);
                    instance.name = "Mission01 / Player Lynx (flight preview)";
                    instance.transform.SetPositionAndRotation(old.transform.position, old.transform.rotation);
                    instance.transform.localScale = old.transform.localScale;
                    var jet = instance.GetComponent<JetAssembly>();
                    var trigger = instance.GetComponent<PlayerWeaponTrigger>();
                    if (jet == null || !jet.IsConfigured || trigger == null)
                        throw new InvalidOperationException("Incomplete Lynx prefab.");
                    // The mission is still a free-flight art preview. Do not equip the
                    // combat encounter or enable firing until its own mission atom.
                    trigger.enabled = false;
                    foreach (UnityEngine.Object obj in new UnityEngine.Object[] {instance, instance.transform, trigger})
                        PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
                    Undo.RecordObjects(new UnityEngine.Object[] {session, follow}, UndoName);
                    sessionData.FindProperty("player").objectReferenceValue = jet;
                    sessionData.ApplyModifiedProperties();
                    cameraTarget.objectReferenceValue = instance.transform;
                    cameraData.ApplyModifiedProperties();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(follow);
                    Undo.DestroyObjectImmediate(old.gameObject);
                    Undo.CollapseUndoOperations(group);
                    EditorSceneManager.MarkSceneDirty(scene);
                    Selection.activeGameObject = instance;
                    Debug.Log("MISSION-01D: Lynx flight preview ready. Save scene and prefab. Runway placeholder stays until ready/takeoff authoring.");
                }
                catch
                {
                    Undo.RevertAllDownToGroup(group);
                    throw;
                }
            }
            catch (Exception ex) { Debug.LogError("MISSION-01D: " + ex); }
        }

        private static GameObject CreateOrLoad()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(LynxPath);
            if (existing != null)
            {
                Validate(existing);
                return existing;
            }
            if (AssetDatabase.LoadMainAssetAtPath(LynxPath) != null)
                throw new InvalidOperationException("Unexpected asset at Lynx prefab path.");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPath);
            if (model == null || AssetDatabase.LoadAssetAtPath<GameObject>(KestrelPath) == null)
                throw new InvalidOperationException("Missing source aircraft prefabs.");

            // Work in an isolated prefab-editing scene. Save to a NEW path; the
            // original Kestrel and the standalone parked PF02 visual are immutable.
            var root = PrefabUtility.LoadPrefabContents(KestrelPath);
            try
            {
                var assembly = root.GetComponent<JetAssembly>();
                var visual = assembly.VisualRoot;
                var oldChassis = assembly.Chassis;
                if (!assembly.IsConfigured || oldChassis.transform.parent != visual)
                    throw new InvalidOperationException("Unexpected Kestrel chassis hierarchy.");
                var vfx = visual.GetComponent<AfterburnerVfx>();
                var nurs = root.GetComponentInChildren<ArcadeNursLauncher>(true);
                if (vfx == null || nurs == null) throw new InvalidOperationException("Missing source modules.");

                // PF02's authored model is centered at its own origin and points +Z.
                // Preserve the flight visual transform itself, but center the new model.
                visual.localPosition = Vector3.zero;
                var chassisRoot = new GameObject("Lynx chassis");
                chassisRoot.layer = oldChassis.gameObject.layer;
                chassisRoot.transform.SetParent(visual, false);
                var chassis = chassisRoot.AddComponent<JetChassis>();
                var meshInstance = (GameObject)PrefabUtility.InstantiatePrefab(model, chassisRoot.transform);
                if (meshInstance == null) throw new InvalidOperationException("PF02 visual instantiation failed.");
                meshInstance.transform.localPosition = Vector3.zero;
                meshInstance.transform.localRotation = Quaternion.identity;
                meshInstance.transform.localScale = Vector3.one;
                var sockets = new GameObject("Sockets").transform;
                sockets.SetParent(chassisRoot.transform, false);
                chassis.SetAuthoringSockets(
                    Socket(sockets, "Autocannon L", new Vector3(-1.35f, -.12f, 2.30f)),
                    Socket(sockets, "Autocannon R", new Vector3(1.35f, -.12f, 2.30f)),
                    Socket(sockets, "Gauss", new Vector3(0f, .02f, 7.30f)),
                    Socket(sockets, "Missile", new Vector3(0f, -.75f, 1.70f)),
                    Socket(sockets, "Engine L", new Vector3(-1.30f, -.16f, -6.23f)),
                    Socket(sockets, "Engine R", new Vector3(1.30f, -.16f, -6.23f)));

                // Retain the source's collision policy. A collider-free chassis
                // stays collider-free; otherwise use simple arcade hull/wing boxes.
                var oldColliders = oldChassis.GetComponentsInChildren<Collider>(true);
                if (oldColliders.Length > 0)
                {
                    var sample = oldColliders[0];
                    if (oldColliders.Any(c => c.isTrigger != sample.isTrigger || c.enabled != sample.enabled ||
                        c.gameObject.layer != sample.gameObject.layer || c.sharedMaterial != sample.sharedMaterial))
                        throw new InvalidOperationException("Mixed source collision settings need manual authoring.");
                    AddHitBox(chassisRoot.transform, sample, "Hull collider", new Vector3(0f, .15f, .35f), new Vector3(4.2f, 1.95f, 13.5f));
                    AddHitBox(chassisRoot.transform, sample, "Wing collider", new Vector3(0f, 0f, -1.65f), new Vector3(9.5f, .45f, 2.5f));
                }
                assembly.SetAuthoringReferences(visual, chassis);
                vfx.BindChassis(chassis);
                // Only the cosmetic launch origins change; the launcher tuning is copied intact.
                var data = new SerializedObject(nurs);
                data.FindProperty("leftPylon").vector3Value = new Vector3(-2.8f, -.45f, -1.55f);
                data.FindProperty("rightPylon").vector3Value = new Vector3(2.8f, -.45f, -1.55f);
                data.ApplyModifiedPropertiesWithoutUndo();
                UnityEngine.Object.DestroyImmediate(oldChassis.gameObject);
                root.name = "PlayerLynx_Combat";
                Validate(root);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, LynxPath);
                if (prefab == null) throw new InvalidOperationException("Could not save independent Lynx prefab.");
                AssetDatabase.SaveAssets();
                Validate(prefab);
                return prefab;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Transform Socket(Transform parent, string name, Vector3 localPosition)
        {
            var socket = new GameObject(name).transform;
            socket.SetParent(parent, false);
            socket.localPosition = localPosition;
            return socket;
        }

        private static void AddHitBox(Transform parent, Collider sample, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.layer = sample.gameObject.layer;
            go.tag = sample.gameObject.tag;
            go.transform.SetParent(parent, false);
            var box = go.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
            box.enabled = sample.enabled;
            box.isTrigger = sample.isTrigger;
            box.sharedMaterial = sample.sharedMaterial;
        }

        private static void Validate(GameObject root)
        {
            var jet = root.GetComponent<JetAssembly>();
            var flight = root.GetComponent<FlightController>();
            if (jet == null || !jet.IsConfigured || flight == null || flight.Visual != jet.VisualRoot ||
                root.GetComponent<Afterburner>() == null || root.GetComponent<PlayerWeaponTrigger>() == null ||
                root.GetComponent<PlayerHitPoints>() == null || root.GetComponent<AircraftRadar>() == null ||
                root.GetComponentInChildren<ArcadeNursLauncher>(true) == null ||
                root.GetComponentInChildren<ArcadeMissileLauncher>(true) == null ||
                root.GetComponentsInChildren<AutocannonWeapon>(true).Length != 2 ||
                root.GetComponentsInChildren<JetChassis>(true).Length != 1)
                throw new InvalidOperationException("Player prefab validation failed.");
            var vfx = jet.VisualRoot.GetComponent<AfterburnerVfx>();
            if (vfx == null || new SerializedObject(vfx).FindProperty("chassis").objectReferenceValue != jet.Chassis)
                throw new InvalidOperationException("Afterburner chassis binding is missing.");
        }
    }
}
