using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;

namespace AirCombat
{
    // World-space sampling guarantees matching heights and colors across chunk borders.
    // No UnityEngine.Random calls: revisiting a chunk reproduces the same landscape.
    internal static class LandscapeChunkBuilder
    {
        public const int Size = 128;
        private const int Cells = 48;
        private const float Step = (float)Size / Cells;
        private const float WaterHeight = -0.15f;
        private const int TreeCells = 12;
        private static readonly ProfilerMarker PaintMarker = new ProfilerMarker("Landscape.GroundPaint");
        private static readonly ProfilerMarker GeometryMarker = new ProfilerMarker("Landscape.Geometry");
        private static readonly ProfilerMarker PropsMarker = new ProfilerMarker("Landscape.Props");
        private static readonly ProfilerMarker UploadMarker = new ProfilerMarker("Landscape.Upload");

        public static Mesh Build(Vector2Int tile, int seed, float treeDensity, Transform owner, GameObject[] trees, GameObject[] rocks, GameObject[] bushes, Material[] materials, out Texture2D groundTexture)
        {
            var originX = tile.x * (float)Size;
            var originZ = tile.y * (float)Size;
            var data = new MeshData(seed);
            LandscapeGroundPaint paint;
            using (PaintMarker.Auto())
                paint = new LandscapeGroundPaint(originX, originZ, (x, z) => GroundTint(x, z, seed));
            var heights = new float[Cells + 1, Cells + 1];
            using (GeometryMarker.Auto())
            {
                // Sample a one-vertex halo once, rather than evaluating Height five
                // times per vertex. Adjacent chunks use the same world-space samples.
                var halo = new float[Cells + 3, Cells + 3];
                for (int z = -1; z <= Cells + 1; z++)
                for (int x = -1; x <= Cells + 1; x++)
                    halo[x + 1, z + 1] = Height(originX + x * Step, originZ + z * Step, seed);

                for (int z = 0; z <= Cells; z++)
                for (int x = 0; x <= Cells; x++)
                {
                    float height = halo[x + 1, z + 1];
                    heights[x, z] = height;
                    var normal = new Vector3(halo[x, z + 1] - halo[x + 2, z + 1], 2f * Step,
                        halo[x + 1, z] - halo[x + 1, z + 2]).normalized;
                    data.GroundVertex(new Vector3(x * Step, height, z * Step), normal);
                }

                // One vertex per grid point, with the same triangle winding/diagonals.
                for (int z = 0; z < Cells; z++)
                for (int x = 0; x < Cells; x++)
                {
                    int a = z * (Cells + 1) + x;
                    int b = a + 1, c = a + Cells + 1, d = c + 1;
                    int gx = tile.x * Cells + x, gz = tile.y * Cells + z;
                    if (((gx + gz) & 1) == 0)
                    {
                        data.GroundTriangle(a, c, d);
                        data.GroundTriangle(a, d, b);
                    }
                    else
                    {
                        data.GroundTriangle(a, c, b);
                        data.GroundTriangle(b, c, d);
                    }
                }
            }

            // A flat water surface intersects the carved river bed. It stays entirely
            // below the dry terrain, and neighboring tiles share the same water height.
            var wa = new Vector3(0f, WaterHeight, 0f);
            var wb = new Vector3(Size, WaterHeight, 0f);
            var wc = new Vector3(0f, WaterHeight, Size);
            var wd = new Vector3(Size, WaterHeight, Size);
            data.Triangle(wa, wc, wb, LandscapePalette.Water);
            data.Triangle(wb, wc, wd, LandscapePalette.Water);

            using (PropsMarker.Auto())
            {
                if (trees != null && trees.Length > 0)
                    AddProps(tile, seed, treeDensity, heights, owner, trees, rocks, bushes, materials, paint);
                else
                    AddTrees(data, tile, originX, originZ, seed, treeDensity, heights);
            }
            using (UploadMarker.Auto())
            {
                groundTexture = paint.ToTexture("Ground color " + tile.x + "," + tile.y);
                return data.ToMesh("Landscape " + tile.x + "," + tile.y);
            }
        }

        private static float RiverDistance(float x, float z, int seed)
        {
            var phase = (seed - 42f) * 0.013f;
            var center = 48f + 37f * Mathf.Sin(z / 90f + phase)
                             + 18f * Mathf.Sin(z / 40f - phase * 0.7f);
            return Mathf.Abs(x - center);
        }

