#if UNITY_EDITOR
using System;
using System.Collections.Generic;
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
    // B1d: one-time authoring of a *modular* road connection and softly painted
    // prepared ground. Saved meshes/prefabs and TerrainLayer are editable afterward.
    // No baked sun, fake shadows, runtime geometry or scene regeneration.
    public static class InstallBaseRoadB1D
    {
        private const string ScenePath = "Assets/_Project/Scenes/ArcadeDesertE1.unity";
        private const string TerrainDataPath = "Assets/_Project/Art/Environment/ArcadeDesertE1/E1_Terrain.asset";
        private const string ArtFolder = "Assets/_Project/Art/Environment/BaseRoadB1d";
        private const string PrefabFolder = "Assets/_Project/Prefabs/Environment/Base";
        private const string SiteName = "B1 / Roads and prepared ground";
        private static readonly Vector3 Anchor = new Vector3(-452f, 0f, -346f);

        private static readonly (string Key, string Color)[] Palette =
        {
            ("Asphalt", "#777871"),
            ("Shoulder", "#8D877A"),
            ("Scuffs", "#696B66"),
            ("Repairs", "#83837B")
        };

        private static readonly (string Name, float Width, Vector2[] Points)[] Routes =
        {
            ("Access", 9.4f, new[]
            {
                new Vector2(-454.8f, -419f), new Vector2(-453.6f, -392f),
                new Vector2(-451.8f, -369f), new Vector2(-451.0f, -347f),
                new Vector2(-452.2f, -322f), new Vector2(-452.6f, -302f)
            }),
            ("Barracks", 8.7f, new[]
            {
                new Vector2(-537f, -305.5f), new Vector2(-516f, -305.0f),
                new Vector2(-493f, -304.5f), new Vector2(-470f, -304f),
                new Vector2(-452.6f, -303.5f)
            }),
            ("HQ_Entry", 7.6f, new[]
            {
                new Vector2(-453.0f, -349.7f), new Vector2(-448.5f, -349.2f),
                new Vector2(-441.2f, -348.8f)
            })
        };

        [MenuItem("Air Combat/Art/B1d - Install Road + Prepared Ground")]
        private static void InstallFromMenu()
        {
            try { Debug.Log(Install()); }
            catch (Exception error) { Debug.LogError("B1d road/ground installation failed: " + error); }
        }

        public static string Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before installing B1d.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save or discard all open scene changes first.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                throw new InvalidOperationException("Main map scene missing. Never regenerate it.");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("B1d requires URP Lit shader.");
            var terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
            if (terrainData == null || terrainData.terrainLayers == null || terrainData.terrainLayers.Length == 0)
                throw new InvalidOperationException("Expected authored E1 terrain with its base layer.");

            // Guard every fixed output before creating or modifying anything.
            var outputNames = new List<string>
            {
                ArtFolder + "/PreparedGround.png",
                ArtFolder + "/PreparedGround.terrainlayer",
                PrefabFolder + "/B1d_WearMarks.prefab"
            };
            foreach (var color in Palette) outputNames.Add(ArtFolder + "/B1d_" + color.Key + ".mat");
            foreach (var route in Routes)
            {
                outputNames.Add(ArtFolder + "/B1d_" + route.Name + "_Asphalt.asset");
                outputNames.Add(ArtFolder + "/B1d_" + route.Name + "_Shoulder.asset");
                outputNames.Add(PrefabFolder + "/B1d_Road_" + route.Name + ".prefab");
            }
            outputNames.Add(ArtFolder + "/B1d_Scuffs.asset");
            outputNames.Add(ArtFolder + "/B1d_Repairs.asset");
            foreach (string path in outputNames)
                if (AssetDatabase.LoadAssetAtPath<Object>(path) != null || File.Exists(path))
                    throw new InvalidOperationException("B1d output already exists; never overwrite authoring: " + path);
            if (terrainData.terrainLayers.Any(l => l != null && l.name == "B1d_PreparedGround"))
                throw new InvalidOperationException("B1d prepared-ground layer is already present.");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            if (roots.Any(x => x.name == SiteName))
                throw new InvalidOperationException("B1d already installed; edit meshes and Terrain in the scene.");
            var barracks = roots.SingleOrDefault(x => x.name == "B1 / Base sector - barracks trial");
            var hq = roots.SingleOrDefault(x => x.name == "B1 / HQ and courtyard");
            var plateau = roots.SingleOrDefault(x => x.name.StartsWith("E1C / Main plateau"));
            var terrain = roots.Select(x => x.GetComponent<Terrain>()).FirstOrDefault(x => x != null);
            if (barracks == null || hq == null || plateau == null || terrain == null ||
                terrain.terrainData != terrainData || barracks.transform.childCount != 3 ||
                hq.transform.childCount < 6)
                throw new InvalidOperationException("Expected B1 barracks/HQ, plateau and source terrain not found.");
            if (Vector2.Distance(new Vector2(barracks.transform.position.x, barracks.transform.position.z),
                    new Vector2(-485f, -270f)) > 5f ||
                Vector2.Distance(new Vector2(hq.transform.position.x, hq.transform.position.z),
                    new Vector2(-422f, -320f)) > 5f)
                throw new InvalidOperationException("B1 site has been repositioned. Review road alignment instead of installing fixed coordinates.");

            EnsureFolder(ArtFolder);
            EnsureFolder(PrefabFolder);
            var materials = new Dictionary<string, Material>();
            foreach (var color in Palette)
            {
                var m = new Material(shader) { name = "B1d / " + color.Key };
                if (!ColorUtility.TryParseHtmlString(color.Color, out var albedo))
                    throw new InvalidOperationException("Invalid B1d color: " + color.Color);
                m.SetColor("_BaseColor", albedo);
                m.SetFloat("_Metallic", 0f);
                m.SetFloat("_Smoothness", 0f);
                m.enableInstancing = true;
                AssetDatabase.CreateAsset(m, ArtFolder + "/B1d_" + color.Key + ".mat");
                materials.Add(color.Key, m);
            }

            var root = new GameObject(SiteName);
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.position = Anchor;
            for (int i = 0; i < Routes.Length; i++)
            {
                var route = Routes[i];
                var path = SmoothPath(route.Points);
                Vector3 pivot = PathPivot(route.Points);
                // Edges are deliberately just a little imperfect, not a ragged brush stroke.
                var shoulder = SaveMesh("B1d_" + route.Name + "_Shoulder", Ribbon(path, route.Width + 2.6f, .044f, i + 11, pivot));
                var asphalt = SaveMesh("B1d_" + route.Name + "_Asphalt", Ribbon(path, route.Width, .083f + .008f * i, i + 17, pivot));
                var group = new GameObject("B1d_Road_" + route.Name);
                SceneManager.MoveGameObjectToScene(group, scene);
                AddSurface(group.transform, "Dust shoulders", shoulder, materials["Shoulder"], false);
                AddSurface(group.transform, "Asphalt", asphalt, materials["Asphalt"], true);
                string prefabPath = PrefabFolder + "/B1d_Road_" + route.Name + ".prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(group, prefabPath);
                Object.DestroyImmediate(group);
                if (prefab == null) throw new InvalidOperationException("Unable to save road prefab: " + prefabPath);
                Place(scene, root.transform, prefab, $"0{i + 1} / Road - {route.Name}", pivot);
            }
            var wearRoot = new GameObject("B1d_WearMarks");
            SceneManager.MoveGameObjectToScene(wearRoot, scene);
            var wearPivot = new Vector3(-479f, 0f, -347f);
            var repairMesh = SaveMesh("B1d_Repairs", CreateRepairMesh(wearPivot));
            var scuffMesh = SaveMesh("B1d_Scuffs", CreateScuffMesh(wearPivot));
            AddSurface(wearRoot.transform, "Worn repair patches", repairMesh, materials["Repairs"], false);
            AddSurface(wearRoot.transform, "Faint tire and brake traces", scuffMesh, materials["Scuffs"], false);
            var wearPrefab = PrefabUtility.SaveAsPrefabAsset(wearRoot, PrefabFolder + "/B1d_WearMarks.prefab");
            Object.DestroyImmediate(wearRoot);
            if (wearPrefab == null) throw new InvalidOperationException("Unable to save wear prefab.");
            Place(scene, root.transform, wearPrefab, "04 / Road wear - sparse", wearPivot);

            var layer = CreatePreparedGroundLayer();
            PaintPreparedGround(terrain, layer);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("B1d failed to save main map scene.");
            Selection.activeGameObject = root;
            return "B1d installed: three modular road prefabs, independently movable wear, " +
                   "softly painted TerrainLayer (no ground mesh), barracks/HQ/plateau unchanged. " +
                   "Commit the modified TerrainData + scene, generated materials/meshes/prefabs/layer/texture and .meta files.";
        }

        private static void AddSurface(Transform parent, string name, Mesh mesh, Material material, bool collidable)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; // painted road surfaces never cast floating shadows
            renderer.receiveShadows = true;
            if (collidable) go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        private static void Place(Scene scene, Transform parent, GameObject prefab, string name, Vector3 pivot)
        {
            var placed = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            placed.name = name;
            placed.transform.SetParent(parent, false);
            placed.transform.localPosition = pivot - Anchor;
            placed.transform.localRotation = Quaternion.identity;
            placed.transform.localScale = Vector3.one;
        }

        private static Mesh SaveMesh(string name, Mesh mesh)
        {
            mesh.name = name;
            AssetDatabase.CreateAsset(mesh, ArtFolder + "/" + name + ".asset");
            return mesh;
        }

        private static List<Vector2> SmoothPath(Vector2[] nodes)
        {
            var points = new List<Vector2>();
            for (int i = 0; i < nodes.Length - 1; i++)
            {
                Vector2 a = nodes[Mathf.Max(i - 1, 0)];
                Vector2 b = nodes[i];
                Vector2 c = nodes[i + 1];
                Vector2 d = nodes[Mathf.Min(i + 2, nodes.Length - 1)];
                int steps = Mathf.Max(3, Mathf.CeilToInt(Vector2.Distance(b, c) / 2f));
                for (int j = 0; j < steps; j++)
                {
                    float t = j / (float)steps;
                    float t2 = t * t;
                    float t3 = t2 * t;
                    Vector2 p = .5f * ((2f * b) + (-a + c) * t +
                                   (2f*a - 5f*b + 4f*c - d) * t2 +
                                   (-a + 3f*b - 3f*c + d) * t3);
                    points.Add(p);
                }
            }
            points.Add(nodes[nodes.Length - 1]);
            return points;
        }

        private static Vector3 PathPivot(Vector2[] points)
        {
            float minX = points.Min(p => p.x);
            float maxX = points.Max(p => p.x);
            float minZ = points.Min(p => p.y);
            float maxZ = points.Max(p => p.y);
            return new Vector3((minX + maxX) * .5f, 0f, (minZ + maxZ) * .5f);
        }

        private static Mesh Ribbon(List<Vector2> path, float width, float height, int seed, Vector3 pivot)
        {
            var verts = new Vector3[path.Count * 2];
            var uv = new Vector2[verts.Length];
            var triangles = new int[(path.Count - 1) * 6];
            float dist = 0f;
            for (int i = 0; i < path.Count; i++)
            {
                if (i > 0) dist += Vector2.Distance(path[i - 1], path[i]);
                Vector2 tangent = (path[Mathf.Min(i + 1, path.Count - 1)] -
                                   path[Mathf.Max(i - 1, 0)]).normalized;
                Vector2 lateral = new Vector2(-tangent.y, tangent.x);
                float edge = width * .5f + .10f * Mathf.Sin(dist * .39f + seed);
                Vector2 l = path[i] + lateral * edge;
                Vector2 r = path[i] - lateral * (edge + .09f * Mathf.Sin(dist * .54f + seed * 2f));
                verts[i*2] = new Vector3(l.x - pivot.x, height, l.y - pivot.z);
                verts[i*2+1] = new Vector3(r.x - pivot.x, height, r.y - pivot.z);
                uv[i*2] = new Vector2(0f, dist / 8f);
                uv[i*2+1] = new Vector2(1f, dist / 8f);
                if (i == path.Count - 1) continue;
                int t = i * 6;
                int a = i * 2;
                int b = (i + 1) * 2;
                // On a northward segment these produce +Y normals, not downward faces.
                triangles[t] = a;
                triangles[t+1] = b;
                triangles[t+2] = a+1;
                triangles[t+3] = a+1;
                triangles[t+4] = b;
                triangles[t+5] = b+1;
            }
            var mesh = new Mesh { vertices = verts, triangles = triangles, uv = uv };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // Tiny, broken repair islands: intentionally sparse and close to asphalt tone.
        private static Mesh CreateRepairMesh(Vector3 pivot)
        {
            var verts = new List<Vector3>();
            var indices = new List<int>();
            var repairs = new (float x, float z, float w, float l, float a)[]
            {
                (-453f, -393f, 2.5f, 1.4f, .2f), (-449.6f, -366f, 1.9f, 2.5f, -.12f),
                (-454.4f, -336f, 2.1f, 1.3f, .10f), (-450.3f, -322f, 1.7f, 2.3f, .05f),
                (-516f, -304.1f, 2.5f, 1.2f, -.09f), (-486f, -306.4f, 2f, 1.2f, .16f),
                (-464f, -302.4f, 1.8f, 1.1f, .10f), (-446.7f, -350.5f, 1.6f, .8f, .2f)
            };
            foreach (var (x,z,w,l,a) in repairs) AddParallelogram(verts, indices, x, z, w, l, a, .127f, pivot);
            return BuildFlatMesh(verts, indices);
        }

        private static Mesh CreateScuffMesh(Vector3 pivot)
        {
            var verts = new List<Vector3>();
            var indices = new List<int>();
            // Faint intermittent paired tire traces at a single turn + a small braking mark.
            for (int lane = 0; lane < 2; lane++)
            {
                float offset = lane == 0 ? -1.25f : 1.25f;
                for (int i = 0; i < 5; i++)
                {
                    float z = -339f + i * 3.25f;
                    AddParallelogram(verts, indices, -451.8f + offset + .3f * Mathf.Sin(i * .8f),
                        z, .15f, 1.35f + .25f * (i%2), .16f, .135f, pivot);
                }
            }
            for (int i = 0; i < 3; i++)
                AddParallelogram(verts, indices, -516f + i*4.2f, -304.6f + (i%2)*.45f,
                    2.1f, .14f, .03f, .138f, pivot);
            return BuildFlatMesh(verts, indices);
        }

        private static void AddParallelogram(List<Vector3> vertices, List<int> indices,
            float x, float z, float width, float length, float slant, float height, Vector3 pivot)
        {
            int start = vertices.Count;
            // Flat quad, not transparent effects or procedural speckle noise.
            vertices.Add(new Vector3(x - width*.5f - pivot.x, height, z - length*.5f - pivot.z));
            vertices.Add(new Vector3(x + width*.5f - pivot.x, height, z - length*.5f - pivot.z));
            vertices.Add(new Vector3(x + width*.5f + slant - pivot.x, height, z + length*.5f - pivot.z));
            vertices.Add(new Vector3(x - width*.5f + slant - pivot.x, height, z + length*.5f - pivot.z));
            indices.Add(start); indices.Add(start + 3); indices.Add(start + 1);
            indices.Add(start + 1); indices.Add(start + 3); indices.Add(start + 2);
        }

        private static Mesh BuildFlatMesh(List<Vector3> vertices, List<int> indices)
        {
            var mesh = new Mesh { vertices = vertices.ToArray(), triangles = indices.ToArray() };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static TerrainLayer CreatePreparedGroundLayer()
        {
            var tex = new Texture2D(128, 128, TextureFormat.RGBA32, true, false);
            const float tau = Mathf.PI * 2f;
            Color baseColor;
            if (!ColorUtility.TryParseHtmlString("#9D9384", out baseColor))
                throw new InvalidOperationException("Invalid prepared-ground color.");
            for (int y = 0; y < tex.height; y++)
            for (int x = 0; x < tex.width; x++)
            {
                float u = x / (float)tex.width;
                float v = y / (float)tex.height;
                // Seamlessly tiled, ultra-low-contrast long-wave breakup; no gravel pixels.
                float noise = .022f * Mathf.Sin(tau * u) * Mathf.Cos(tau * v)
                            + .013f * Mathf.Cos(tau * 2f * u + .5f) * Mathf.Sin(tau * v);
                tex.SetPixel(x, y, new Color(Mathf.Clamp01(baseColor.r + noise),
                    Mathf.Clamp01(baseColor.g + noise * .9f),
                    Mathf.Clamp01(baseColor.b + noise * .75f), 1f));
            }
            tex.Apply(true);
            string texturePath = ArtFolder + "/PreparedGround.png";
            File.WriteAllBytes(texturePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Failed to import prepared ground texture.");
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();
            var layer = new TerrainLayer
            {
                name = "B1d_PreparedGround",
                diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath),
                tileSize = new Vector2(70f, 70f),
                metallic = 0f,
                smoothness = 0f
            };
            AssetDatabase.CreateAsset(layer, ArtFolder + "/PreparedGround.terrainlayer");
            return layer;
        }

        private static void PaintPreparedGround(Terrain terrain, TerrainLayer layer)
        {
            var data = terrain.terrainData;
            var oldLayers = data.terrainLayers;
            int minX = ToAlphaX(-575f, terrain, data);
            int maxX = ToAlphaX(-363f, terrain, data);
            int minZ = ToAlphaZ(-464f, terrain, data);
            int maxZ = ToAlphaZ(-204f, terrain, data);
            int countX = maxX - minX + 1;
            int countZ = maxZ - minZ + 1;
            var previous = data.GetAlphamaps(minX, minZ, countX, countZ);
            var updated = new float[countZ, countX, oldLayers.Length+1];
            for (int z = 0; z < countZ; z++)
            for (int x = 0; x < countX; x++)
            {
                float wx = terrain.transform.position.x + (minX+x+.5f) / data.alphamapWidth * data.size.x;
                float wz = terrain.transform.position.z + (minZ+z+.5f) / data.alphamapHeight * data.size.z;
                float mask = Mathf.Max(
                    MaskRoundedRect(wx,wz,-485f,-269f,73f,47f,11f,13f),
                    MaskRoundedRect(wx,wz,-422f,-331f,43f,54f,13f,12f),
                    MaskRoundedRect(wx,wz,-453f,-373f,19f,71f,8f,11f));
                float variation = .94f + .08f * Mathf.PerlinNoise(wx*.042f+10f, wz*.042f+20f);
                float paint = Mathf.Clamp01(.82f * mask * variation);
                for (int l = 0; l < oldLayers.Length; l++)
                    updated[z, x, l] = previous[z, x, l] * (1f - paint);
                updated[z, x, oldLayers.Length] = paint;
            }
            Undo.RegisterCompleteObjectUndo(data, "B1d prepared military-base ground");
            data.terrainLayers = oldLayers.Concat(new[] { layer }).ToArray();
            data.SetAlphamaps(minX, minZ, updated);
            EditorUtility.SetDirty(data);
            terrain.Flush();
        }

        private static int ToAlphaX(float worldX, Terrain terrain, TerrainData data) => Mathf.Clamp(
            Mathf.FloorToInt((worldX - terrain.transform.position.x) / data.size.x * data.alphamapWidth),
            0, data.alphamapWidth - 1);
        private static int ToAlphaZ(float worldZ, Terrain terrain, TerrainData data) => Mathf.Clamp(
            Mathf.FloorToInt((worldZ - terrain.transform.position.z) / data.size.z * data.alphamapHeight),
            0, data.alphamapHeight - 1);

        private static float MaskRoundedRect(float x, float z, float cx, float cz,
            float halfX, float halfZ, float radius, float feather)
        {
            float dx = Mathf.Abs(x - cx) - halfX + radius;
            float dz = Mathf.Abs(z - cz) - halfZ + radius;
            float outside = Mathf.Sqrt(Mathf.Max(0f,dx)*Mathf.Max(0f,dx) +
                                       Mathf.Max(0f,dz)*Mathf.Max(0f,dz));
            float sdf = outside + Mathf.Min(Mathf.Max(dx,dz), 0f) - radius;
            // Break the perfectly geometric boundary without noisy camouflage.
            sdf += 2.1f * (Mathf.PerlinNoise(x*.043f+9f, z*.043f+3f)-.5f);
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((feather - sdf) / (2f*feather)));
        }

        private static void EnsureFolder(string path)
        {
            string current = "Assets";
            foreach (string segment in path.Substring("Assets/".Length).Split('/'))
            {
                string next = current + "/" + segment;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segment);
                current = next;
            }
        }
    }
}
#endif
