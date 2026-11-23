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
    // B1d3: replace the B1d/B1d2 soft terrain splat with one sharp-edged,
    // irregular, scene-toggleable ground *surface*. Roads and buildings stay put.
    // This is a one-time authored asset, never a runtime geometry generator.
    public static class InstallPreparedLotB1D3
    {
        private const string ScenePath = "Assets/_Project/Scenes/ArcadeDesertE1.unity";
        private const string TerrainPath = "Assets/_Project/Art/Environment/ArcadeDesertE1/E1_Terrain.asset";
        private const string LayerPath = "Assets/_Project/Art/Environment/BaseRoadB1d/PreparedGround.terrainlayer";
        private const string Folder = "Assets/_Project/Art/Environment/BaseRoadB1d";
        private const string MeshPath = Folder + "/B1d3_PreparedLot.asset";
        private const string MaterialPath = Folder + "/B1d3_PreparedLot.mat";
        private const string PrefabPath = "Assets/_Project/Prefabs/Environment/Base/B1d3_PreparedLot.prefab";
        private const string RootName = "B1 / Prepared lot - DISABLE to compare";
        private static readonly Vector3 Pivot = new Vector3(-470f, 0f, -330f);

        // One irregular perimeter around the WHOLE site, not three rounded
        // rectangles hugging individual buildings. Coordinates are world X,Z.
        // The south extension includes the approach road, not just the HQ lawn.
        private static readonly Vector2[] Outline =
        {
            new Vector2(-568f, -229f), new Vector2(-549f, -217f),
            new Vector2(-532f, -222f), new Vector2(-512f, -217f),
            new Vector2(-495f, -221f), new Vector2(-474f, -215f),
            new Vector2(-457f, -222f), new Vector2(-437f, -226f),
            new Vector2(-417f, -239f), new Vector2(-401f, -255f),
            new Vector2(-397f, -273f), new Vector2(-391f, -296f),
            new Vector2(-385f, -318f), new Vector2(-395f, -335f),
            new Vector2(-402f, -363f), new Vector2(-417f, -378f),
            new Vector2(-430f, -409f), new Vector2(-438f, -430f),
            new Vector2(-455f, -441f), new Vector2(-470f, -437f),
            new Vector2(-488f, -414f), new Vector2(-488f, -397f),
            new Vector2(-504f, -372f), new Vector2(-529f, -352f),
            new Vector2(-544f, -340f), new Vector2(-552f, -319f),
            new Vector2(-563f, -293f), new Vector2(-558f, -275f),
            new Vector2(-568f, -250f)
        };

        [MenuItem("Air Combat/Art/B1d3 - Install Sharp-Edge Prepared Lot")]
        private static void FromMenu()
        {
            try { Debug.Log(Install()); }
            catch (Exception e) { Debug.LogError("B1d3 prepared lot failed: " + e); }
        }

        public static string Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before installing B1d3.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save or discard open scene changes first.");

            foreach (string path in new[] { MeshPath, MaterialPath, PrefabPath })
                if (File.Exists(path) || AssetDatabase.LoadAssetAtPath<Object>(path) != null)
                    throw new InvalidOperationException("B1d3 asset already exists; never overwrite: " + path);

            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainPath);
            var preparedLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(LayerPath);
            if (data == null || preparedLayer == null || data.terrainLayers == null ||
                data.terrainLayers.Length != 2 || data.terrainLayers[1] != preparedLayer)
                throw new InvalidOperationException("Expected the original B1d/B1d2 two-layer terrain; do not overwrite hand edits.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                throw new InvalidOperationException("The authored map scene is missing; never regenerate it.");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit is unavailable.");

            // We intentionally leave the old TerrainLayer asset in place, but
            // remove its paint before placing the new single visual lot.
            const float terrainStart = -1000f;
            int minX = ToIndex(-575f, terrainStart, data.size.x, data.alphamapWidth);
            int maxX = ToIndex(-363f, terrainStart, data.size.x, data.alphamapWidth);
            int minZ = ToIndex(-464f, terrainStart, data.size.z, data.alphamapHeight);
            int maxZ = ToIndex(-204f, terrainStart, data.size.z, data.alphamapHeight);
            if (minX >= maxX || minZ >= maxZ)
                throw new InvalidOperationException("Prepared-ground painting rectangle is invalid.");
            var center = data.GetAlphamaps(ToIndex(-485f, terrainStart, data.size.x, data.alphamapWidth),
                ToIndex(-269f, terrainStart, data.size.z, data.alphamapHeight), 1, 1);
            if (center[0, 0, 1] < .70f)
                throw new InvalidOperationException("B1d ground painting was removed or changed; aborting rather than repainting it.");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            if (roots.Any(r => r.name == RootName))
                throw new InvalidOperationException("B1d3 is already in the scene.");
            if (!roots.Any(r => r.name == "B1 / Base sector - barracks trial") ||
                !roots.Any(r => r.name == "B1 / HQ and courtyard") ||
                !roots.Any(r => r.name == "B1 / Roads and prepared ground") ||
                !roots.Any(r => r.name.StartsWith("E1C / Main plateau")))
                throw new InvalidOperationException("The accepted barracks, HQ, roads or plateau are missing.");
            var terrain = roots.Select(r => r.GetComponent<Terrain>()).FirstOrDefault(t => t != null);
            if (terrain == null || terrain.terrainData != data ||
                Mathf.Abs(terrain.transform.position.x - terrainStart) > .1f ||
                Mathf.Abs(terrain.transform.position.z - terrainStart) > .1f)
                throw new InvalidOperationException("Unexpected terrain reference or position.");

            var material = new Material(shader) { name = "B1d3 / Prepared compacted soil" };
            if (!ColorUtility.TryParseHtmlString("#A38B72", out Color color))
                throw new InvalidOperationException("Invalid prepared-lot paint color.");
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0f);
            material.enableInstancing = true;
            AssetDatabase.CreateAsset(material, MaterialPath);

            var mesh = BuildMesh(terrain);
            mesh.name = "B1d3 / Hard-edged graded lot";
            AssetDatabase.CreateAsset(mesh, MeshPath);

            var master = new GameObject("B1d3_PreparedLot");
            SceneManager.MoveGameObjectToScene(master, scene);
            var mf = master.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = master.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true; // Building and aircraft cast REAL URP shadows on the lot.
            // No collider: the original TerrainCollider remains the ground.
            var prefab = PrefabUtility.SaveAsPrefabAsset(master, PrefabPath);
            Object.DestroyImmediate(master);
            if (prefab == null) throw new InvalidOperationException("Cannot save prepared-lot prefab.");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = RootName;
            instance.transform.position = Pivot;
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);

            // Remove just B1d/B1d2's old painted alpha, without changing any
            // height, other terrain layer or the rest of the 2km map.
            int nx = maxX - minX + 1;
            int nz = maxZ - minZ + 1;
            var a = data.GetAlphamaps(minX, minZ, nx, nz);
            for (int z = 0; z < nz; z++)
            for (int x = 0; x < nx; x++)
            {
                a[z, x, 0] = 1f;
                a[z, x, 1] = 0f;
            }
            Undo.RegisterCompleteObjectUndo(data, "B1d3 convert painted soil to toggleable sharp-edged lot");
            data.SetAlphamaps(minX, minZ, a);
            EditorUtility.SetDirty(data);
            terrain.Flush();
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Failed to save main scene with prepared lot.");

            Selection.activeGameObject = instance;
            return "B1d3: one irregular hard-edge lot. Toggle its root in Hierarchy to compare " +
                "prepared vs natural sand. Roads, barracks, HQ, plateau, lights and Y78 flight unchanged. " +
                "Commit scene, TerrainData, new mesh/material/prefab and .meta files.";
        }

        private static Mesh BuildMesh(Terrain terrain)
        {
            var points = new List<Vector2>(Outline);
            // XZ clockwise gives upward-facing Unity triangles (+Y).
            if (SignedArea(points) > 0f) points.Reverse();
            var indices = Triangulate(points);
            var verts = new Vector3[points.Count];
            var uv = new Vector2[points.Count];
            var data = terrain.terrainData;
            for (int i = 0; i < points.Count; i++)
            {
                float px = points[i].x;
                float pz = points[i].y;
                float tx = Mathf.Clamp01((px - terrain.transform.position.x) / data.size.x);
                float tz = Mathf.Clamp01((pz - terrain.transform.position.z) / data.size.z);
                float y = terrain.transform.position.y + data.GetInterpolatedHeight(tx, tz);
                verts[i] = new Vector3(px - Pivot.x, y + .028f - Pivot.y, pz - Pivot.z);
                uv[i] = new Vector2(px / 48f, pz / 48f);
            }
            var mesh = new Mesh { vertices = verts, triangles = indices, uv = uv.ToArray() };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static int ToIndex(float world, float start, float size, int resolution) =>
            Mathf.Clamp(Mathf.FloorToInt((world - start) / size * resolution), 0, resolution - 1);

        private static float SignedArea(List<Vector2> p)
        {
            float a = 0f;
            for (int i = 0; i < p.Count; i++)
            {
                var v = p[i];
                var n = p[(i + 1) % p.Count];
                a += v.x * n.y - n.x * v.y;
            }
            return a * .5f;
        }

        // Ear clipping supports the deliberately not-quite-convex lot outline.
        private static int[] Triangulate(List<Vector2> p)
        {
            var remaining = Enumerable.Range(0, p.Count).ToList();
            var triangles = new List<int>((p.Count - 2) * 3);
            int limit = p.Count * p.Count;
            while (remaining.Count > 3 && --limit > 0)
            {
                bool found = false;
                for (int i = 0; i < remaining.Count; i++)
                {
                    int a = remaining[(i + remaining.Count - 1) % remaining.Count];
                    int b = remaining[i];
                    int c = remaining[(i + 1) % remaining.Count];
                    if (Cross(p[b] - p[a], p[c] - p[b]) >= -.00001f) continue;
                    bool blocked = false;
                    foreach (int d in remaining)
                    {
                        if (d == a || d == b || d == c) continue;
                        if (InsideClockwiseTriangle(p[d], p[a], p[b], p[c]))
                        {
                            blocked = true;
                            break;
                        }
                    }
                    if (blocked) continue;
                    triangles.Add(a); triangles.Add(b); triangles.Add(c);
                    remaining.RemoveAt(i);
                    found = true;
                    break;
                }
                if (!found) break;
            }
            if (remaining.Count != 3)
                throw new InvalidOperationException("Prepared-lot perimeter is invalid (cannot triangulate).");
            triangles.Add(remaining[0]); triangles.Add(remaining[1]); triangles.Add(remaining[2]);
            return triangles.ToArray();
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        private static bool InsideClockwiseTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c) =>
            Cross(b - a, p - a) <= 0f && Cross(c - b, p - b) <= 0f &&
            Cross(a - c, p - c) <= 0f;
    }
}
#endif
