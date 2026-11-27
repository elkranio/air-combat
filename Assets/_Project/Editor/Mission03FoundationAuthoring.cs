using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

namespace AirCombat
{
    // One-time authoring tool. Never regenerate a scene that exists already.
    internal static class Mission03FoundationAuthoring
    {
        private const string Root = "Assets/_Project/";
        private const string ScenePath = Root + "Scenes/Mission03_SparrowInterception.unity";
        private const string GroundFolder = Root + "Art/Environment/Mission03_SparrowInterception";
        private static readonly Rect Bounds = new Rect(-1500f, -1200f, 3000f, 2400f);
        private static readonly Vector2 Home = new Vector2(-1050f, -850f);
        private static readonly Vector2 Enemy = new Vector2(950f, 750f);
        private static readonly Vector2 ProtectedSite = new Vector2(-200f, -780f);

        [MenuItem("Air Combat/Missions/M03 - Create Green Foundation")]
        private static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (File.Exists(ScenePath) || !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(ScenePath)) ||
                AssetDatabase.IsValidFolder(GroundFolder))
            {
                Debug.LogError("M03 foundation already exists (scene or ground assets). Open it; do not regenerate.");
                return;
            }
            try
            {
                // Fail before modifying anything if the shared project assets are missing.
                var playerPrefab = Load<GameObject>("Prefabs/Aircraft/PlayerLynx_Combat.prefab");
                var basePrefab = Load<GameObject>("Prefabs/Environment/Base/Airfield_B.prefab");
                var cameraPrefab = Load<GameObject>("Prefabs/Camera/ArcadeCameraRig.prefab");
                var hudPrefab = Load<GameObject>("Prefabs/UI/CombatHUD.prefab");
                var lightPrefab = Load<GameObject>("Prefabs/Environment/CombatLightingRig.prefab");
                var missile = Load<GameObject>("Art/Weapons/Missile.prefab").GetComponent<MissileProjectile>();
                var cruise = Load<AudioClip>("Art/Aircraft/Audio/Lynx_Engine_Cruise.wav");
                var boost = Load<AudioClip>("Art/Aircraft/Audio/Lynx_Engine_Boost.wav");
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null || missile == null || playerPrefab.GetComponent<JetAssembly>() == null ||
                    playerPrefab.GetComponent<MissionCheats>() == null || cameraPrefab.GetComponent<CombatCameraRig>() == null ||
                    hudPrefab.GetComponent<CombatHud>() == null || lightPrefab.GetComponent<CombatLightingRig>() == null)
                    throw new InvalidOperationException("Missing shared campaign components (including DEV01).");
                FindPavement(basePrefab);
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.CreateFolder(Root + "Art/Environment", "Mission03_SparrowInterception");
                var ground = CreateGround(shader);
                var airfield = Place(basePrefab, scene, "M03 / Trident airfield");
                Transform pavement = FindPavement(airfield);
                // Keep the existing airfield geometry. Align its actual runway to
                // north, then place the center at the SVG v0 home coordinates.
                float yaw = Mathf.Atan2(pavement.forward.x, pavement.forward.z) * Mathf.Rad2Deg;
                airfield.transform.rotation = Quaternion.Euler(0f, -yaw, 0f);
                Vector3 center = pavement.TransformPoint(new Vector3(125f, 0f, 0f));
                airfield.transform.position += new Vector3(Home.x - center.x, 0f, Home.y - center.z);
                var runway = new GameObject("M03 / friendly runway center").transform;
                runway.SetParent(airfield.transform, true);
                runway.SetPositionAndRotation(pavement.TransformPoint(new Vector3(125f, 0f, 0f)),
                    Quaternion.LookRotation(pavement.forward, Vector3.up));
                var player = Place(playerPrefab, scene, "PlayerLynx_Combat");
                Vector3 spawn = pavement.TransformPoint(new Vector3(125f, 0f, -92f));
                spawn.y = airfield.transform.position.y + .8857401f; // Existing M02 ground pose.
                player.transform.SetPositionAndRotation(spawn, runway.rotation);
                var camera = Place(cameraPrefab, scene, "M03 / combat camera").GetComponent<CombatCameraRig>();
                // Match the campaign camera axes; the shared map and radar follow this yaw.
                var follow = camera.GetComponent<ArcadeCameraFollow>();
                var cameraSettings = new SerializedObject(follow);
                cameraSettings.FindProperty("yaw").floatValue = 0f;
                cameraSettings.ApplyModifiedPropertiesWithoutUndo();
                camera.transform.position = spawn + new Vector3(0f, 166f, -190f);
                var hud = Place(hudPrefab, scene, "M03 / combat HUD").GetComponent<CombatHud>();
                Place(lightPrefab, scene, "M03 / daylight");
                var missionRoot = new GameObject("M03 / mission");
                var mission = missionRoot.AddComponent<Mission03Flight>();
                mission.ConfigureForAuthoring(hud, ground, runway, missile, Bounds);
                var session = missionRoot.AddComponent<FlightSession>();
                session.Configure(player.GetComponent<JetAssembly>(), camera, 1500f);
                session.ConfigureHud(hud, Array.Empty<RadarAirContact>());
                missionRoot.AddComponent<Mission01EngineAudio>().ConfigureForAuthoring(
                    player.GetComponent<FlightController>(), cruise, boost);
                var map = missionRoot.AddComponent<TacticalMapView>();
                map.ConfigureForAuthoring(null, player.transform, Array.Empty<TacticalMapView.PlannedSam>(),
                    Array.Empty<Transform>(), null, "SPARROW INTERCEPTION");
                AssetDatabase.SaveAssets();
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save M03 scene.");
                Mission03AutoMap.Refresh(mission, map, true);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save M03 map reference.");
                Selection.activeGameObject = player;
                Debug.Log("M03 foundation created. W takeoff, TAB map, R retry. Mission Cheats are on Lynx. M01/M02 untouched.");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                Debug.LogError("M03 foundation stopped. Inspect any newly created scene/assets; the installer never overwrites them.");
            }
        }

        private static T Load<T>(string relative) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(Root + relative);
            if (asset == null) throw new InvalidOperationException("Missing asset: " + Root + relative);
            return asset;
        }

        private static GameObject Place(GameObject prefab, Scene scene, string name)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = name; return instance;
        }

        private static Transform FindPavement(GameObject airfield)
        {
            var matches = airfield.GetComponentsInChildren<MeshFilter>(true).Where(f =>
                f.sharedMesh != null && f.sharedMesh.name == "Mission01_HangarPass").ToArray();
            if (matches.Length != 1) throw new InvalidOperationException("M03: expected one Airfield B C6 pavement mesh.");
            return matches[0].transform;
        }

        private static Renderer CreateGround(Shader shader)
        {
            // Low-poly broad meadow shapes. Flat pads reserve the two airfields
            // and defended site; no collision obstacles yet. Decorative margin
            // outside the operation covers the camera near the map edges.
            const int nx = 76, nz = 64;
            const float margin = 400f;
            float x0 = Bounds.xMin - margin, z0 = Bounds.yMin - margin;
            float dx = (Bounds.width + margin * 2f) / nx, dz = (Bounds.height + margin * 2f) / nz;
            var vertices = new List<Vector3>(nx * nz * 6);
            var indices = new List<int>[4];
            for (int i = 0; i < indices.Length; i++) indices[i] = new List<int>();
            Vector3 Point(int x, int z)
            {
                float px = x0 + x * dx, pz = z0 + z * dz;
                var p = new Vector2(px, pz);
                float pad = Mathf.Min(Vector2.Distance(p, Home),
                    Mathf.Min(Vector2.Distance(p, Enemy), Vector2.Distance(p, ProtectedSite)));
                float fade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(300f, 520f, pad));
                float height = 20f * Mathf.PerlinNoise(px * .0015f + 4f, pz * .0015f + 7f) +
                    6f * Mathf.PerlinNoise(px * .003f + 23f, pz * .003f + 11f);
                return new Vector3(px, -.04f + height * fade, pz);
            }
            void Triangle(Vector3 a, Vector3 b, Vector3 c, int band)
            {
                int start = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c);
                indices[band].Add(start); indices[band].Add(start + 1); indices[band].Add(start + 2);
            }
            for (int z = 0; z < nz; z++) for (int x = 0; x < nx; x++)
            {
                var a = Point(x, z); var b = Point(x + 1, z);
                var c = Point(x, z + 1); var d = Point(x + 1, z + 1);
                float noise = Mathf.PerlinNoise(a.x * .0012f + 18f, a.z * .0012f + 28f);
                int band = Mathf.Clamp(Mathf.FloorToInt(noise * 4f), 0, 3);
                if ((x + z) % 2 == 0) { Triangle(a, c, d, band); Triangle(a, d, b, band); }
                else { Triangle(a, c, b, band); Triangle(b, c, d, band); }
            }
            var mesh = new Mesh { name = "M03 broad meadow", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.subMeshCount = indices.Length;
            for (int i = 0; i < indices.Length; i++) mesh.SetTriangles(indices[i], i);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, GroundFolder + "/M03_Meadow.asset");
            var palette = new[] { new Color32(108, 126, 83, 255), new Color32(117, 134, 90, 255),
                new Color32(126, 140, 95, 255), new Color32(135, 145, 101, 255) };
            var materials = new Material[palette.Length];
            for (int i = 0; i < palette.Length; i++)
            {
                var mat = new Material(shader) { name = "Meadow " + i };
                mat.SetColor("_BaseColor", palette[i]); mat.SetFloat("_Smoothness", 0f); mat.SetFloat("_Metallic", 0f);
                AssetDatabase.CreateAsset(mat, GroundFolder + $"/M03_Meadow_{i}.mat"); materials[i] = mat;
            }
            var go = new GameObject("M03 / visible meadow", typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = materials; renderer.shadowCastingMode = ShadowCastingMode.Off;
            return renderer;
        }
    }
}
