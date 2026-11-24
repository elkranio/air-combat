#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    [CustomEditor(typeof(GroundPatrolRoute))]
    public sealed class GroundPatrolRouteEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var route = (GroundPatrolRoute)target;
            EditorGUILayout.HelpBox("Drag numbered square handles in Scene view (XZ). The route hugs its assigned Terrain. Edit the point list to reorder or remove waypoints. Avoid buildings and non-Terrain plateau meshes for this first test.", MessageType.Info);
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
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
            if (Application.isPlaying) return;
            var route = (GroundPatrolRoute)target;
            Color before = Handles.color;
            for (int i = 0; i < route.Count; i++)
            {
                Vector3 p = route.GetPoint(i) + Vector3.up * .6f;
                float size = HandleUtility.GetHandleSize(p) * .075f;
                Handles.color = i == 0 ? Color.yellow : Color.cyan;
                Handles.Label(p + Vector3.up * size * 2f, $"{i + 1:00}{(i == 0 ? " START" : "")}");
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
            EditorUtility.SetDirty(route);
            PrefabUtility.RecordPrefabInstancePropertyModifications(route);
            EditorSceneManager.MarkSceneDirty(route.gameObject.scene);
            SceneView.RepaintAll();
        }

        [MenuItem("Air Combat/Mission/Create Patrol Route")]
        private static void CreateRoute()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Scene scene = SceneManager.GetActiveScene();
            Terrain terrain = FindTerrain(scene);
            if (terrain == null) { Debug.LogError("Patrol: active scene needs a Terrain."); return; }
            Vector3 p = SceneView.lastActiveSceneView != null
                ? SceneView.lastActiveSceneView.pivot : terrain.transform.position + terrain.terrainData.size * .5f;
            GroundPatrolRoute route = MakeRoute(scene, terrain, p, "Patrol Route");
            Selection.activeGameObject = route.gameObject;
        }

        [MenuItem("Air Combat/Mission/AA-01b - Install Mantis patrol trial")]
        private static void InstallTrial()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Scene scene = SceneManager.GetSceneByPath("Assets/_Project/Scenes/ArcadeDesertE1.unity");
            if (!scene.IsValid() || !scene.isLoaded) { Debug.LogError("Open ArcadeDesertE1 first."); return; }
            GameObject trial = scene.GetRootGameObjects().FirstOrDefault(x => x.name == "AA-01a / Mantis - VISUAL TRIAL");
            Terrain terrain = FindTerrain(scene);
            if (trial == null || terrain == null)
            {
                Debug.LogError("Patrol: install the AA-01a visual trial and open its Terrain first.");
                return;
            }
            var motor = trial.GetComponent<GroundPatrolMotor>();
            if (motor != null && motor.Route != null)
            {
                Selection.activeGameObject = motor.Route.gameObject;
                return;
            }
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Install Mantis patrol trial");
            GroundPatrolRoute route = MakeRoute(scene, terrain, trial.transform.position, "AA-01b / Mantis patrol route");
            if (motor == null) motor = Undo.AddComponent<GroundPatrolMotor>(trial);
            Undo.RecordObject(motor, "Assign patrol route");
            motor.SetRoute(route);
            EditorUtility.SetDirty(motor);
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(group);
            Selection.activeGameObject = route.gameObject;
            Debug.Log("AA-01b: route installed. Drag the points onto clear terrain, save the scene, and enter Play Mode.", route);
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
}
#endif
