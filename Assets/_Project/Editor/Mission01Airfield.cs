using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    // One-time layout authoring only. All objects remain editable in the mission scene.
    internal static class Mission01Airfield
    {
        private const string ScenePath = "Assets/_Project/Scenes/Mission01_FirstSortie.unity";
        private const string BasePrefabs = "Assets/_Project/Prefabs/Environment/Base/";
        private const string AircraftPrefabs = "Assets/_Project/Prefabs/Aircraft/";
        private const string UndoName = "Build Mission 01 airfield layout";

        [MenuItem("Air Combat/Missions/Build Mission 01 Airfield v0")]
        private static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Debug.LogError("MISSION-01C: open Mission01_FirstSortie before building its airfield.");
                return;
            }
            var anchor = scene.GetRootGameObjects().SingleOrDefault(g => g.name == "Mission01 / Airfield");
            if (anchor == null || anchor.transform.childCount != 0)
            {
                Debug.LogWarning("MISSION-01C: expected an empty airfield anchor; existing layout is never overwritten.");
                return;
            }

            // Resolve all required assets before touching the scene.
            var runwayAsset = AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabs + "B1e_RunwayAndApron.prefab");
            var hangarAsset = AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabs + "B1f_ArchHangar.prefab");
            var towerAsset = AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabs + "B1e_ControlTower.prefab");
            var lynxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(AircraftPrefabs + "PF02_Lynx.prefab");
            var kestrelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(AircraftPrefabs + "PlayerKestrel_Combat.prefab");
            var assembly = kestrelAsset != null ? kestrelAsset.GetComponent<JetAssembly>() : null;
            var chassis = assembly != null ? new SerializedObject(assembly).FindProperty("chassis")?.objectReferenceValue as Component : null;
            if (runwayAsset == null || hangarAsset == null || towerAsset == null || lynxAsset == null || chassis == null)
            {
                Debug.LogError("MISSION-01C: required runway, building or aircraft asset missing; nothing changed.");
                return;
            }

            GameObject layout = null;
            try
            {
                layout = new GameObject("Airfield v0 - editable layout");
                layout.transform.SetParent(anchor.transform, false);
                // Local +Z points along the runway towards the map interior.
                layout.transform.localRotation = Quaternion.Euler(0f, 30f, 0f);
                Transform root = layout.transform;

                var runway = PlacePrefab(runwayAsset, root, "Runway and apron", Vector3.zero, 0f);
                Transform apron = Child(runway.transform, "03 / Apron - aircraft and hangar");
                Transform taxiway = Child(runway.transform, "04 / Taxiway runway-to-apron");
                Transform lineSample = Child(runway.transform, "Runway edge 1");
                // Keep the existing 460 x 36 runway, shoulders and markings.
                // Change only this instance's apron/taxiway overrides.
                SetSurface(apron, 150f, -75f, 150f, 130f);
                SetSurface(taxiway, 46.5f, -112f, 57f, 22f);
                foreach (Transform child in runway.transform)
                {
                    if (child.name.StartsWith("Parking stand", StringComparison.Ordinal) ||
                        child.name.StartsWith("05 / Light wear", StringComparison.Ordinal))
                    {
                        child.gameObject.SetActive(false);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(child.gameObject);
                    }
                }

                // Independent geometry so the road pass can replace it without editing a shared prefab.
                var surfaces = Group(root, "Apron connections and markings");
                CopySurface(apron, surfaces, "Hangar approach", 232.5f, .052f, -75f, 15f, 36f);
                for (int i = 0; i < 3; i++)
                {
                    float x = 100f + i * 50f;
                    // Three open-ended parking stands, large enough to read at gameplay zoom.
                    CopySurface(lineSample, surfaces, "Stand " + (i + 1) + " left", x - 14f, .078f, -53f, .65f, 40f);
                    CopySurface(lineSample, surfaces, "Stand " + (i + 1) + " right", x + 14f, .078f, -53f, .65f, 40f);
                    CopySurface(lineSample, surfaces, "Stand " + (i + 1) + " front", x, .078f, -33f, 28.65f, .65f);
                }

                var buildings = Group(root, "Buildings");
                // The existing arch hangar opens towards local -X (towards the apron).
                PlacePrefab(hangarAsset, buildings, "Arch hangar", new Vector3(255f, 0f, -75f), 0f);
                PlacePrefab(towerAsset, buildings, "Control tower", new Vector3(150f, 0f, 20f), 0f);

                var parked = Group(root, "Parked aircraft - visuals only");
                // Copy mesh hierarchy and paint only, not the combat scripts or chassis logic.
                ParkKestrel(chassis.transform, parked, "Kestrel 01", 100f, -53f);
                ParkKestrel(chassis.transform, parked, "Kestrel 02", 150f, -53f);
                var lynx = PlacePrefab(lynxAsset, parked, "Lynx 03", new Vector3(200f, 0f, -53f), 0f);
                SitOnSurface(lynx, .10f);

                var start = Group(root, "Takeoff start - visual placeholder");
                start.localPosition = new Vector3(0f, .06f, -178f);
                var preview = PlacePrefab(lynxAsset, start, "Lynx - remove when player is integrated", Vector3.zero, 0f);
                SitOnSurface(preview, .11f);
                // No takeoff or player changes in this atom. The existing airborne Kestrel
                // remains available for reviewing the layout from the normal game camera.

                Undo.RegisterCreatedObjectUndo(layout, UndoName);
                EditorSceneManager.MarkSceneDirty(scene);
                Selection.activeGameObject = layout;
                Debug.Log("MISSION-01C: airfield v0 built. Review in Scene/Game view, then save the mission scene.");
            }
            catch (Exception ex)
            {
                if (layout != null) UnityEngine.Object.DestroyImmediate(layout);
                Debug.LogError("MISSION-01C: layout creation failed; partial layout removed. " + ex);
            }
        }

        private static Transform Group(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static Transform Child(Transform parent, string name)
        {
            // Names contain literal slashes; Transform.Find would interpret them as paths.
            return parent.Cast<Transform>().Single(t => t.name == name);
        }

        private static GameObject PlacePrefab(GameObject asset, Transform parent, string name, Vector3 localPosition, float yaw)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            if (go == null) throw new InvalidOperationException("Could not instantiate " + asset.name);
            go.name = name;
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(go);
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
            return go;
        }

        private static void SetSurface(Transform t, float x, float z, float width, float length)
        {
            t.localPosition = new Vector3(x, t.localPosition.y, z);
            t.localScale = new Vector3(width, t.localScale.y, length);
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
        }

        private static void CopySurface(Transform sample, Transform parent, string name,
            float x, float y, float z, float width, float length)
        {
            var filter = sample.GetComponent<MeshFilter>();
            var renderer = sample.GetComponent<MeshRenderer>();
            if (filter == null || renderer == null || filter.sharedMesh == null || renderer.sharedMaterial == null)
                throw new InvalidOperationException("Surface sample is incomplete: " + sample.name);
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, y, z);
            go.transform.localRotation = sample.localRotation;
            go.transform.localScale = new Vector3(width, 1f, length);
            go.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            var target = go.GetComponent<MeshRenderer>();
            target.sharedMaterials = renderer.sharedMaterials;
            target.shadowCastingMode = ShadowCastingMode.Off;
            target.receiveShadows = true;
        }

        private static void ParkKestrel(Transform source, Transform parent, string name, float x, float z)
        {
            var root = Group(parent, name);
            root.localPosition = new Vector3(x, 0f, z);
            var model = CopyVisual(source, root);
            // The source chassis is the identity-oriented nested model in the combat prefab.
            model.localPosition = Vector3.zero;
            model.localRotation = Quaternion.identity;
            SitOnSurface(root.gameObject, .10f);
        }

        private static Transform CopyVisual(Transform source, Transform parent)
        {
            var copy = Group(parent, source.name);
            copy.localPosition = source.localPosition;
            copy.localRotation = source.localRotation;
            copy.localScale = source.localScale;
            var filter = source.GetComponent<MeshFilter>();
            var renderer = source.GetComponent<MeshRenderer>();
            if (filter != null && renderer != null && filter.sharedMesh != null)
            {
                copy.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var target = copy.gameObject.AddComponent<MeshRenderer>();
                target.sharedMaterials = renderer.sharedMaterials;
                target.enabled = renderer.enabled;
                target.shadowCastingMode = renderer.shadowCastingMode;
                target.receiveShadows = renderer.receiveShadows;
            }
            foreach (Transform child in source) CopyVisual(child, copy);
            copy.gameObject.SetActive(source.gameObject.activeSelf);
            return copy;
        }

        private static void SitOnSurface(GameObject visual, float worldY)
        {
            var renderers = visual.GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length == 0)
                throw new InvalidOperationException("No visible mesh on " + visual.name);
            float bottom = renderers.Min(r => r.bounds.min.y);
            visual.transform.position += Vector3.up * (worldY - bottom);
            if (PrefabUtility.IsPartOfPrefabInstance(visual))
                PrefabUtility.RecordPrefabInstancePropertyModifications(visual.transform);
        }
    }
}
