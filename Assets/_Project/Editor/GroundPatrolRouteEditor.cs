#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    // Editor update callbacks can still run during play-mode transitions.
    // In particular isPlayingOrWillChangePlaymode alone is not sufficient
    // when leaving Play Mode. Never move units or dirty scenes in that window.
    [InitializeOnLoad]
    internal static class PatrolAuthoringState
    {
        private static bool blocked;

        static PatrolAuthoringState()
        {
            blocked = Application.isPlaying || EditorApplication.isPlaying ||
                EditorApplication.isPlayingOrWillChangePlaymode;
            EditorApplication.playModeStateChanged += state =>
                blocked = state != PlayModeStateChange.EnteredEditMode;
        }

        internal static bool CanEdit => !blocked && !Application.isPlaying &&
            !EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    [CustomEditor(typeof(GroundPatrolRoute))]
    public sealed class GroundPatrolRouteEditor : Editor
    {
        private void OnEnable() => EditorApplication.update += RefreshSelected;
        private void OnDisable() => EditorApplication.update -= RefreshSelected;

        private void RefreshSelected()
        {
            if (!PatrolAuthoringState.CanEdit || target == null) return;
            var route = (GroundPatrolRoute)target;
            if (Selection.activeGameObject != route.gameObject || EditorUtility.IsPersistent(route)) return;
            // Derived authoring pose: parent translation, rotation, and undo are
            // reflected even when the Transform inspector (not the route) changed.
            RefreshOwnedPreview(route);
        }

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            using (new EditorGUI.DisabledScope(!PatrolAuthoringState.CanEdit))
                DrawDefaultInspector();
            bool changed = EditorGUI.EndChangeCheck();
            var route = (GroundPatrolRoute)target;
            if (changed && PatrolAuthoringState.CanEdit) Changed(route);
            EditorGUILayout.HelpBox("Move or yaw this group with the normal Transform tools. Drag numbered square handles to edit its shape. Corner Radius rounds the actual driven path; waypoint handles remain the sharp control points. The child previews the start corner exit. Do not scale a group containing a vehicle; edit the points instead.", MessageType.Info);
            using (new EditorGUI.DisabledScope(!PatrolAuthoringState.CanEdit))
            {
                if ((route.transform.localScale - Vector3.one).sqrMagnitude > .000001f && OwnedMotors(route).Any())
                {
                    EditorGUILayout.HelpBox("Group scale also scales the vehicle. Bake scale into the route points to keep the model at normal size.", MessageType.Warning);
                    if (GUILayout.Button("Bake group scale into waypoints")) BakeScale(route);
                }
                if (GUILayout.Button("Check route for obstacles")) CheckClearance(route);
                if (GUILayout.Button("Add point at Scene view pivot"))
                {
                    Vector3 position = SceneView.lastActiveSceneView != null
                        ? SceneView.lastActiveSceneView.pivot : route.transform.position;
                    Undo.RecordObject(route, "Add patrol waypoint");
                    route.AddAuthoringPoint(position);
                    Changed(route);
                }
                if (GUILayout.Button("Frame route") && route.Count > 0)
                {
                    var bounds = new Bounds(route.GetPoint(0), Vector3.one * 10f);
                    for (int i = 1; i < route.Count; i++) bounds.Encapsulate(route.GetPoint(i));
                    SceneView.lastActiveSceneView?.Frame(bounds, false);
                }
            }
        }

        private void OnSceneGUI()
        {
            if (!PatrolAuthoringState.CanEdit) return;
            var route = (GroundPatrolRoute)target;
            Color before = Handles.color;
            var starts = OwnedMotors(route).Select(x => x.StartPoint).ToArray();
            for (int i = 0; i < route.Count; i++)
            {
                Vector3 p = route.GetPoint(i) + Vector3.up * .6f;
                float size = HandleUtility.GetHandleSize(p) * .075f;
                bool isStart = starts.Length == 0 ? i == 0 : starts.Contains(i);
                Handles.color = isStart ? Color.yellow : Color.cyan;
                Handles.Label(p + Vector3.up * size * 2f, $"{i + 1:00}{(isStart ? " START" : "")}");
                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.Slider2D(p, Vector3.up, Vector3.right, Vector3.forward,
                    size, Handles.RectangleHandleCap, Vector2.zero);
                if (!EditorGUI.EndChangeCheck()) continue;
                Undo.RecordObject(route, "Move patrol waypoint");
                route.MoveAuthoringPoint(i, moved - Vector3.up * .6f);
                Changed(route);
            }
            Handles.color = before;
        }

        private static void Changed(GroundPatrolRoute route)
        {
            if (!PatrolAuthoringState.CanEdit || route == null) return;
            EditorUtility.SetDirty(route);
            PrefabUtility.RecordPrefabInstancePropertyModifications(route);
            EditorSceneManager.MarkSceneDirty(route.gameObject.scene);
            RefreshOwnedPreview(route);
            SceneView.RepaintAll();
        }

        private static GroundPatrolMotor[] OwnedMotors(GroundPatrolRoute route) => route
            .GetComponentsInChildren<GroundPatrolMotor>(true)
            .Where(m => m.Route == route && m.transform.parent == route.transform).ToArray();

        private static void RefreshOwnedPreview(GroundPatrolRoute route)
        {
            if (!PatrolAuthoringState.CanEdit || route == null) return;
            foreach (var motor in OwnedMotors(route)) SnapPreview(motor, false);
        }

        internal static void SnapPreview(GroundPatrolMotor motor, bool recordUndo)
        {
            if (!PatrolAuthoringState.CanEdit || motor == null || EditorUtility.IsPersistent(motor) ||
                !motor.gameObject.scene.IsValid() || !motor.TryGetStartPose(out Vector3 position, out Quaternion rotation)) return;
            Transform t = motor.transform;
            if ((t.position - position).sqrMagnitude < .000001f && Quaternion.Angle(t.rotation, rotation) < .05f) return;
            if (recordUndo) Undo.RecordObject(t, "Snap patrol start");
            t.SetPositionAndRotation(position, rotation);
            // This is a derived preview, not a separate user edit / undo step.
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
            SceneView.RepaintAll();
        }

        private static void BakeScale(GroundPatrolRoute route)
        {
            Vector3[] points = Enumerable.Range(0, route.Count).Select(route.GetPoint).ToArray();
            var motors = OwnedMotors(route);
            Undo.RecordObject(route.transform, "Bake route scale");
            Undo.RecordObject(route, "Bake route scale");
            foreach (var motor in motors) Undo.RecordObject(motor.transform, "Bake route scale");
            route.transform.localScale = Vector3.one;
            route.ConfigureAuthoring(route.Surface, points);
            foreach (var motor in motors) motor.transform.localScale = Vector3.one;
            Changed(route);
        }

        internal static bool GroupUnit(GroundPatrolMotor motor)
        {
            if (!PatrolAuthoringState.CanEdit || motor == null) return false;
            var route = motor.Route;
            if (route == null || route.transform == motor.transform || route.gameObject.scene != motor.gameObject.scene ||
                EditorUtility.IsPersistent(route) || EditorUtility.IsPersistent(motor)) return false;
            if (motor.transform.parent == route.transform) { SnapPreview(motor, true); return true; }
            // A standalone shared route is still valid. Do not silently move it
            // into one unit's group or change the placement of other consumers.
            bool shared = route.gameObject.scene.GetRootGameObjects()
                .SelectMany(x => x.GetComponentsInChildren<GroundPatrolMotor>(true))
                .Any(x => x != motor && x.Route == route);
            if (shared || route.transform.parent != null || motor.transform.parent != null)
            {
                Debug.LogWarning("Patrol: grouping requires a dedicated root route and root vehicle. Shared routes remain independent.", motor);
                return false;
            }
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Group patrol unit and route");
            // Preserve world waypoints, including any existing non-uniform scale.
            BakeScale(route);
            Undo.SetTransformParent(motor.transform, route.transform, "Group patrol unit and route");
            SnapPreview(motor, true);
            Undo.RecordObject(route.gameObject, "Name patrol group");
            route.gameObject.name = motor.gameObject.name.Contains("Mantis") ? "Mantis / Patrol Group" : motor.gameObject.name + " / Patrol Group";
            EditorUtility.SetDirty(route.gameObject);
            EditorSceneManager.MarkSceneDirty(route.gameObject.scene);
            Undo.CollapseUndoOperations(group);
            return true;
        }

        private static void CheckClearance(GroundPatrolRoute route)
        {
            if (route.Count == 0 || route.Surface == null)
            {
                Debug.LogWarning("Patrol: assign a Terrain and at least one waypoint.", route);
                return;
            }
            Physics.SyncTransforms();
            var path = route.BuildPath();
            int legs = Mathf.Max(1, path.Count);
            Vector3 origin = route.Surface.transform.position;
            Vector3 size = route.Surface.terrainData.size;
            for (int i = 0; i < legs; i++)
            {
                var segment = path.Count > 0 ? path.Segments[i] : default;
                int samples = Mathf.Max(1, Mathf.CeilToInt(segment.Length / 2f));
                for (int j = 0; j <= samples; j++)
                {
                    Vector3 p = route.Project(path.Count > 0 ? segment.Position(j / (float)samples) : route.GetPoint(0), out _);
                    if (p.x < origin.x || p.z < origin.z || p.x > origin.x + size.x || p.z > origin.z + size.z)
                    { Debug.LogWarning($"Patrol: path segment {i + 1} leaves Terrain near {p}.", route); return; }
                    // Approximate vehicle envelope; exclude Terrain and the patrol
                    // group itself. This is a warning, not navigation or physics.
                    var hit = Physics.OverlapSphere(p + Vector3.up * 1.5f, 4f, ~0, QueryTriggerInteraction.Ignore)
                        .FirstOrDefault(c => c.gameObject.scene == route.gameObject.scene && !(c is TerrainCollider) &&
                            !c.transform.IsChildOf(route.transform));
                    if (hit != null)
                    { Debug.LogWarning($"Patrol: possible obstacle on path segment {i + 1}: {hit.name} near {p}.", hit); return; }
                }
            }
            Debug.Log("Patrol: sampled route clear of non-Terrain colliders (approximate check).", route);
        }

        [MenuItem("Air Combat/Mission/Create Patrol Route")]
        private static void CreateRoute()
        {
            if (!PatrolAuthoringState.CanEdit) return;
            Scene scene = SceneManager.GetActiveScene();
            Terrain terrain = FindTerrain(scene);
            if (terrain == null) { Debug.LogError("Patrol: active scene needs a Terrain."); return; }
            Vector3 p = SceneView.lastActiveSceneView != null
                ? SceneView.lastActiveSceneView.pivot : terrain.transform.position + terrain.terrainData.size * .5f;
            GroundPatrolRoute route = MakeRoute(scene, terrain, p, "Patrol Route");
            Selection.activeGameObject = route.gameObject;
        }

        private static Terrain FindTerrain(Scene scene) => Terrain.activeTerrains.FirstOrDefault(x => x.gameObject.scene == scene);

        private static GroundPatrolRoute MakeRoute(Scene scene, Terrain terrain, Vector3 point, string name)
        {
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            // Keep a compact sample rectangle inside the terrain even near an edge.
            float width = Mathf.Min(65f, size.x * .25f), length = Mathf.Min(45f, size.z * .25f);
            point.x = Mathf.Clamp(point.x, origin.x + 5f, origin.x + size.x - width - 5f);
            point.z = Mathf.Clamp(point.z, origin.z + 5f, origin.z + size.z - length - 5f);
            point.y = terrain.SampleHeight(point) + origin.y;
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.position = point;
            Undo.RegisterCreatedObjectUndo(go, "Create patrol route");
            var route = Undo.AddComponent<GroundPatrolRoute>(go);
            Undo.RecordObject(route, "Set patrol waypoints");
            route.ConfigureAuthoring(terrain, new[] { point, point + Vector3.right * width,
                point + Vector3.right * width + Vector3.forward * length, point + Vector3.forward * length });
            Changed(route);
            return route;
        }
    }

    [CustomEditor(typeof(GroundPatrolMotor))]
    public sealed class GroundPatrolMotorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var motor = (GroundPatrolMotor)target;
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            bool changed = EditorGUI.EndChangeCheck();
            if (!PatrolAuthoringState.CanEdit || motor == null || EditorUtility.IsPersistent(motor)) return;
            if (motor.Route == null)
            {
                EditorGUILayout.HelpBox("No route: the unit stays stationary. Assign a scene Patrol Route here to enable movement.", MessageType.Info);
                return;
            }
            if (changed) GroundPatrolRouteEditor.SnapPreview(motor, true);
            if (GUILayout.Button("Snap to Start Point")) GroundPatrolRouteEditor.SnapPreview(motor, true);
            if (motor.transform.parent != motor.Route.transform && GUILayout.Button("Group with assigned route"))
            {
                if (GroundPatrolRouteEditor.GroupUnit(motor)) Selection.activeGameObject = motor.Route.gameObject;
            }
            if (motor.transform.parent == motor.Route.transform && GUILayout.Button("Select patrol group"))
                Selection.activeGameObject = motor.Route.gameObject;
        }
    }
}
#endif