        private static float RiverHalfWidth(float z, int seed)
        {
            return 9f + 3f * Noise(0f, z, 0.008f, seed + 71);
        }

        private static float Height(float x, float z, int seed)
        {
            return ApplyRiver(LandHeight(x, z, seed), RiverDistance(x, z, seed), RiverHalfWidth(z, seed));
        }

        private static float LandHeight(float x, float z, int seed)
        {
            var hill = Noise(x, z, 0.009f, seed);
            var detail = Noise(x, z, 0.034f, seed + 13);
            // Taller hills, with smooth normals; props are capped below flight altitude.
            return 0.65f + 6.4f * hill * hill + 1.25f * detail;
        }

        private static float ApplyRiver(float land, float distance, float width)
        {
            var bank = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(width - 3f, width + 7f, distance));
            return Mathf.Lerp(-2f, land, bank);
        }

        private static Color GroundTint(float x, float z, int seed)
        {
            // Blend broad biome colors continuously, in world space, rather than per cell.
            var forest = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.43f, 0.69f, Noise(x, z, 0.018f, seed + 43)));
            var meadow = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.52f, 0.73f, Noise(x, z, 0.012f, seed + 101)));
            Color color = Color.Lerp(new Color32(124, 154, 101, 255), new Color32(91, 126, 87, 255), forest);
            color = Color.Lerp(color, new Color32(165, 166, 112, 255), meadow);
            float distance = RiverDistance(x, z, seed), width = RiverHalfWidth(z, seed);
            var bank = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 6f, distance - width));
            color = Color.Lerp(color, new Color32(166, 158, 119, 255), bank);
            // Gentle height/detail tint keeps the hills readable from an overhead camera.
            float heightTint = Mathf.Lerp(0.92f, 1.09f,
                Mathf.Clamp01(ApplyRiver(LandHeight(x, z, seed), distance, width) / 6f));
            float detail = Mathf.Lerp(0.96f, 1.04f, Noise(x, z, 0.052f, seed + 111));
            return new Color(color.r * heightTint * detail, color.g * heightTint * detail,
                color.b * heightTint * detail, 1f);
        }

        private static void AddProps(Vector2Int tile, int seed, float density, float[,] heights,
            Transform owner, GameObject[] trees, GameObject[] rocks, GameObject[] bushes, Material[] materials,
            LandscapeGroundPaint paint)
        {
            const int cells = 12;
            const float spacing = (float)Size / cells;
            for (int z = 0; z < cells; z++)
            for (int x = 0; x < cells; x++)
            {
                int gx = tile.x * cells + x, gz = tile.y * cells + z;
                float lx = (x + Mathf.Lerp(0.32f, 0.68f, Hash(gx, gz, seed + 300))) * spacing;
                float lz = (z + Mathf.Lerp(0.32f, 0.68f, Hash(gx, gz, seed + 301))) * spacing;
                float wx = tile.x * (float)Size + lx, wz = tile.y * (float)Size + lz;
                if (RiverDistance(wx, wz, seed) < RiverHalfWidth(wz, seed) + 9f) continue;
                float forest = Noise(wx, wz, 0.018f, seed + 43);
                float meadow = Noise(wx, wz, 0.012f, seed + 101);
                float roll = Hash(gx, gz, seed + 302);
                GameObject[] options;
                int kind;
                float size;
                // High-density groves separated by open meadows.
                float grove = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.48f, 0.68f, forest));
                if (meadow < 0.64f && roll < grove * 0.90f * density)
                {
                    options = trees; kind = 0; size = Mathf.Lerp(6.0f, 8.3f, Hash(gx, gz, seed + 304));
                }
                else if (roll < 0.045f * density)
                {
                    options = rocks; kind = 1; size = Mathf.Lerp(3.4f, 5.0f, Hash(gx, gz, seed + 304));
                }
                else if (forest > 0.48f && roll < 0.12f * density)
                {
                    options = bushes; kind = 2; size = Mathf.Lerp(2.3f, 3.7f, Hash(gx, gz, seed + 304));
                }
                else continue;
                if (options == null || options.Length == 0) continue;
                var model = options[Mathf.Min(options.Length - 1, (int)(Hash(gx, gz, seed + 305) * options.Length))];
                var point = new Vector3(lx, SurfaceHeight(tile, lx, lz, heights) - 0.10f, lz);
                // The flight plane is at y=12; the largest dimension bounds prop height.
                size = Mathf.Min(size, 10.8f - point.y);
                if (size <= 0f) continue;
                if (LandscapePropSpawner.Spawn(model, owner, point, Hash(gx, gz, seed + 306) * 360f, size, kind, materials))
                    paint.AddShadow(lx, lz, size, kind == 0);
            }
        }

        private static void AddTrees(MeshData data, Vector2Int tile, float originX, float originZ,
            int seed, float density, float[,] heights)
        {
            var spacing = (float)Size / TreeCells;
            for (int z = 0; z < TreeCells; z++)
            {
                for (int x = 0; x < TreeCells; x++)
                {
                    var gx = tile.x * TreeCells + x;
                    var gz = tile.y * TreeCells + z;
                    var lx = (x + Mathf.Lerp(0.28f, 0.72f, Hash(gx, gz, seed + 201))) * spacing;
                    var lz = (z + Mathf.Lerp(0.28f, 0.72f, Hash(gx, gz, seed + 202))) * spacing;
                    var wx = originX + lx;
                    var wz = originZ + lz;
                    if (RiverDistance(wx, wz, seed) < RiverHalfWidth(wz, seed) + 9f)
                        continue;
                    if (Noise(wx, wz, 0.012f, seed + 101) > 0.62f)
                        continue; // keep the golden meadows open

                    var forest = Noise(wx, wz, 0.018f, seed + 43);
                    var chance = Mathf.Lerp(0.025f, 0.75f, Mathf.InverseLerp(0.50f, 0.70f, forest));
                    if (Hash(gx, gz, seed + 203) >= chance * density)
                        continue;

                    // Interpolate on the actual rendered triangles, not the smooth noise,
                    // so trunks sit on the mesh rather than floating over it.
                    var y = SurfaceHeight(tile, lx, lz, heights) - 0.12f;
                    var radius = Mathf.Lerp(1.45f, 2.5f, Hash(gx, gz, seed + 204));
                    var height = Mathf.Lerp(2.7f, 4.2f, Hash(gx, gz, seed + 205));
                    var rotation = Hash(gx, gz, seed + 206) * Mathf.PI * 2f;
                    var p = new Vector3(lx, y, lz);
                    AddTree(data, p, radius, height, rotation, Hash(gx, gz, seed + 207) < 0.55f);
                }
            }
        }

        private static float SurfaceHeight(Vector2Int tile, float x, float z, float[,] h)
        {
            int ix = Mathf.Min(Cells - 1, Mathf.FloorToInt(x / Step));
            int iz = Mathf.Min(Cells - 1, Mathf.FloorToInt(z / Step));
            float u = x / Step - ix;
            float v = z / Step - iz;
            var a = h[ix, iz];
            var b = h[ix + 1, iz];
            var c = h[ix, iz + 1];
            var d = h[ix + 1, iz + 1];
            if (((tile.x * Cells + ix + tile.y * Cells + iz) & 1) == 0)
                return v >= u ? a * (1f - v) + c * (v - u) + d * u
                              : a * (1f - u) + d * v + b * (u - v);
            return u + v <= 1f ? a * (1f - u - v) + b * u + c * v
                               : b * (1f - v) + c * (1f - u) + d * (u + v - 1f);
        }

        private static void AddTree(MeshData data, Vector3 p, float radius, float height,
            float rotation, bool pine)
        {
            const int sides = 6;
            // Simple trunk + faceted crown. The crown is the main readable shape from above.
            AddRing(data, p, 0f, 0.24f, height * 0.55f, 0.18f, rotation,
                sides, LandscapePalette.Bark, LandscapePalette.Bark);
            if (pine)
            {
                AddRing(data, p, height * 0.18f, radius, height * 0.80f, 0f, rotation,
                    sides, LandscapePalette.Pine, LandscapePalette.PineLight);
                AddRing(data, p, height * 0.46f, radius * 0.72f, height, 0f, rotation,
                    sides, LandscapePalette.Pine, LandscapePalette.PineLight);
            }
            else
            {
                AddRing(data, p, height * 0.26f, radius * 0.52f, height * 0.57f, radius,
                    rotation, sides, LandscapePalette.Leaf, LandscapePalette.LeafLight);
                AddRing(data, p, height * 0.57f, radius, height, 0f, rotation,
                    sides, LandscapePalette.Leaf, LandscapePalette.LeafLight);
            }
        }

        private static void AddRing(MeshData data, Vector3 p, float y0, float r0,
            float y1, float r1, float rotation, int sides, int dark, int light)
        {
            for (int i = 0; i < sides; i++)
            {
                var theta = rotation + i * Mathf.PI * 2f / sides;
                var next = rotation + ((i + 1) % sides) * Mathf.PI * 2f / sides;
                var a = p + new Vector3(Mathf.Cos(theta) * r0, y0, Mathf.Sin(theta) * r0);
                var b = p + new Vector3(Mathf.Cos(next) * r0, y0, Mathf.Sin(next) * r0);
                var c = p + new Vector3(Mathf.Cos(theta) * r1, y1, Mathf.Sin(theta) * r1);
                var d = p + new Vector3(Mathf.Cos(next) * r1, y1, Mathf.Sin(next) * r1);
                var color = (i == 0 || i == 3) ? light : dark;
                if (r1 > 0f)
                    data.Triangle(a, c, d, color);
                data.Triangle(a, d, b, color);
            }
        }

        private static float Noise(float x, float z, float scale, int seed)
        {
            // Offsets provide a deterministic seed without touching global random state.
            var sx = 137f + Hash(seed, 17, 991) * 1000f;
            var sz = 251f + Hash(23, seed, 773) * 1000f;
            return Mathf.Clamp01(Mathf.PerlinNoise(x * scale + sx, z * scale + sz));
        }

        private static float Hash(int x, int z, int seed)
        {
            unchecked
            {
                uint h = (uint)x * 0x9E3779B9u ^ (uint)z * 0x85EBCA6Bu ^ (uint)seed * 0xC2B2AE35u;
                h ^= h >> 16;
                h *= 0x7FEB352Du;
                h ^= h >> 15;
                h *= 0x846CA68Bu;
                h ^= h >> 16;
                return (h & 0x00FFFFFFu) / 16777216f;
            }
        }

        private sealed class MeshData
        {
            private readonly List<Vector3> vertices = new List<Vector3>((Cells + 1) * (Cells + 1) + 6);
            private readonly List<Vector3> normals = new List<Vector3>((Cells + 1) * (Cells + 1) + 6);
            private readonly List<Vector2> uvs = new List<Vector2>((Cells + 1) * (Cells + 1) + 6);
            private readonly List<int>[] triangles = new List<int>[LandscapePalette.Count];
            public int Seed { get; }

            public MeshData(int seed = 42)
            {
                Seed = seed;
                for (int i = 0; i < triangles.Length; i++)
                    triangles[i] = new List<int>(i == LandscapePalette.Grass ? Cells * Cells * 6 : 0);
            }

            public void Triangle(Vector3 a, Vector3 b, Vector3 c, int material)
            {
                var index = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
                var normal = Vector3.Cross(b - a, c - a).normalized;
                normals.Add(normal); normals.Add(normal); normals.Add(normal);
                uvs.Add(LandscapeGroundPaint.Uv(a));
                uvs.Add(LandscapeGroundPaint.Uv(b));
                uvs.Add(LandscapeGroundPaint.Uv(c));
                triangles[material].Add(index);
                triangles[material].Add(index + 1);
                triangles[material].Add(index + 2);
            }

            public void GroundVertex(Vector3 point, Vector3 normal)
            {
                vertices.Add(point);
                normals.Add(normal);
                uvs.Add(LandscapeGroundPaint.Uv(point));
            }

            public void GroundTriangle(int a, int b, int c)
            {
                var indices = triangles[LandscapePalette.Grass];
                indices.Add(a);
                indices.Add(b);
                indices.Add(c);
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(vertices);
                mesh.subMeshCount = triangles.Length;
                for (int i = 0; i < triangles.Length; i++)
                    mesh.SetTriangles(triangles[i], i, false);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.RecalculateBounds();
                mesh.UploadMeshData(true);
                return mesh;
            }
        }
    }
}
