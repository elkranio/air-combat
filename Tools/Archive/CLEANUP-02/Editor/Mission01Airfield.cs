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

        [MenuItem("Air Combat/Missions/Apply Compact Airfield A")]
        private static void ApplyCompactA()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Debug.LogWarning("MISSION-01C2A: open Mission01_FirstSortie first.");
                return;
            }
            var anchor = scene.GetRootGameObjects().SingleOrDefault(g => g.name == "Mission01 / Airfield");
            var layout = anchor != null ? anchor.transform.Cast<Transform>()
                .SingleOrDefault(t => t.name == "Airfield v0 - editable layout") : null;
            if (layout == null)
            {
                Debug.LogWarning("MISSION-01C2A: expected the existing v0 layout; nothing changed.");
                return;
            }

            // Modify only the authored v0 scene instance. Keep buildings fixed in world space.
            Transform runway, apron, taxiway, markings, approach, parked, start;
            Transform[] runwayParts, standLines, aircraft;
            try
            {
                runway = Child(layout, "Runway and apron");
                apron = Child(runway, "03 / Apron - aircraft and hangar");
                taxiway = Child(runway, "04 / Taxiway runway-to-apron");
                markings = Child(layout, "Apron connections and markings");
                approach = Child(markings, "Hangar approach");
                parked = Child(layout, "Parked aircraft - visuals only");
                start = Child(layout, "Takeoff start - visual placeholder");
                runwayParts = runway.Cast<Transform>().Where(t =>
                    t.name.StartsWith("01 / Graded runway", StringComparison.Ordinal) ||
                    t.name.StartsWith("02 / Asphalt runway", StringComparison.Ordinal) ||
                    t.name.StartsWith("Runway edge", StringComparison.Ordinal) ||
                    t.name.StartsWith("Runway center dash", StringComparison.Ordinal) ||
                    t.name.StartsWith("Threshold", StringComparison.Ordinal)).ToArray();
                if (runwayParts.Length != 25 ||
                    !Mathf.Approximately(Child(runway, "02 / Asphalt runway 460x36").localScale.z, 460f))
                    throw new InvalidOperationException("Unexpected v0 runway geometry.");
                standLines = Enumerable.Range(1, 3).SelectMany(i => new[] {
                    Child(markings, "Stand " + i + " left"),
                    Child(markings, "Stand " + i + " right"),
                    Child(markings, "Stand " + i + " front") }).ToArray();
                aircraft = new[] { Child(parked, "Kestrel 01"), Child(parked, "Kestrel 02"), Child(parked, "Lynx 03") };
            }
            catch (Exception ex)
            {
                Debug.LogError("MISSION-01C2A: unexpected layout; nothing changed. " + ex.Message);
                return;
            }

            const string undo = "Apply compact airfield A";
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undo);
            var changed = new[] { runway, apron, taxiway, approach, start }
                .Concat(runwayParts).Concat(standLines).Concat(aircraft).ToArray();
            Undo.RecordObjects(changed.Cast<UnityEngine.Object>().ToArray(), undo);
            Undo.RecordObjects(new UnityEngine.Object[] { layout.gameObject, aircraft[2].gameObject }, undo);
            try
            {
                const float runwayX = 85f;
                const float widthRatio = 26f / 36f;
                const float lengthRatio = 300f / 460f;
                // Rescale each pavement/marking child, not the root: apron and taxiway
                // must keep their independent dimensions and rotation.
                runway.localPosition = new Vector3(runwayX, runway.localPosition.y, 0f);
                foreach (Transform part in runwayParts)
                {
                    Vector3 p = part.localPosition, s = part.localScale;
                    part.localPosition = new Vector3(p.x * widthRatio, p.y, p.z * lengthRatio);
                    part.localScale = new Vector3(s.x * widthRatio, s.y, s.z * lengthRatio);
                }
                SetSurface(apron, 160f - runwayX, -75f, 90f, 70f);
                // Short angled connection from the left apron edge to the runway,
                // pointing along +Z (takeoff direction) when leaving the apron.
                Vector3 a = new Vector3(122f - runwayX, 0f, -80f);
                Vector3 b = new Vector3(95f - runwayX, 0f, -50f);
                Vector3 delta = b - a;
                Vector3 center = (a + b) * .5f;
                SetSurface(taxiway, center.x, center.z, 18f, delta.magnitude);
                taxiway.localRotation = Quaternion.LookRotation(delta, Vector3.up);
                // Existing hangar still opens at approximately X240.
                SetSurface(approach, 222.5f, -75f, 35f, 36f);

                Quaternion standYaw = Quaternion.Euler(0f, -75f, 0f);
                for (int i = 0; i < 3; i++)
                {
                    Vector3 c = new Vector3(174f, .078f, -97f + i * 22f);
                    SetStandLine(standLines[i * 3], c, standYaw, -9f, 0f, .65f, 22f);
                    SetStandLine(standLines[i * 3 + 1], c, standYaw, 9f, 0f, .65f, 22f);
                    // Close the tail end; the nose end stays open towards the taxi lane.
                    SetStandLine(standLines[i * 3 + 2], c, standYaw, 0f, -11f, 18.65f, .65f);
                    Transform jet = aircraft[i];
                    jet.localPosition = new Vector3(c.x, jet.localPosition.y, c.z);
                    jet.localRotation = standYaw;
                }
                // Our Lynx is already on the runway. Keep its bay empty; retain the
                // parked prefab disabled so later iterations can restore it if needed.
                aircraft[2].gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(aircraft[2].gameObject);
                start.localPosition = new Vector3(runwayX, start.localPosition.y, -115f);
                layout.gameObject.name = "Airfield A - editable layout";
                foreach (Transform t in changed)
                    if (PrefabUtility.IsPartOfPrefabInstance(t))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(t);
                Undo.CollapseUndoOperations(undoGroup);
                EditorSceneManager.MarkSceneDirty(scene);
                Selection.activeGameObject = layout.gameObject;
                Debug.Log("MISSION-01C2A: compact layout A applied. Review and save the scene.");
            }
            catch (Exception ex)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                Debug.LogError("MISSION-01C2A: layout change reverted. " + ex);
            }
        }

        [MenuItem("Air Combat/Missions/Apply Compact Airfield B")]
        private static void ApplyCompactB()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Debug.LogWarning("MISSION-01C2B: open Mission01_FirstSortie first.");
                return;
            }
            var anchor = scene.GetRootGameObjects().SingleOrDefault(g => g.name == "Mission01 / Airfield");
            var layout = anchor != null ? anchor.transform.Cast<Transform>()
                .SingleOrDefault(t => t.name == "Airfield A - editable layout") : null;
            if (layout == null)
            {
                Debug.LogWarning("MISSION-01C2B: expected layout A; nothing changed.");
                return;
            }

            Transform runway, apron, taxiway, markings, approach, parked, start, edge;
            Transform[] runwayParts, standLines, aircraft;
            try
            {
                runway = Child(layout, "Runway and apron");
                apron = Child(runway, "03 / Apron - aircraft and hangar");
                taxiway = Child(runway, "04 / Taxiway runway-to-apron");
                markings = Child(layout, "Apron connections and markings");
                approach = Child(markings, "Hangar approach");
                parked = Child(layout, "Parked aircraft - visuals only");
                start = Child(layout, "Takeoff start - visual placeholder");
                edge = Child(runway, "Runway edge 1");
                runwayParts = runway.Cast<Transform>().Where(t =>
                    t.name.StartsWith("01 / Graded runway", StringComparison.Ordinal) ||
                    t.name.StartsWith("02 / Asphalt runway", StringComparison.Ordinal) ||
                    t.name.StartsWith("Runway edge", StringComparison.Ordinal) ||
                    t.name.StartsWith("Runway center dash", StringComparison.Ordinal) ||
                    t.name.StartsWith("Threshold", StringComparison.Ordinal)).ToArray();
                var asphalt = Child(runway, "02 / Asphalt runway 460x36");
                if (runwayParts.Length != 25 || !Mathf.Approximately(asphalt.localScale.x, 26f) ||
                    !Mathf.Approximately(asphalt.localScale.z, 300f))
                    throw new InvalidOperationException("Expected the 300 x 26 runway from A.");
                standLines = Enumerable.Range(1, 3).SelectMany(i => new[] {
                    Child(markings, "Stand " + i + " left"),
                    Child(markings, "Stand " + i + " right"),
                    Child(markings, "Stand " + i + " front") }).ToArray();
                aircraft = new[] { Child(parked, "Kestrel 01"), Child(parked, "Kestrel 02"), Child(parked, "Lynx 03") };
                if (markings.Cast<Transform>().Any(t => t.name == "Runway edge - south of apron access"))
                    throw new InvalidOperationException("B access marking already exists.");
            }
            catch (Exception ex)
            {
                Debug.LogError("MISSION-01C2B: unexpected layout; nothing changed. " + ex.Message);
                return;
            }

            const string undo = "Apply compact airfield B";
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undo);
            var changed = new[] { runway, apron, taxiway, approach, start }
                .Concat(runwayParts).Concat(standLines).Concat(aircraft).ToArray();
            Undo.RecordObjects(changed.Cast<UnityEngine.Object>().ToArray(), undo);
            Undo.RecordObjects(new UnityEngine.Object[] { layout.gameObject, taxiway.gameObject,
                aircraft[2].gameObject }, undo);
            try
            {
                const float runwayX = 125f;
                const float widthRatio = 24f / 26f;
                const float lengthRatio = 240f / 300f;
                runway.localPosition = new Vector3(runwayX, runway.localPosition.y, 0f);
                foreach (Transform part in runwayParts)
                {
                    Vector3 p = part.localPosition, s = part.localScale;
                    part.localPosition = new Vector3(p.x * widthRatio, p.y, p.z * lengthRatio);
                    part.localScale = new Vector3(s.x * widthRatio, s.y, s.z * lengthRatio);
                }
                // Apron abuts the runway's right edge. No separate diagonal road.
                // Local layout bounds: apron X137..219, Z-108..-42.
                SetSurface(apron, 178f - runwayX, -75f, 82f, 66f);
                taxiway.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(taxiway.gameObject);
                // Continuous paved lane from the runway to the existing hangar portal.
                SetSurface(approach, 231f, -75f, 24f, 26f);

                // Break the runway edge paint only at the entrance to the clear lane.
                // Preserve both ends of the original edge line, with no paint across the opening.
                float end = edge.localScale.z * .5f;
                float edgeX = edge.localPosition.x;
                float edgeWidth = edge.localScale.x;
                const float south = -88f, north = -62f;
                CopySurface(edge, markings, "Runway edge - south of apron access",
                    runwayX + edgeX, edge.localPosition.y, (-end + south) * .5f,
                    edgeWidth, south + end);
                Undo.RegisterCreatedObjectUndo(Child(markings,
                    "Runway edge - south of apron access").gameObject, undo);
                SetSurface(edge, edgeX, (north + end) * .5f, edgeWidth, end - north);

                // Three bays in a single row off the side of the through lane, not across it.
                // Nose points into the open apron; all aircraft clear the hangar approach.
                Quaternion standYaw = Quaternion.identity;
                for (int i = 0; i < 3; i++)
                {
                    Vector3 c = new Vector3(152f + i * 22f, .078f, -96f);
                    SetStandLine(standLines[i * 3], c, standYaw, -9f, 0f, .65f, 22f);
                    SetStandLine(standLines[i * 3 + 1], c, standYaw, 9f, 0f, .65f, 22f);
                    SetStandLine(standLines[i * 3 + 2], c, standYaw, 0f, -11f, 18.65f, .65f);
                    Transform jet = aircraft[i];
                    jet.localPosition = new Vector3(c.x, jet.localPosition.y, c.z);
                    jet.localRotation = standYaw;
                }
                aircraft[2].gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(aircraft[2].gameObject);
                start.localPosition = new Vector3(runwayX, start.localPosition.y, -92f);
                layout.gameObject.name = "Airfield B - editable layout";
                foreach (Transform t in changed)
                    if (PrefabUtility.IsPartOfPrefabInstance(t))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(t);
                Undo.CollapseUndoOperations(undoGroup);
                EditorSceneManager.MarkSceneDirty(scene);
                Selection.activeGameObject = layout.gameObject;
                Debug.Log("MISSION-01C2B: compact layout B applied. Review and save the scene.");
            }
            catch (Exception ex)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                Debug.LogError("MISSION-01C2B: layout change reverted. " + ex);
            }
        }

        private static void SetStandLine(Transform line, Vector3 center, Quaternion yaw,
            float x, float z, float width, float length)
        {
            line.localPosition = center + yaw * new Vector3(x, 0f, z);
            line.localRotation = yaw;
            line.localScale = new Vector3(width, line.localScale.y, length);
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
