using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AirCombat
{
    // Incremental M02 repair. Does not regenerate the scene or edit M01.
    internal static class Mission02MapAuthoring
    {
        private const string ScenePath = "Assets/_Project/Scenes/Mission02_ConvoyInterdiction.unity";
        private const string MaterialPath = "Assets/_Project/Art/Environment/Mission02_ConvoyInterdiction/M02_Sand.mat";
        private const string GroundName = "M02 / visible sand";

        [MenuItem("Air Combat/Missions/M02 - Install Terrain Surface and Map")]
        private static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            { Debug.LogError("Open the saved Mission02_ConvoyInterdiction scene first."); return; }
            try
            {
                var roots = scene.GetRootGameObjects();
                var trial = roots.SelectMany(r => r.GetComponentsInChildren<Mission02RouteTrial>(true)).Single();
                var terrain = roots.SelectMany(r => r.GetComponentsInChildren<Terrain>(true)).Single();
                var player = roots.SelectMany(r => r.GetComponentsInChildren<JetAssembly>(true)).Single();
                var collider = terrain.GetComponent<TerrainCollider>();
                if (terrain.terrainData == null || collider == null || collider.terrainData != terrain.terrainData)
                    throw new InvalidOperationException("M02 terrain/collider data missing.");
                if (roots.SelectMany(r => r.GetComponentsInChildren<TacticalMapView>(true))
                    .Any(m => m.gameObject != trial.gameObject))
                    throw new InvalidOperationException("Unexpected existing tactical map owner.");

                var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
                if (material == null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader == null) throw new InvalidOperationException("URP/Lit shader missing.");
                    material = new Material(shader) { name = "M02 flat sand" };
                    material.SetColor("_BaseColor", new Color(.64f,.55f,.42f));
                    material.SetFloat("_Smoothness", 0f);
                    AssetDatabase.CreateAsset(material, MaterialPath);
                }

                // The existing Terrain stays the collision/height source. Its
                // invisible M01 presentation is replaced by one matte plane.
                var surface = roots.SingleOrDefault(r => r.name == GroundName);
                if (surface == null)
                {
                    surface = GameObject.CreatePrimitive(PrimitiveType.Plane);
                    surface.name = GroundName;
                    Object.DestroyImmediate(surface.GetComponent<Collider>());
                    Undo.RegisterCreatedObjectUndo(surface, "M02 sand surface");
                }
                var renderer = surface.GetComponent<MeshRenderer>();
                if (renderer == null) throw new InvalidOperationException("Unexpected M02 sand object.");
                Vector3 size = terrain.terrainData.size;
                Undo.RecordObject(surface.transform, "M02 ground dimensions");
                surface.transform.SetPositionAndRotation(terrain.transform.position +
                    new Vector3(size.x*.5f,-.02f,size.z*.5f), Quaternion.identity);
                surface.transform.localScale = new Vector3(size.x/10f,1f,size.z/10f);
                Undo.RecordObject(renderer, "M02 ground material");
                renderer.sharedMaterial = material;
                Undo.RecordObject(terrain, "M02 terrain presentation");
                terrain.drawHeightmap = false;

                MissionMapData map = MissionMapBaker.BakeForRouteTrial(scene, renderer, player.transform);
                if (map == null || map.Geography == null || map.ObstacleCount == 0 || map.SectionTriangleCount == 0)
                    throw new InvalidOperationException("M02 map bake contains no rock section.");
                var tactical = trial.GetComponent<TacticalMapView>();
                if (tactical == null) tactical = Undo.AddComponent<TacticalMapView>(trial.gameObject);
                Undo.RecordObject(tactical, "M02 tactical map binding");
                tactical.ConfigureForAuthoring(map, player.transform, Array.Empty<TacticalMapView.PlannedSam>(),
                    Array.Empty<Transform>(), null, "CONVOY INTERDICTION / ROUTE TRIAL");
                EditorUtility.SetDirty(tactical);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("M02 scene save failed.");
                AssetDatabase.SaveAssets();
                Selection.activeObject = map;
                Debug.Log("M02 map installed: Tab/Esc, A/B/C, routes, aircraft heading. Commit the saved scene and generated assets.", tactical);
            }
            catch (Exception error) { Debug.LogError("M02 map install: " + error); }
        }
    }
}
