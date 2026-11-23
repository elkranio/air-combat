#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AirCombat
{
    // E1: one-time permanent, editable map foundation. No existing scene or
    // prefab is copied, modified or regenerated; editing the output is intentional.
    public static class BuildArcadeMapE1
    {
        private const string ScenePath = "Assets/_Project/Scenes/ArcadeDesertE1.unity";
        private const string ArtFolder = "Assets/_Project/Art/Environment/ArcadeDesertE1";
        private const float Side = 2000f;
        private const int HeightResolution = 513;
        private const int PaintResolution = 256;
        private const float GroundOriginY = -40f;
        private const float TerrainHeight = 180f;
        private const float GroundHeight = -GroundOriginY / TerrainHeight;

        private static readonly Color GroundColor = Parse("#DDA44D");
        private static readonly string[] RockColors = { "#C76D42", "#E18D48", "#AC5540" };

        // Hand-placed and deliberately sparse. The coordinates are metres on a
        // 2km map, not a random distribution. Group arrangements are editable.
        private static readonly Vector2[] ClusterCenters =
        {
            new Vector2(65, -130), new Vector2(-100, -55),
            new Vector2(100, 190), new Vector2(-160, 270),
            new Vector2(210, -265), new Vector2(-265, -340),
            new Vector2(330, 425), new Vector2(-390, 450),
            new Vector2(475, -530), new Vector2(-530, -515),
            new Vector2(610, 135), new Vector2(-635, 85)
        };

        [MenuItem("Air Combat/Art/E1 - Create Vibrant Desert Map")]
        private static void CreateFromMenu()
        {
            try { Debug.Log(Build()); }
            catch (Exception ex) { Debug.LogError("E1 map creation failed: " + ex); }
        }

        public static string Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before creating E1.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save or discard the open scene before E1.");
            if (System.IO.File.Exists(ScenePath) || AssetDatabase.IsValidFolder(ArtFolder))
                throw new InvalidOperationException("E1 already exists or has partial output. Never overwrite authored map work; inspect " + ScenePath + " and " + ArtFolder);

            var playerPrefab = Require<GameObject>("Assets/_Project/Prefabs/Aircraft/PlayerJet.prefab");
            var cameraPrefab = Require<GameObject>("Assets/_Project/Prefabs/Camera/CombatCameraRig.prefab");
            var lightPrefab = Require<GameObject>("Assets/_Project/Prefabs/Environment/CombatLightingRig.prefab");
            var terrainShader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            var rockShader = Shader.Find("Universal Render Pipeline/Lit");
            if (terrainShader == null || rockShader == null)
                throw new InvalidOperationException("URP Terrain/Lit or Lit shader is missing.");

            EnsureFolder(ArtFolder);
            var terrainData = MakeTerrainData();
            var groundMat = new Material(terrainShader) { name = "E1 / matte terrain" };
            if (groundMat.HasProperty("_Smoothness")) groundMat.SetFloat("_Smoothness", 0f);
            AssetDatabase.CreateAsset(groundMat, ArtFolder + "/E1_Ground.mat");

            var meshes = new[]
            {
                MakeRock("Rock_A_Low", 3.3f, 3.0f, 1.25f, .83f, .57f, new Vector2(-.13f, .12f),
                    new[] { 1f, .91f, 1.06f, .87f, 1.04f, .92f, 1.08f }),
                MakeRock("Rock_B_Upright", 2.0f, 2.3f, 2.1f, .81f, .47f, new Vector2(.16f, -.08f),
                    new[] { 1.03f, .85f, 1.05f, 1.12f, .93f, .88f, 1.02f }),
                MakeRock("Rock_C_Slab", 4.2f, 2.1f, .90f, .92f, .72f, new Vector2(.25f, .06f),
                    new[] { 1f, 1.12f, .95f, .90f, 1.08f, .83f, 1.02f })
            };
            for (int i = 0; i < meshes.Length; i++)
                AssetDatabase.CreateAsset(meshes[i], ArtFolder + "/" + meshes[i].name + ".asset");
            var rockMats = new Material[RockColors.Length];
            for (int i = 0; i < rockMats.Length; i++)
            {
                var mat = new Material(rockShader) { name = "E1 / stone " + (i + 1) };
                mat.SetColor("_BaseColor", Parse(RockColors[i]));
                mat.SetFloat("_Metallic", 0f);
                mat.SetFloat("_Smoothness", .03f);
                mat.enableInstancing = true;
                AssetDatabase.CreateAsset(mat, ArtFolder + "/E1_Rock" + (i + 1) + ".mat");
                rockMats[i] = mat;
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var ground = Terrain.CreateTerrainGameObject(terrainData);
            ground.name = "E1 / Terrain - sculpt here later";
            SceneManager.MoveGameObjectToScene(ground, scene);
            ground.transform.position = new Vector3(-Side * .5f, GroundOriginY, -Side * .5f);
            var terrain = ground.GetComponent<Terrain>();
            var collider = ground.GetComponent<TerrainCollider>();
            if (terrain == null || collider == null || collider.terrainData != terrainData)
                throw new InvalidOperationException("Unity Terrain or TerrainCollider missing.");
            terrain.materialTemplate = groundMat;
            terrain.drawInstanced = true;
            terrain.shadowCastingMode = ShadowCastingMode.On;
            terrain.heightmapPixelError = 3f;
            terrain.basemapDistance = 1800f;

            MakeRockGroups(scene, meshes, rockMats);

            // Use approved, existing prefabs. This map deliberately contains no
            // fighter, AA/SAM or weapons combat session: first evaluate the art.
            var aircraft = Spawn(playerPrefab, scene, "E1 / PlayerJet (current model)",
                new Vector3(0f, 48f, -220f));
            var assembly = Need<JetAssembly>(aircraft);
            var flight = Need<FlightController>(aircraft);
            if (!assembly.IsConfigured) throw new InvalidOperationException("PlayerJet has no configured chassis.");
            DisableIfPresent<WeaponController>(aircraft);
            DisableIfPresent<BoundedAim>(aircraft);
            DisableIfPresent<WeaponSelection>(aircraft);
            DisableIfPresent<GuidedMissileLauncher>(aircraft);

            var cameraRoot = Spawn(cameraPrefab, scene, "E1 / Main Camera", aircraft.transform.position + new Vector3(0f, 88f, -90f));
            cameraRoot.tag = "MainCamera";
            var viewCamera = Need<Camera>(cameraRoot);
            viewCamera.orthographic = true;
            viewCamera.orthographicSize = 80f;
            viewCamera.nearClipPlane = .3f;
            viewCamera.farClipPlane = 700f;
            cameraRoot.transform.rotation = Quaternion.Euler(50f, -45f, 0f);
            Need<TopDownCameraFollow>(cameraRoot).enabled = false;
            DisableIfPresent<CameraLookAhead>(cameraRoot);
            DisableIfPresent<TargetLock>(cameraRoot);
            DisableIfPresent<TargetLockHud>(cameraRoot);
            cameraRoot.AddComponent<HeadingLockedObliqueCameraStudy>()
                .ConfigureMapMode(aircraft.transform);
            var cameraRig = Need<CombatCameraRig>(cameraRoot);

            Spawn(lightPrefab, scene, "E1 / CombatLightingRig", Vector3.zero);
            var session = new GameObject("E1 / Flight session (no combat)");
            SceneManager.MoveGameObjectToScene(session, scene);
            session.AddComponent<E1MapFlightSession>().Configure(assembly, cameraRig, 850f);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Unable to save E1 map scene.");
            Selection.activeGameObject = ground;
            return "E1 created: " + ScenePath + ". Open/Play: A/D, W, Q/E, R. " +
                   "Native 2000m Terrain, 12 sparse rock groups, existing C3 camera/player and shared CombatLightingRig.";
        }

        private static TerrainData MakeTerrainData()
        {
            var result = new TerrainData
            {
                heightmapResolution = HeightResolution,
                alphamapResolution = PaintResolution,
                baseMapResolution = PaintResolution,
                size = new Vector3(Side, TerrainHeight, Side)
            };
            AssetDatabase.CreateAsset(result, ArtFolder + "/E1_Terrain.asset");
            var heights = new float[HeightResolution, HeightResolution];
            for (int z = 0; z < HeightResolution; z++)
            for (int x = 0; x < HeightResolution; x++)
                heights[z, x] = GroundHeight;
            result.SetHeights(0, 0, heights);

            // Single completely uniform color texture. The TerrainData remains
            // sculptable; geometry can change later without repainting textures.
            var pixels = new Texture2D(4, 4, TextureFormat.RGB24, false);
            for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++) pixels.SetPixel(x, y, GroundColor);
            pixels.Apply(false);
            var texturePath = ArtFolder + "/E1_GroundColor.png";
            System.IO.File.WriteAllBytes(texturePath, pixels.EncodeToPNG());
            Object.DestroyImmediate(pixels);
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Ground PNG failed to import.");
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();

            var layer = new TerrainLayer
            {
                name = "E1 / pure ochre (no noise)",
                diffuseTexture = Require<Texture2D>(texturePath),
                tileSize = new Vector2(160f, 160f),
                metallic = 0f,
                smoothness = 0f,
                smoothnessSource = TerrainLayerSmoothnessSource.ConstantOnly
            };
            AssetDatabase.CreateAsset(layer, ArtFolder + "/E1_Ground.terrainlayer");
            result.terrainLayers = new[] { layer };
            var alpha = new float[PaintResolution, PaintResolution, 1];
            for (int z = 0; z < PaintResolution; z++)
            for (int x = 0; x < PaintResolution; x++) alpha[z, x, 0] = 1f;
            result.SetAlphamaps(0, 0, alpha);
            EditorUtility.SetDirty(result);
            return result;
        }

        private static void MakeRockGroups(Scene scene, Mesh[] meshes, Material[] materials)
        {
            var parent = new GameObject("E1 / Sparse rock groups - move freely");
            SceneManager.MoveGameObjectToScene(parent, scene);
            for (int i = 0; i < ClusterCenters.Length; i++)
            {
                var group = new GameObject($"Rocks {i + 1:00}");
                group.transform.SetParent(parent.transform, false);
                group.transform.localPosition = new Vector3(ClusterCenters[i].x, 0f, ClusterCenters[i].y);
                // 1–3 simple forms, irregular but not random; big empty areas.
                int count = i % 4 == 0 ? 1 : i % 3 == 0 ? 3 : 2;
                for (int j = 0; j < count; j++)
                {
                    int kind = (i + j * 2) % meshes.Length;
                    var stone = new GameObject($"Rock {j + 1} / {meshes[kind].name}");
                    stone.transform.SetParent(group.transform, false);
                    var local = j == 0 ? Vector3.zero : j == 1
                        ? new Vector3(6.8f + i % 3, 0f, -4.2f)
                        : new Vector3(-6f, 0f, 5.5f + i % 4);
                    stone.transform.localPosition = local;
                    stone.transform.localRotation = Quaternion.Euler(0f, (i * 39 + j * 67) % 360, 0f);
                    float size = 1f + (i % 3) * .2f + j * .12f;
                    stone.transform.localScale = new Vector3(size, .9f + (i % 2) * .2f, size);
                    stone.AddComponent<MeshFilter>().sharedMesh = meshes[kind];
                    var meshRenderer = stone.AddComponent<MeshRenderer>();
                    meshRenderer.sharedMaterial = materials[(i + j) % materials.Length];
                    meshRenderer.shadowCastingMode = ShadowCastingMode.On;
                    meshRenderer.receiveShadows = true;
                    // Props already have physics for future ground projectiles.
                    // FlightController moves Transform, so wall collisions are E3.
                    stone.AddComponent<MeshCollider>().sharedMesh = meshes[kind];
                }
            }
        }

        private static Mesh MakeRock(string label, float xRadius, float zRadius, float height,
            float shoulderScale, float crownScale, Vector2 topOffset, float[] outline)
        {
            int n = outline.Length;
            var bottom = new Vector3[n];
            var shoulder = new Vector3[n];
            var crown = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float angle = i * Mathf.PI * 2f / n;
                float x = Mathf.Cos(angle) * outline[i] * xRadius;
                float z = Mathf.Sin(angle) * outline[i] * zRadius;
                bottom[i] = new Vector3(x, -.42f, z);
                shoulder[i] = new Vector3(x * shoulderScale + topOffset.x * .45f,
                    height * .67f, z * shoulderScale + topOffset.y * .45f);
                crown[i] = new Vector3(x * crownScale + topOffset.x,
                    height * (i % 3 == 0 ? 1.03f : .94f),
                    z * crownScale + topOffset.y);
            }
            // All major planes have deliberate hard edges, no noisy displacement.
            // Separate vertices per face produce clean flat normals under the sun.
            var vertices = new System.Collections.Generic.List<Vector3>();
            var triangles = new System.Collections.Generic.List<int>();
            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                AddQuad(vertices, triangles, bottom[i], shoulder[i], shoulder[next], bottom[next]);
                AddQuad(vertices, triangles, shoulder[i], crown[i], crown[next], shoulder[next]);
                AddTriangle(vertices, triangles, new Vector3(topOffset.x, height, topOffset.y),
                    crown[next], crown[i]);
                AddTriangle(vertices, triangles, new Vector3(0f, -.42f, 0f), bottom[i], bottom[next]);
            }
            var mesh = new Mesh { name = label };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddQuad(System.Collections.Generic.List<Vector3> verts,
            System.Collections.Generic.List<int> faces,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int first = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            faces.Add(first); faces.Add(first + 1); faces.Add(first + 2);
            faces.Add(first); faces.Add(first + 2); faces.Add(first + 3);
        }

        private static void AddTriangle(System.Collections.Generic.List<Vector3> verts,
            System.Collections.Generic.List<int> faces, Vector3 a, Vector3 b, Vector3 c)
        {
            int first = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            faces.Add(first); faces.Add(first + 1); faces.Add(first + 2);
        }

        private static GameObject Spawn(GameObject prefab, Scene scene, string name, Vector3 pos)
        {
            var result = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (result == null) throw new InvalidOperationException("Cannot instantiate prefab " + prefab.name);
            result.name = name;
            result.transform.SetPositionAndRotation(pos, Quaternion.identity);
            return result;
        }

        private static void DisableIfPresent<T>(GameObject root) where T : Behaviour
        {
            var component = root.GetComponent<T>();
            if (component != null) component.enabled = false;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int split = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, split));
            AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
        }

        private static Color Parse(string hex)
        {
            if (!ColorUtility.TryParseHtmlString(hex, out var color))
                throw new ArgumentException("Invalid color: " + hex);
            return color;
        }

        private static T Require<T>(string path) where T : Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ??
            throw new InvalidOperationException("Missing asset: " + path);
        private static T Need<T>(GameObject go) where T : Component =>
            go.GetComponent<T>() ??
            throw new InvalidOperationException("Missing component " + typeof(T).Name + " on " + go.name);
    }
}
#endif
