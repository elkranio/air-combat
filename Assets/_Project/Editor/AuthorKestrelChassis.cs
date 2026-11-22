#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat.EditorTools
{
    // One-time NON-DESTRUCTIVE asset authoring: copies the approved WeaponLab
    // Kestrel artwork to a reusable chassis prefab. Never modifies WeaponLab,
    // never rebuilds the scene and refuses to overwrite an existing prefab.
    internal static class AuthorKestrelChassis
    {
        private const string SourceScene = "Assets/_Project/Scenes/WeaponLab.unity";
        private const string PrefabsFolder = "Assets/_Project/Prefabs";
        private const string ChassisFolder = PrefabsFolder + "/Chassis";
        private const string PrefabPath = ChassisFolder + "/Kestrel.prefab";
        private const string MenuPath = "Air Combat/Prefabs/Author Kestrel Chassis";

        [MenuItem(MenuPath)]
        private static void Author()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Stop Play Mode before authoring the chassis prefab.");
                return;
            }
            if (SceneManager.GetActiveScene().path != SourceScene)
            {
                Debug.LogError("Open the existing WeaponLab.unity before authoring the Kestrel chassis.");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                Debug.LogWarning("Kestrel.prefab already exists. Not overwriting an authored chassis.");
                return;
            }

            GameObject player = GameObject.Find("Player");
            Transform visual = player != null ? player.transform.Find("JetVisual") : null;
            AfterburnerVfx vfx = visual != null ? visual.GetComponent<AfterburnerVfx>() : null;
            if (visual == null || vfx == null)
            {
                Debug.LogError("Existing WeaponLab Player/JetVisual/AfterburnerVfx is missing. No assets created.");
                return;
            }

            // Capture the exact source positions BEFORE copying. The current
            // flight root and JetVisual origins are offset from each other.
            var so = new SerializedObject(vfx);
            var nozzlePositions = so.FindProperty("nozzlePositions");
            if (nozzlePositions == null || !nozzlePositions.isArray || nozzlePositions.arraySize != 2)
            {
                Debug.LogError("Expected exactly two authored Kestrel engine nozzles. No assets created.");
                return;
            }
            Vector3 leftEngine = nozzlePositions.GetArrayElementAtIndex(0).vector3Value;
            Vector3 rightEngine = nozzlePositions.GetArrayElementAtIndex(1).vector3Value;
            Vector3 ToChassis(Vector3 rootLocal) =>
                visual.InverseTransformPoint(player.transform.TransformPoint(rootLocal));
            Vector3 leftGun = ToChassis(new Vector3(-1.35f, -.55f, 5.6f));
            Vector3 rightGun = ToChassis(new Vector3(1.35f, -.55f, 5.6f));
            Vector3 gauss = ToChassis(new Vector3(0f, -.55f, 5.6f));
            Vector3 missile = ToChassis(new Vector3(0f, 0f, 6f));

            EnsureFolder(PrefabsFolder);
            EnsureFolder(ChassisFolder);

            // Saving a scene GameObject creates an asset COPY. In particular,
            // it does not turn WeaponLab's existing visual into an instance.
            bool success;
            var result = PrefabUtility.SaveAsPrefabAsset(visual.gameObject, PrefabPath, out success);
            if (!success || result == null)
            {
                Debug.LogError("Could not save Kestrel prefab; source scene remains unchanged.");
                return;
            }

            GameObject contents = null;
            try
            {
                // LoadPrefabContents edits an isolated prefab stage, never the
                // active test scene. Boost VFX belongs to the role jet root,
                // not to a reusable visual chassis with no Afterburner attached.
                contents = PrefabUtility.LoadPrefabContents(PrefabPath);
                var originalVfx = contents.GetComponent<AfterburnerVfx>();
                if (originalVfx != null) UnityEngine.Object.DestroyImmediate(originalVfx);
                contents.name = "Kestrel";

                var chassis = contents.GetComponent<JetChassis>();
                if (chassis == null) chassis = contents.AddComponent<JetChassis>();
                var sockets = new GameObject("Hardpoints").transform;
                sockets.SetParent(contents.transform, false);
                chassis.SetAuthoringSockets(
                    Socket(sockets, "Autocannon_L", leftGun),
                    Socket(sockets, "Autocannon_R", rightGun),
                    Socket(sockets, "Gauss", gauss),
                    Socket(sockets, "Missile", missile),
                    Socket(sockets, "Engine_L", leftEngine),
                    Socket(sockets, "Engine_R", rightEngine));
                if (!chassis.HasRequiredSockets)
                    throw new InvalidOperationException("Kestrel hardpoints were not assigned.");
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath, out success);
                if (!success) throw new InvalidOperationException("Failed to save Kestrel hardpoints.");
            }
            catch (Exception ex)
            {
                // Do not leave behind an incomplete asset that would block a retry.
                Debug.LogException(ex);
                AssetDatabase.DeleteAsset(PrefabPath);
                return;
            }
            finally
            {
                if (contents != null) PrefabUtility.UnloadPrefabContents(contents);
            }
            AssetDatabase.SaveAssets();
            var authored = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var authoredChassis = authored != null ? authored.GetComponent<JetChassis>() : null;
            if (authoredChassis == null || !authoredChassis.HasRequiredSockets)
            {
                Debug.LogError("Kestrel prefab saved without valid hardpoints. Inspect the prefab before use.");
                return;
            }
            Debug.Log("Authored reusable visual chassis: " + PrefabPath +
                ". WeaponLab remains unchanged. Stage the generated prefab and its .meta in Git.");
            EditorGUIUtility.PingObject(authored);
        }

        private static Transform Socket(Transform parent, string name, Vector3 position)
        {
            var go = new GameObject(name);
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = position;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
            return t;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            int separator = folder.LastIndexOf('/');
            var parent = folder.Substring(0, separator);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder.Substring(separator + 1));
        }
    }
}
#endif
