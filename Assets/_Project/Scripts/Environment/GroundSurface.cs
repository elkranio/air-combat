using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    // One scene-local height source. Explicit ground mesh only: buildings, rocks
    // and aircraft must never become the floor. Cache the static mesh once.
    internal sealed class GroundSurface
    {
        private readonly Terrain[] terrains;
        private readonly MeshHeight mesh;

        internal GroundSurface(Scene scene, MeshFilter groundMesh = null)
        {
            var localTerrains = new List<Terrain>();
            foreach (var terrain in Terrain.activeTerrains)
                if (terrain != null && terrain.gameObject.scene == scene) localTerrains.Add(terrain);
            terrains = localTerrains.ToArray();
            if (groundMesh == null) return;
            if (groundMesh.gameObject.scene != scene || groundMesh.sharedMesh == null ||
                !groundMesh.sharedMesh.isReadable)
            {
                Debug.LogWarning("GroundSurface: invalid or unreadable ground mesh.", groundMesh);
                return;
            }
            mesh = new MeshHeight(groundMesh);
        }

        internal bool TrySample(Vector3 point, out float height)
        {
            height = float.NegativeInfinity;
            bool found = false;
            if (mesh != null && mesh.TrySample(point, out float meshHeight))
            { height = meshHeight; found = true; }
            foreach (var terrain in terrains)
            {
                if (!TryTerrain(terrain, point, out float y, out _)) continue;
                height = Mathf.Max(height, y);
                found = true;
            }
            if (!found) height = 0f;
            return found;
        }

        internal float HeightOr(Vector3 point, float fallback = 0f) =>
            TrySample(point, out float y) ? y : fallback;

        // Explicit Terrain consumers retain their authored surface and bounds policy.
        internal static bool TryTerrain(Terrain terrain, Vector3 point, out float height, out Vector3 normal)
        {
            height = point.y; normal = Vector3.up;
            if (terrain == null || terrain.terrainData == null) return false;
            Vector3 origin = terrain.transform.position, size = terrain.terrainData.size;
            if (size.x <= 0f || size.z <= 0f) return false;
            float u = (point.x - origin.x) / size.x, v = (point.z - origin.z) / size.z;
            if (u < 0f || u > 1f || v < 0f || v > 1f) return false;
            height = terrain.terrainData.GetInterpolatedHeight(u, v) + origin.y;
            normal = terrain.transform.TransformDirection(terrain.terrainData.GetInterpolatedNormal(u, v)).normalized;
            return true;
        }

        internal static float TerrainHeightClamped(Terrain terrain, Vector3 point, float fallback = 0f)
        {
            if (terrain == null || terrain.terrainData == null) return fallback;
            Vector3 origin = terrain.transform.position, size = terrain.terrainData.size;
            if (size.x <= 0f || size.z <= 0f) return fallback;
            return origin.y + terrain.terrainData.GetInterpolatedHeight(
                Mathf.Clamp01((point.x - origin.x) / size.x), Mathf.Clamp01((point.z - origin.z) / size.z));
        }

        // Mesh triangles are immutable during the sortie. A spatial index avoids
        // scanning the full meadow on every wreck substep; no physics queries.
        private sealed class MeshHeight
        {
            private const float CellSize = 64f;
            private readonly Vector3[] vertices;
            private readonly int[] indices;
            private readonly Dictionary<Vector2Int, List<int>> cells = new Dictionary<Vector2Int, List<int>>();
            private static int Cell(float value) => Mathf.FloorToInt(value / CellSize);

            internal MeshHeight(MeshFilter filter)
            {
                vertices = filter.sharedMesh.vertices;
                for (int i = 0; i < vertices.Length; i++) vertices[i] = filter.transform.TransformPoint(vertices[i]);
                indices = filter.sharedMesh.triangles;
                for (int i = 0; i + 2 < indices.Length; i += 3)
                {
                    Vector3 a = vertices[indices[i]], b = vertices[indices[i+1]], c = vertices[indices[i+2]];
                    int x0 = Cell(Mathf.Min(a.x, Mathf.Min(b.x,c.x))), x1 = Cell(Mathf.Max(a.x, Mathf.Max(b.x,c.x)));
                    int z0 = Cell(Mathf.Min(a.z, Mathf.Min(b.z,c.z))), z1 = Cell(Mathf.Max(a.z, Mathf.Max(b.z,c.z)));
                    for (int x = x0; x <= x1; x++) for (int z = z0; z <= z1; z++)
                    {
                        var key = new Vector2Int(x,z);
                        if (!cells.TryGetValue(key, out var list)) cells[key] = list = new List<int>();
                        list.Add(i);
                    }
                }
            }

            internal bool TrySample(Vector3 p, out float height)
            {
                height = float.NegativeInfinity;
                if (!cells.TryGetValue(new Vector2Int(Cell(p.x), Cell(p.z)), out var list)) return false;
                foreach (int i in list)
                {
                    Vector3 a = vertices[indices[i]], b = vertices[indices[i+1]], c = vertices[indices[i+2]];
                    float den = (b.z-c.z)*(a.x-c.x) + (c.x-b.x)*(a.z-c.z);
                    if (Mathf.Abs(den) < .0001f) continue;
                    float u = ((b.z-c.z)*(p.x-c.x) + (c.x-b.x)*(p.z-c.z))/den;
                    float v = ((c.z-a.z)*(p.x-c.x) + (a.x-c.x)*(p.z-c.z))/den;
                    float w = 1f-u-v;
                    if (u < -.00001f || v < -.00001f || w < -.00001f) continue;
                    height = Mathf.Max(height, u*a.y + v*b.y + w*c.y);
                }
                return !float.IsNegativeInfinity(height);
            }
        }
    }
}
