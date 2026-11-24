#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AirCombat
{
    // B1e: one incremental airfield composition in the authored E1 world.
    // Hangar + tower are independent prefabs; runway/apron are separate meshes.
    // Reuse the existing player's visual chassis for two non-flying parked props.
    // Do not rebuild the terrain, prepared lot, HQ, barracks or camera.
    public static class InstallAirfieldB1E
    {
        private const string ScenePath = "Assets/_Project/Scenes/ArcadeDesertE1.unity";
        private const string ModelFolder = "Assets/_Project/Art/Buildings/Base/Airfield_B1e";
        private const string PrefabFolder = "Assets/_Project/Prefabs/Environment/Base";
        private const string B1Root = "B1 / Airfield - runway hangar tower";
        private const string HangarPrefabPath = PrefabFolder + "/B1e_Hangar.prefab";
        private const string TowerPrefabPath = PrefabFolder + "/B1e_ControlTower.prefab";
        private const string SurfacesPrefabPath = PrefabFolder + "/B1e_RunwayAndApron.prefab";
        private const string UnitMeshPath = ModelFolder + "/B1e_UnitSurface.asset";

        private static readonly string[] HangarParts = { "Hangar_Walls", "Hangar_Roof", "Hangar_Openings", "Hangar_Trim" };
        private static readonly string[] TowerParts = { "Tower_Structure", "Tower_Cabin", "Tower_Glass" };
        private static readonly Vector3 RunwayCenter = new Vector3(-770, 0, -290);
        private static readonly Vector3 HangarPosition = new Vector3(-619, 0, -280);
        private static readonly Vector3 TowerPosition = new Vector3(-603, 0, -200);

        [MenuItem("Air Combat/Art/B1e - Install Airfield: Runway + Hangar + Tower")]
        private static void InstallFromMenu()
        {
            try { Debug.Log(Install()); }
            catch (Exception error) { Debug.LogError("B1e airfield installation FAILED: " + error); }
        }

        public static string Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before B1e installation.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save or discard all dirty scenes before B1e installation.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                throw new InvalidOperationException("ArcadeDesertE1 scene not found.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(HangarPrefabPath) != null ||
                File.Exists(HangarPrefabPath) || File.Exists(TowerPrefabPath) ||
                File.Exists(SurfacesPrefabPath) || File.Exists(UnitMeshPath))
                throw new InvalidOperationException("B1e output already exists; don't overwrite authored work.");

            var urp = Shader.Find("Universal Render Pipeline/Lit");
            if (urp == null) throw new InvalidOperationException("URP Lit shader missing.");
            var models = HangarParts.Concat(TowerParts).ToDictionary(x => x, x =>
                AssetDatabase.LoadAssetAtPath<GameObject>(ModelFolder + "/" + x + ".obj"));
            foreach (var entry in models)
                if (entry.Value == null || entry.Value.GetComponentsInChildren<MeshFilter>(true).Length == 0)
                    throw new InvalidOperationException("Missing imported OBJ mesh: " + entry.Key);

            var barracksWalls = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/Art/Buildings/Base/Barracks_B1a/B1a_Walls.mat");
            var barracksRoof = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/Art/Buildings/Base/Barracks_B1a/B1a_Roof.mat");
            var parkedPlane = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Chassis/Kestrel.prefab");
            if (barracksWalls == null || barracksRoof == null || parkedPlane == null)
                throw new InvalidOperationException("Existing barracks palette or Kestrel visual prefab missing.");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            if (roots.Any(r => r.name == B1Root))
                throw new InvalidOperationException("B1e is already installed. Edit existing scene objects instead.");
            if (!roots.Any(r => r.name == "B1 / Base sector - barracks trial") ||
                !roots.Any(r => r.name == "B1 / HQ and courtyard") ||
                !roots.Any(r => r.name.StartsWith("E1C / Main plateau")))
                throw new InvalidOperationException("Required approved scene roots changed; inspect current map before B1e.");
            if (!roots.Any(r => r.name == "B1 / Prepared lot - DISABLE to compare"))
                throw new InvalidOperationException("Expected B1d3 prepared lot not installed; do not overwrite an earlier layout.");

            EnsureFolder(PrefabFolder);
            var dark = MakeMaterial(urp, "B1e_Dark", "#253945");
            var trim = MakeMaterial(urp, "B1e_Trim", "#A78C68");
            var concrete = MakeMaterial(urp, "B1e_ApronConcrete", "#929B97");
            var runway = MakeMaterial(urp, "B1e_RunwayAsphalt", "#555E60");
            var shoulder = MakeMaterial(urp, "B1e_GradedShoulder", "#A18E79");
            var marking = MakeMaterial(urp, "B1e_Marking", "#B8BAB1");
            var wear = MakeMaterial(urp, "B1e_Wear", "#4C5759");

            var hangarPrefab = CreateBuildingPrefab(scene, models, HangarParts,
                new[] { barracksWalls, barracksRoof, dark, trim },
                "B1e / Hangar - editable", HangarPrefabPath,
                new Vector3(0, 7, 0), new Vector3(38, 14.5f, 52));
            var towerPrefab = CreateBuildingPrefab(scene, models, TowerParts,
                new[] { trim, barracksWalls, dark },
                "B1e / Control tower - editable", TowerPrefabPath,
                new Vector3(0, 11.1f, 0), new Vector3(13, 22.3f, 13));

            var unit = MakeUnitMesh();
            AssetDatabase.CreateAsset(unit, UnitMeshPath);
            var runwayRoot = new GameObject("B1e / Runway and apron - independent surfaces");
            SceneManager.MoveGameObjectToScene(runwayRoot, scene);
            runwayRoot.transform.position = RunwayCenter;

            // At y~0.03, on flat ground, without modifying TerrainData or B1d3.
            // The airstrip is a separate developed zone, not an extension of
            // the prepared-lot visual mesh behind the barracks/HQ.
            Rect(runwayRoot.transform, unit, "01 / Graded runway shoulders", shoulder,
                RunwayCenter + new Vector3(0, .028f, 0), 48f, 474f, true);
            Rect(runwayRoot.transform, unit, "02 / Asphalt runway 460x36", runway,
                RunwayCenter + new Vector3(0, .051f, 0), 36f, 460f, true);

            // One uninterrupted, wide hardstand: an actual apron, not a decorative
            // rectangle underneath each new prop.
            Rect(runwayRoot.transform, unit, "03 / Apron - aircraft and hangar", concrete,
                new Vector3(-678f, .052f, -280f), 120f, 102f, true);
            Rect(runwayRoot.transform, unit, "04 / Taxiway runway-to-apron", runway,
                new Vector3(-740f, .061f, -280f), 50f, 25f, true);

            // Legible painted markings are sparse. No perfect stripe every 2m.
            for (int i = -5; i <= 5; i++)
                Rect(runwayRoot.transform, unit, "Runway center dash " + i.ToString("+00;-00;0"),
                    marking, new Vector3(RunwayCenter.x, .076f, RunwayCenter.z + i * 39f), 1.2f, 15.5f, false);
            for (int side = -1; side <= 1; side += 2)
                Rect(runwayRoot.transform, unit, "Runway edge " + side, marking,
                    new Vector3(RunwayCenter.x + side * 16.65f, .076f, RunwayCenter.z),
                    .55f, 438f, false);
            foreach (float end in new[] {-217f, 217f})
                for (int i = -2; i <= 2; i++)
                    Rect(runwayRoot.transform, unit, "Threshold " + end + "/" + i, marking,
                        new Vector3(RunwayCenter.x + i * 5.7f, .077f, RunwayCenter.z + end),
                        2.1f, 7f, false);

            // Two clearly separated aircraft parking bays, not two giant planes
            // pushed into the hangar. Both are editable, and can be deleted.
            foreach (float z in new[] {-307f, -254f})
            {
                Rect(runwayRoot.transform, unit, "Parking stand transverse " + z,
                    marking, new Vector3(-673, .077f, z - 11), 29, .34f, false);
                foreach (float x in new[] {-689f, -657f})
                    Rect(runwayRoot.transform, unit, "Parking stand edge " + x + "/" + z,
                        marking, new Vector3(x, .077f, z), .34f, 24f, false);
            }
            // A handful of restrained surface scuffs, all separately removable.
            var marks = new GameObject("05 / Light wear - disable independently");
            marks.transform.SetParent(runwayRoot.transform, false);
            foreach (var x in new [] { -779.3f, -762.5f })
                Rect(marks.transform, unit, "Short touchdown trace", wear,
                    new Vector3(x, .079f, -116f), .55f, 17.0f, false);
            Rect(marks.transform, unit, "Apron wheel trace", wear,
                new Vector3(-701, .079f, -323), .5f, 10f, false);

            var surfacesPrefab = PrefabUtility.SaveAsPrefabAsset(runwayRoot, SurfacesPrefabPath);
            Object.DestroyImmediate(runwayRoot);
            if (surfacesPrefab == null) throw new InvalidOperationException("Unable to save runway/apron prefab.");

            var airfield = new GameObject(B1Root);
            SceneManager.MoveGameObjectToScene(airfield, scene);
            MakeSceneInstance(surfacesPrefab, scene, airfield.transform,
                "Airfield surfaces - edit as prefab", RunwayCenter);
            MakeSceneInstance(hangarPrefab, scene, airfield.transform,
                "Hangar / maintenance", HangarPosition);
            MakeSceneInstance(towerPrefab, scene, airfield.transform,
                "Control tower", TowerPosition);

            // Kestrel is the *visual chassis only* (no FlightController, weapons
            // or enemy AI). One reusable prefab instance per parking stand.
            var planeColors = new [] {
                AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Environment/ArcadeDesertE1/E1C/E1C_Kestrel_Hull.mat"),
                AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Environment/ArcadeDesertE1/E1C/E1C_Kestrel_Upper.mat"),
                AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Environment/ArcadeDesertE1/E1C/E1C_Kestrel_Panels.mat")
            };
            for (int i = 0; i < 2; i++)
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(parkedPlane, scene);
                visual.name = $"Parked Kestrel {i+1:00} - visual only";
                visual.transform.SetParent(airfield.transform, true);
                visual.transform.SetPositionAndRotation(new Vector3(-673f, 1.1f, i == 0 ? -307f : -254f),
                    Quaternion.Euler(0, -90, 0));
                // Existing jet paint is also scene-local; no shared chassis edits.
                foreach (var renderer in visual.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var mats = renderer.sharedMaterials;
                    bool changed = false;
                    for (int j = 0; j < mats.Length; j++)
                    {
                        string name = mats[j] != null ? mats[j].name : "";
                        int idx = name.Contains("Hull") ? 0 : name.Contains("Upper") ? 1 :
                            name.Contains("Panels") ? 2 : -1;
                        if (idx >= 0 && planeColors[idx] != null) { mats[j] = planeColors[idx]; changed = true; }
                    }
                    if (changed)
                    {
                        renderer.sharedMaterials = mats;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    }
                }
                // A parked plane must not run even a chassis-only behavioural script.
                foreach (var behaviour in visual.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    behaviour.enabled = false;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(behaviour);
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications(visual.transform);
            }

            // Never reposition the approved Y78 approach, buildings or plateau.
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Unable to save scene with B1e airfield.");
            Selection.activeGameObject = airfield;
            return "B1e airfield installed: runway 460x36, apron, independent hangar/tower prefabs, " +
                "two static Kestrel visuals. B1d3 prepared lot, barracks, HQ, plateau, camera and Y78 intact. " +
                "Review Game view before designing permanent road connections. Commit generated .meta/assets/scene.";
        }

        private static Material MakeMaterial(Shader shader, string name, string color)
        {
            string path = ModelFolder + "/" + name + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null || File.Exists(path))
                throw new InvalidOperationException("B1e material exists: " + path);
            if (!ColorUtility.TryParseHtmlString(color, out var albedo))
                throw new InvalidOperationException("Invalid color: " + color);
            var mat = new Material(shader) { name = name };
            mat.SetColor("_BaseColor", albedo);
            mat.SetFloat("_Metallic", 0);
            mat.SetFloat("_Smoothness", 0);
            mat.enableInstancing = true;
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static GameObject CreateBuildingPrefab(Scene scene,
            System.Collections.Generic.IDictionary<string, GameObject> models,
            string[] parts, Material[] palette,
            string objectName, string prefabPath, Vector3 colliderCenter, Vector3 colliderSize)
        {
            var master = new GameObject(objectName);
            SceneManager.MoveGameObjectToScene(master, scene);
            for (int i = 0; i < parts.Length; i++)
            {
                var section = (GameObject)PrefabUtility.InstantiatePrefab(models[parts[i]], scene);
                section.name = parts[i];
                section.transform.SetParent(master.transform, false);
                section.transform.localPosition = Vector3.zero;
                section.transform.localRotation = Quaternion.identity;
                section.transform.localScale = Vector3.one;
                foreach (var renderer in section.GetComponentsInChildren<MeshRenderer>(true))
                {
                    int n = Mathf.Max(1, renderer.sharedMaterials.Length);
                    var mats = new Material[n];
                    for (int j = 0; j < n; j++) mats[j] = palette[i];
                    renderer.sharedMaterials = mats;
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }
            }
            var collider = master.AddComponent<BoxCollider>();
            collider.center = colliderCenter;
            collider.size = colliderSize;
            var prefab = PrefabUtility.SaveAsPrefabAsset(master, prefabPath);
            Object.DestroyImmediate(master);
            if (prefab == null) throw new InvalidOperationException("Unable to save " + prefabPath);
            return prefab;
        }

        private static GameObject MakeSceneInstance(GameObject prefab, Scene scene, Transform parent,
            string name, Vector3 worldPosition)
        {
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            obj.name = name;
            obj.transform.SetParent(parent, true);
            obj.transform.position = worldPosition;
            obj.transform.rotation = Quaternion.identity;
            PrefabUtility.RecordPrefabInstancePropertyModifications(obj.transform);
            return obj;
        }

        private static Mesh MakeUnitMesh()
        {
            var mesh = new Mesh { name = "B1e / Flat ground surface - 1m square" };
            mesh.vertices = new [] {
                new Vector3(-.5f,0,-.5f), new Vector3(-.5f,0,.5f),
                new Vector3(.5f,0,.5f), new Vector3(.5f,0,-.5f) };
            mesh.triangles = new [] {0,1,2,0,2,3};
            mesh.uv = new [] {new Vector2(0,0),new Vector2(0,1),new Vector2(1,1),new Vector2(1,0)};
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static GameObject Rect(Transform parent, Mesh mesh, string name, Material material,
            Vector3 pos, float width, float length, bool collider)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.position = pos;
            obj.transform.localScale = new Vector3(width, 1, length);
            var filter = obj.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = obj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; // painted ground is not raised geometry.
            renderer.receiveShadows = true;
            if (collider)
            {
                // Existing terrain remains the collision authority for now.
                // Surfaces are visual overlays: avoid physics mesh collider z-fights.
            }
            return obj;
        }

        private static void EnsureFolder(string path)
        {
            string current = "Assets";
            foreach (string name in path.Substring("Assets/".Length).Split('/'))
            {
                string next = current + "/" + name;
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, name);
                current = next;
            }
        }
    }
}
#endif
