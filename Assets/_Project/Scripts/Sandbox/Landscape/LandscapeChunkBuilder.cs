using System.Collections.Generic;
using UnityEngine;

namespace AirCombat
{
    // World-space sampling guarantees matching heights and colors across chunk borders.
    // No UnityEngine.Random calls: revisiting a chunk reproduces the same landscape.
    internal static class LandscapeChunkBuilder
    {
        public const int Size = 128;
        private const int Cells = 32;
        private const float Step = (float)Size / Cells;
        private const float WaterHeight = -0.15f;
        private const int TreeCells = 12;

        public static Mesh Build(Vector2Int tile, int seed, float treeDensity)
        {
            var originX = tile.x * (float)Size;
            var originZ = tile.y * (float)Size;
            var data = new MeshData(seed);
            var heights = new float[Cells + 1, Cells + 1];
            for (int z = 0; z <= Cells; z++)
                for (int x = 0; x <= Cells; x++)
                    heights[x, z] = Height(originX + x * Step, originZ + z * Step, seed);

            for (int z = 0; z < Cells; z++)
            {
                for (int x = 0; x < Cells; x++)
                {
                    var a = new Vector3(x * Step, heights[x, z], z * Step);
                    var b = new Vector3((x + 1) * Step, heights[x + 1, z], z * Step);
                    var c = new Vector3(x * Step, heights[x, z + 1], (z + 1) * Step);
                    var d = new Vector3((x + 1) * Step, heights[x + 1, z + 1], (z + 1) * Step);
                    var gx = tile.x * Cells + x;
                    var gz = tile.y * Cells + z;
                    if (((gx + gz) & 1) == 0)
                    {
                        AddGround(data, a, c, d, originX, originZ, gx, gz, seed);
                        AddGround(data, a, d, b, originX, originZ, gx, gz, seed + 1);
                    }
                    else
                    {
                        AddGround(data, a, c, b, originX, originZ, gx, gz, seed);
                        AddGround(data, b, c, d, originX, originZ, gx, gz, seed + 1);
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

            AddTrees(data, tile, originX, originZ, seed, treeDensity, heights);
            return data.ToMesh("Landscape " + tile.x + "," + tile.y);
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
            var hill = Noise(x, z, 0.009f, seed);
            var detail = Noise(x, z, 0.034f, seed + 13);
            // Keep all hills and tree tops well below the player's altitude of 12.
            var land = 0.65f + 3.6f * hill * hill + 0.65f * detail;
            var width = RiverHalfWidth(z, seed);
            var bank = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(width - 3f, width + 7f, RiverDistance(x, z, seed)));
            return Mathf.Lerp(-2f, land, bank);
        }

        private static void AddGround(MeshData data, Vector3 a, Vector3 b, Vector3 c,
            float originX, float originZ, int gx, int gz, int seed)
        {
            var center = (a + b + c) / 3f;
            var x = originX + center.x;
            var z = originZ + center.z;
            // The terrain seed must stay identical for both triangles; the variation
            // comes from their world positions, so patch boundaries remain coherent.
            var roll = Hash(gx, gz, seed);
            int color;
            if (center.y < 0.85f || RiverDistance(x, z, data.Seed) < RiverHalfWidth(z, data.Seed) + 3f)
                color = LandscapePalette.Bank;
            else if (Noise(x, z, 0.012f, data.Seed + 101) > 0.62f)
                color = roll < 0.72f ? LandscapePalette.Meadow : LandscapePalette.LightMeadow;
            else if (Noise(x, z, 0.018f, data.Seed + 43) > 0.56f)
                color = roll < 0.75f ? LandscapePalette.DarkGrass : LandscapePalette.Grass;
            else
                color = roll < 0.70f ? LandscapePalette.Grass : LandscapePalette.LightGrass;
            data.Triangle(a, b, c, color);
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
            private readonly List<Vector3> vertices = new List<Vector3>(8192);
            private readonly List<int>[] triangles = new List<int>[LandscapePalette.Count];
            public int Seed { get; }

            public MeshData(int seed = 42)
            {
                Seed = seed;
                for (int i = 0; i < triangles.Length; i++)
                    triangles[i] = new List<int>();
            }

            public void Triangle(Vector3 a, Vector3 b, Vector3 c, int material)
            {
                var index = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
                triangles[material].Add(index);
                triangles[material].Add(index + 1);
                triangles[material].Add(index + 2);
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(vertices);
                mesh.subMeshCount = triangles.Length;
                for (int i = 0; i < triangles.Length; i++)
                    mesh.SetTriangles(triangles[i], i, false);
                // Duplicated face vertices intentionally produce flat, faceted normals.
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                mesh.UploadMeshData(true);
                return mesh;
            }
        }
    }
}
