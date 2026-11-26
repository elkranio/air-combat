using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AirCombat
{
    // One-time authoring only. The resulting scene and meshes are normal assets.
    public static class Mission02RouteAuthoring
    {
        private const string Source = "Assets/_Project/Scenes/Mission01_FirstSortie.unity";
        private const string Destination = "Assets/_Project/Scenes/Mission02_ConvoyInterdiction.unity";
        private const string Folder = "Assets/_Project/Art/Environment/Mission02_ConvoyInterdiction";
        private const string Geometry = "Assets/_Project/Editor/Mission02/Mission02RockGeometry.json";
        private const string AirfieldPrefab = "Assets/_Project/Prefabs/Environment/Base/Airfield_B.prefab";
        private const string PlayerPrefab = "Assets/_Project/Prefabs/Aircraft/PlayerLynx_Combat.prefab";
        private const string CameraPrefab = "Assets/_Project/Prefabs/Camera/ArcadeCameraRig.prefab";
        private const string HudPrefab = "Assets/_Project/Prefabs/UI/CombatHUD.prefab";
        private static readonly Rect Bounds = new Rect(-2430f, -1845f, 4860f, 3690f);
        private static readonly Vector3 Spawn = new Vector3(-1912.5f, 0f, 580.5f);
        private static readonly Vector3 A = new Vector3(-1363.5f, 0f, 1314f);
        private static readonly Vector3 B = new Vector3(1732.5f, 0f, 1156.5f);
        private static readonly Vector3 C = new Vector3(1998f, 0f, 420f);

        [Serializable] private sealed class Rock { public Vector3[] vertices; public int[] sides, tops; }
        [Serializable] private sealed class Layout { public Rock[] rocks; public Vector3[] direct, optional, exit, convoy; }

        [MenuItem("Air Combat/Missions/M02 - Create Route Graybox")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            // Never regenerate an authored mission or delete a pre-existing asset.
            if (AssetDatabase.LoadMainAssetAtPath(Destination) != null || AssetDatabase.IsValidFolder(Folder))
            { Debug.LogError("M02 already exists. Open the saved scene; this installer is one-shot."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            bool createdScene = false, createdFolder = false;
            try
            {
                var data = AssetDatabase.LoadAssetAtPath<TextAsset>(Geometry);
                var layout = data != null ? JsonUtility.FromJson<Layout>(data.text) : null;
                Require(layout != null && layout.rocks != null && layout.rocks.Length == 2 &&
                    layout.direct != null && layout.optional != null && layout.exit != null && layout.convoy != null,
                    "missing route geometry");
                foreach (string path in new[] { Source, AirfieldPrefab, PlayerPrefab, CameraPrefab, HudPrefab })
                    Require(AssetDatabase.LoadMainAssetAtPath(path) != null, "missing " + path);
                Require(AssetDatabase.CopyAsset(Source, Destination), "could not copy M01 scene");
                createdScene = true;
                Scene scene = EditorSceneManager.OpenScene(Destination, OpenSceneMode.Single);
                SceneManager.SetActiveScene(scene);
                var roots = scene.GetRootGameObjects();
                var launch = Unique<Mission01LaunchSequence>(roots);
                var oldFlight = new SerializedObject(launch).FindProperty("flight").objectReferenceValue as FlightController;
                Require(oldFlight != null, "M01 runway aircraft not found");
                var oldTerrain = Unique<Terrain>(roots);
                var oldRig = Unique<CombatCameraRig>(roots);
                var oldFollow = oldRig.GetComponent<ArcadeCameraFollow>();
                Require(oldFollow != null, "M01 camera follow not found");
                var oldSession = Unique<FlightSession>(roots);
                var pavement = roots.SelectMany(r => r.GetComponentsInChildren<MeshFilter>(false))
                    .Where(f => f.sharedMesh != null && f.sharedMesh.name == "Mission01_HangarPass").ToArray();
                Require(pavement.Length == 1, "expected one active M01 runway pavement");
                Transform field = FindAirfield(pavement[0].transform);
                Require(field != null && field.GetComponentInChildren<Terrain>(true) == null &&
                    field.GetComponentInChildren<FlightController>(true) == null &&
                    field.GetComponentInChildren<RadarTerrainObstacle>(true) == null,
                    "airfield is not an isolated visual hierarchy");
                Vector3 oldSpawn = oldFlight.transform.position;
                float oldGround = oldTerrain.SampleHeight(oldSpawn) + oldTerrain.transform.position.y;
                Vector3 oldHeading = Vector3.ProjectOnPlane(oldFlight.transform.forward, Vector3.up).normalized;
                Require(oldHeading.sqrMagnitude > .9f, "invalid runway heading");
                Vector3 targetHeading = new Vector3(0.92f, 0f, 0.38f).normalized;
                Quaternion rotate = Quaternion.FromToRotation(oldHeading, targetHeading);
                Vector3 newSpawn = Spawn + Vector3.up * (oldSpawn.y - oldGround);
                Vector3 fieldPosition = newSpawn + rotate * (field.position - oldSpawn);
                Quaternion fieldRotation = rotate * field.rotation;
                // Keep the exact authored airfield instance from the COPY, including
                // pavement/building overrides; clone only if nested in another root.
                GameObject airfield = field.gameObject;
                if (field.parent != null)
                {
                    airfield = Object.Instantiate(field.gameObject);
                    airfield.transform.localScale = field.lossyScale;
                }
                airfield.name = "Airfield B / reused M01";
                airfield.transform.SetPositionAndRotation(fieldPosition, fieldRotation);
                PrefabUtility.RecordPrefabInstancePropertyModifications(airfield.transform);

                Require(!string.IsNullOrEmpty(AssetDatabase.CreateFolder("Assets/_Project/Art/Environment", "Mission02_ConvoyInterdiction")),
                    "could not create M02 art folder");
                createdFolder = true;
                // New scene-specific terrain; only shared material/layers are reused.
                CreateGround(oldTerrain);
                GameObject player = Instantiate(PlayerPrefab, scene);
                player.transform.SetPositionAndRotation(newSpawn, rotate * oldFlight.transform.rotation);
                PrefabUtility.RecordPrefabInstancePropertyModifications(player.transform);
                var newFlight = player.GetComponent<FlightController>();
                CopyFloats(oldFlight, newFlight, "minSpeed", "cruiseSpeed", "maxSpeed", "turnRate", "precisionTurnMultiplier", "maxBank", "bankRate");
                CopyFloats(oldFlight.GetComponent<Afterburner>(), player.GetComponent<Afterburner>(),
                    "boostSpeed", "boostAcceleration", "capacitySeconds", "rechargeSeconds", "rechargeDelay");
                var camera = Instantiate(CameraPrefab, scene);
                // Preserve the actual M01 camera altitude relative to its runway spawn.
                camera.transform.position = newSpawn + Vector3.up * Mathf.Max(20f, oldRig.transform.position.y - oldSpawn.y);
                camera.transform.rotation = oldRig.transform.rotation;
                PrefabUtility.RecordPrefabInstancePropertyModifications(camera.transform);
                CopyFloats(oldFollow, camera.GetComponent<ArcadeCameraFollow>(), "pitch", "yaw", "orthographicSize",
                    "cruiseLead", "boostExtraLead", "leadTransitionSeconds", "aircraftViewportMargin", "positionSmoothTime");
                var hud = Instantiate(HudPrefab, scene).GetComponent<CombatHud>();
                var sessionObject = new GameObject("M02 / route trial session");
                var session = sessionObject.AddComponent<FlightSession>();
                session.Configure(player.GetComponent<JetAssembly>(), camera.GetComponent<CombatCameraRig>(), Mathf.Max(Bounds.width, Bounds.height) * .5f);
                session.ConfigureHud(hud, Array.Empty<RadarAirContact>());
                CopyFloats(oldSession, session, "tightTurnSpeed", "boostTurnMultiplier", "tightTurnMultiplier", "maneuverResponse");
                var trial = sessionObject.AddComponent<Mission02RouteTrial>();
                var guide = CreateGuide(layout);
                trial.Configure(hud, guide, Bounds, A, B, C);
                CreateRocks(layout);

                // Preserve M01's RenderSettings and actual directional lighting;
                // do not retain any old mission controllers, enemies or geography.
                Light sun = null;
                foreach (var old in roots.SelectMany(r => r.GetComponentsInChildren<Light>(false)).Where(l => l.type == LightType.Directional).ToArray())
                {
                    var copy = Object.Instantiate(old.gameObject);
                    copy.name = old.name;
                    copy.transform.SetPositionAndRotation(old.transform.position, old.transform.rotation);
                    if (sun == null || old == RenderSettings.sun) sun = copy.GetComponent<Light>();
                }
                Require(sun != null, "M01 directional light not found");
                foreach (GameObject root in roots)
                    if (root != airfield) Object.DestroyImmediate(root);
                RenderSettings.sun = sun;
                Require(!scene.GetRootGameObjects().Any(r => r.GetComponentInChildren<Mission01LaunchSequence>(true) != null),
                    "M01 runtime leaked into M02");
                Require(EditorSceneManager.SaveScene(scene), "could not save M02 scene");
                AssetDatabase.SaveAssets();
                Selection.activeGameObject = sessionObject;
                Debug.Log("M02 created: W takeoff, G route lines, R restart. Timing starts at flight handover; A/C/B split at 75 units. Save and commit the generated scene and art assets.", sessionObject);
            }
            catch (Exception error)
            {
                Debug.LogError("M02 graybox: " + error);
                // Roll back only paths created during this invocation; M01 is never saved here.
                if (createdScene)
                {
                    EditorSceneManager.OpenScene(Source, OpenSceneMode.Single);
                    AssetDatabase.DeleteAsset(Destination);
                }
                if (createdFolder) AssetDatabase.DeleteAsset(Folder);
            }
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }

        private static T Unique<T>(GameObject[] roots) where T : Component
        {
            var matches = roots.SelectMany(r => r.GetComponentsInChildren<T>(false)).ToArray();
            Require(matches.Length == 1, "expected one active " + typeof(T).Name + ", found " + matches.Length);
            return matches[0];
        }

        private static Transform FindAirfield(Transform pavement)
        {
            // Prefer the outer authored Airfield_B instance, not nested tower/hangar prefabs.
            Transform candidate = null, named = null;
            for (Transform t = pavement; t != null; t = t.parent)
            {
                var source = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                if (source != null && AssetDatabase.GetAssetPath(source) == AirfieldPrefab) candidate = t;
                if (t.name.IndexOf("airfield", StringComparison.OrdinalIgnoreCase) >= 0) named = t;
            }
            return candidate != null ? candidate : named != null ? named : pavement.root;
        }

        private static GameObject Instantiate(string path, Scene scene)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var go = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            Require(go != null, "could not instantiate " + path);
            return go;
        }

        private static void CopyFloats(Component source, Component destination, params string[] fields)
        {
            Require(source != null && destination != null, "missing tuning component");
            var from = new SerializedObject(source); var to = new SerializedObject(destination);
            foreach (string name in fields)
            {
                var a = from.FindProperty(name); var b = to.FindProperty(name);
                Require(a != null && b != null && a.propertyType == SerializedPropertyType.Float && b.propertyType == SerializedPropertyType.Float,
                    "missing tuning field " + name);
                b.floatValue = a.floatValue;
            }
            to.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(destination);
        }

        private static void CreateGround(Terrain source)
        {
            var data = new TerrainData { name = "M02 flat desert", heightmapResolution = 513,
                alphamapResolution = 128, size = new Vector3(Bounds.width + 1200f, 100f, Bounds.height + 1200f) };
            data.SetHeights(0, 0, new float[513, 513]);
            data.terrainLayers = source.terrainData.terrainLayers;
            if (data.alphamapLayers > 0)
            {
                var weights = new float[128, 128, data.alphamapLayers];
                for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++) weights[y, x, 0] = 1f;
                data.SetAlphamaps(0, 0, weights);
            }
            AssetDatabase.CreateAsset(data, Folder + "/M02_Terrain.asset");
            var ground = Terrain.CreateTerrainGameObject(data);
            ground.name = "M02 / flat terrain + surround";
            ground.transform.position = new Vector3(Bounds.xMin - 600f, 0f, Bounds.yMin - 600f);
            var terrain = ground.GetComponent<Terrain>();
            terrain.materialTemplate = source.materialTemplate;
            terrain.drawInstanced = source.drawInstanced;
            terrain.heightmapPixelError = source.heightmapPixelError;
        }

        private static Material MakeMaterial(string name, string shaderName, Color tint)
        {
            var shader = Shader.Find(shaderName);
            Require(shader != null, "missing shader " + shaderName);
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", tint);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0f);
            AssetDatabase.CreateAsset(material, Folder + "/" + name + ".mat");
            return material;
        }

        private static void CreateRocks(Layout layout)
        {
            Material side = MakeMaterial("M02_Cliff", "Universal Render Pipeline/Lit", new Color(.48f, .40f, .31f));
            Material top = MakeMaterial("M02_RockTop", "Universal Render Pipeline/Lit", new Color(.58f, .49f, .39f));
            var group = new GameObject("M02 / canyon massifs");
            for (int i = 0; i < layout.rocks.Length; i++)
            {
                Rock data = layout.rocks[i];
                Require(data.vertices != null && data.sides != null && data.tops != null && data.vertices.Length > 0, "invalid rock mesh");
                var mesh = new Mesh { name = "M02_Massif_" + (i + 1), vertices = data.vertices, subMeshCount = 2 };
                mesh.SetTriangles(data.sides, 0); mesh.SetTriangles(data.tops, 1);
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                // Do not upload as unreadable: radar and swept collision use the mesh.
                AssetDatabase.CreateAsset(mesh, Folder + "/" + mesh.name + ".asset");
                var rock = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer));
                rock.transform.SetParent(group.transform, false);
                rock.GetComponent<MeshFilter>().sharedMesh = mesh;
                rock.GetComponent<MeshRenderer>().sharedMaterials = new[] { side, top };
                rock.AddComponent<RadarTerrainObstacle>();
            }
        }

        private static GameObject CreateGuide(Layout layout)
        {
            var guide = new GameObject("M02 / DEBUG route guides (G)");
            var blue = MakeMaterial("M02_GuideBlue", "Universal Render Pipeline/Unlit", new Color(.13f, .43f, .80f));
            var teal = MakeMaterial("M02_GuideTeal", "Universal Render Pipeline/Unlit", new Color(.06f, .58f, .59f));
            var red = MakeMaterial("M02_GuideRed", "Universal Render Pipeline/Unlit", new Color(.72f, .24f, .12f));
            Line(guide.transform, "S to A", layout.direct, blue, 3f, false);
            Line(guide.transform, "S to C", layout.optional, teal, 3f, false);
            Line(guide.transform, "C to B", layout.exit, blue, 3f, false);
            Line(guide.transform, "A to B / future convoy", layout.convoy, red, 2.5f, false);
            Ring(guide.transform, A, red); Ring(guide.transform, B, red); Ring(guide.transform, C, teal);
            return guide;
        }

        private static void Ring(Transform parent, Vector3 point, Material material)
        {
            var ring = new Vector3[48];
            for (int i = 0; i < ring.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / ring.Length;
                ring[i] = point + new Vector3(Mathf.Cos(angle) * 34f, .55f, Mathf.Sin(angle) * 34f);
            }
            Line(parent, "POI / arrival marker", ring, material, 3f, true);
        }

        private static void Line(Transform parent, string name, Vector3[] points, Material material, float width, bool loop)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true; line.loop = loop; line.sharedMaterial = material;
            line.positionCount = points.Length; line.SetPositions(points);
            line.startWidth = line.endWidth = width;
            line.alignment = LineAlignment.TransformZ;
            // Ground-plane strip; rotate local Z to face up.
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            line.numCornerVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
        }
    }
}
