using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UI;

namespace AirCombat
{
    // Explicit opt-in for authored, stationary, closed rock meshes. Slice the
    // combined geometry (including material seams), not the renderer bounds.
    [DisallowMultipleComponent]
    public sealed class RadarTerrainObstacle : MonoBehaviour
    {
        internal static readonly List<RadarTerrainObstacle> Active = new List<RadarTerrainObstacle>();
        internal static int Revision { get; private set; }
        internal readonly List<Vector2> Edges = new List<Vector2>(); // pairs, world XZ
        internal readonly List<Vector2> Fill = new List<Vector2>(); // triangles, world XZ
        private readonly List<Vector3> triangles = new List<Vector3>();
        private float cachedHeight = float.NaN;
        private bool warned;
        private Bounds bounds;
        private readonly List<float> heights = new List<float>();
        private readonly List<SliceHit> crossings = new List<SliceHit>();
        private struct SliceHit : System.IComparable<SliceHit>
        {
            public int Edge;
            public float X;
            public int CompareTo(SliceHit other) => X.CompareTo(other.X);
        }

        internal bool ContainsHeight(float altitude) => triangles.Count != 0 &&
            altitude + .001f >= bounds.min.y && altitude + .001f < bounds.max.y;

        internal bool IsNear(Vector2 origin, float distance, float altitude)
        {
            if (!ContainsHeight(altitude)) return false;
            Vector3 min = bounds.min, max = bounds.max;
            float dx = Mathf.Max(0f, Mathf.Max(min.x - origin.x, origin.x - max.x));
            float dz = Mathf.Max(0f, Mathf.Max(min.z - origin.y, origin.y - max.z));
            return dx * dx + dz * dz <= distance * distance;
        }

        private void OnEnable()
        {
            triangles.Clear();
            Edges.Clear(); Fill.Clear(); cachedHeight = float.NaN;
            warned = false;
            foreach (var filter in GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh;
                if (mesh == null || !mesh.isReadable)
                {
                    Debug.LogWarning("Terrain radar requires readable closed meshes.", this);
                    triangles.Clear(); return;
                }
                var vertices = mesh.vertices;
                foreach (int index in mesh.triangles)
                    triangles.Add(filter.transform.TransformPoint(vertices[index]));
            }
            if (triangles.Count == 0) return;
            bounds = new Bounds(triangles[0], Vector3.zero);
            foreach (var vertex in triangles) bounds.Encapsulate(vertex);
            if (!Active.Contains(this)) { Active.Add(this); Revision++; }
        }

        private void OnDisable() { if (Active.Remove(this)) Revision++; }

        internal void Slice(float altitude)
        {
            if (!float.IsNaN(cachedHeight) && Mathf.Abs(cachedHeight - altitude) < .001f) return;
            cachedHeight = altitude;
            Edges.Clear(); Fill.Clear();
            if (!ContainsHeight(altitude)) return;
            // Sample infinitesimally above the plane to resolve vertex-on-plane cases.
            float h = altitude + .001f;
            for (int i = 0; i < triangles.Count; i += 3)
            {
                Vector2 a = default, b = default;
                int count = 0;
                for (int j = 0; j < 3; j++)
                {
                    Vector3 p = triangles[i + j], q = triangles[i + (j + 1) % 3];
                    if ((p.y > h) == (q.y > h)) continue;
                    // Canonical direction: shared triangle edges produce exactly
                    // the same float intersection even with reversed winding.
                    if (p.y > q.y) { Vector3 t = p; p = q; q = t; }
                    Vector3 hit = Vector3.LerpUnclamped(p, q, (h - p.y) / (q.y - p.y));
                    if (count++ == 0) a = new Vector2(hit.x, hit.z);
                    else b = new Vector2(hit.x, hit.z);
                }
                if (count == 2 && (a - b).sqrMagnitude > 1e-10f)
                { Edges.Add(a); Edges.Add(b); }
            }
            // Even-odd scan strips between each contour vertex. This handles
            // concave outlines and multiple loops without a convex hull or fan.
            heights.Clear();
            foreach (var p in Edges) heights.Add(p.y);
            heights.Sort();
            for (int i = 1; i < heights.Count; i++)
            {
                float lo = heights[i - 1], hi = heights[i];
                if (hi - lo < .0005f) continue;
                float mid = (lo + hi) * .5f;
                crossings.Clear();
                for (int e = 0; e < Edges.Count; e += 2)
                {
                    Vector2 a = Edges[e], b = Edges[e + 1];
                    if (mid > Mathf.Min(a.y, b.y) && mid < Mathf.Max(a.y, b.y))
                        crossings.Add(new SliceHit { Edge = e, X = At(e, mid) });
                }
                crossings.Sort();
                if (crossings.Count % 2 != 0)
                {
                    if (!warned) Debug.LogWarning("Terrain radar: open or inconsistent section; outline only.", this);
                    warned = true; Fill.Clear(); return;
                }
                for (int j = 0; j < crossings.Count; j += 2)
                {
                    int l = crossings[j].Edge, r = crossings[j + 1].Edge;
                    Vector2 a = new Vector2(At(l, lo), lo), b = new Vector2(At(r, lo), lo);
                    Vector2 c = new Vector2(At(r, hi), hi), d = new Vector2(At(l, hi), hi);
                    Fill.Add(a); Fill.Add(b); Fill.Add(c);
                    Fill.Add(a); Fill.Add(c); Fill.Add(d);
                }
            }
        }

        private float At(int edge, float z)
        {
            Vector2 a = Edges[edge], b = Edges[edge + 1];
            return Mathf.LerpUnclamped(a.x, b.x, (z - a.y) / (b.y - a.y));
        }
    }

    // Render the filled section and its actual boundary under the radar grid,
    // threat rings and contact glyphs. Clip all geometry to the circular display.
    internal static class TerrainRadarOverlay
    {
        private const int Sides = 48;
        private static readonly ProfilerMarker DrawMarker = new ProfilerMarker("AirCombat.TerrainRadar.Draw");
        private static readonly Vector2[] Normals = MakeNormals();
        private static readonly float ClipScale = Mathf.Cos(Mathf.PI / Sides);
        private static readonly List<Vector2> BufferA = new List<Vector2>(64);
        private static readonly List<Vector2> BufferB = new List<Vector2>(64);

        internal static void Draw(VertexHelper mesh, AircraftRadar sensor, Vector2 center,
            float radius, Vector2 right, Vector2 up, float opacity)
        {
            using (DrawMarker.Auto()) DrawCore(mesh, sensor, center, radius, right, up, opacity);
        }

        private static void DrawCore(VertexHelper mesh, AircraftRadar sensor, Vector2 center,
            float radius, Vector2 right, Vector2 up, float opacity)
        {
            if (RadarTerrainObstacle.Active.Count == 0 || radius < 3f) return;
            Vector3 origin = sensor.transform.position;
            Vector2 originXZ = new Vector2(origin.x, origin.z);
            float scale = radius / sensor.Range;
            float clipRadius = radius - 1.5f;
            Color32 fill = new Color(.66f, .61f, .54f, .70f * opacity);
            Color32 edge = new Color(.86f, .81f, .72f, .92f * opacity);
            if (TerrainSectionUnion.TryGet(sensor.gameObject.scene, origin.y))
                DrawSection(TerrainSectionUnion.Fill, TerrainSectionUnion.Edges);
            else
            {
                foreach (var obstacle in RadarTerrainObstacle.Active)
                {
                    if (obstacle == null || obstacle.gameObject.scene != sensor.gameObject.scene ||
                        !obstacle.IsNear(originXZ, sensor.Range, origin.y)) continue;
                    obstacle.Slice(origin.y);
                    DrawSection(obstacle.Fill, obstacle.Edges);
                }
            }

            void DrawSection(List<Vector2> points, List<Vector2> edges)
            {
                for (int i = 0; i < points.Count; i += 3)
                {
                    Vector2 a = points[i] - originXZ, b = points[i+1] - originXZ, c = points[i+2] - originXZ;
                    if (SameOutside(a.x,b.x,c.x,c.x,sensor.Range) || SameOutside(a.y,b.y,c.y,c.y,sensor.Range)) continue;
                    DrawPolygon(mesh, center, clipRadius, fill, Project(points[i]), Project(points[i+1]), Project(points[i+2]), default, false);
                }
                points = edges;
                for (int i = 0; i < points.Count; i += 2)
                {
                    Vector2 wa = points[i] - originXZ, wb = points[i+1] - originXZ;
                    if (SameOutside(wa.x,wb.x,wb.x,wb.x,sensor.Range) || SameOutside(wa.y,wb.y,wb.y,wb.y,sensor.Range)) continue;
                    Vector2 a = Project(points[i]), b = Project(points[i+1]);
                    Vector2 delta = b - a;
                    if (delta.sqrMagnitude < 1e-8f) continue;
                    Vector2 n = new Vector2(-delta.y, delta.x).normalized * .6f;
                    DrawPolygon(mesh, center, clipRadius, edge, a-n, b-n, b+n, a+n, true);
                }
            }
            Vector2 Project(Vector2 p)
            {
                Vector2 d = p - originXZ;
                return new Vector2(Vector2.Dot(d, right), Vector2.Dot(d, up)) * scale;
            }
        }

        private static Vector2[] MakeNormals()
        {
            var result = new Vector2[Sides];
            for (int i = 0; i < Sides; i++)
            {
                float angle = (i + .5f) * 2f * Mathf.PI / Sides;
                result[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }
            return result;
        }

        private static void DrawPolygon(VertexHelper mesh, Vector2 center, float radius, Color32 tint,
            Vector2 a, Vector2 b, Vector2 c, Vector2 d, bool quad)
        {
            // Fast reject against the enclosing square, then convex disc clipping.
            if (SameOutside(a.x,b.x,c.x,quad?d.x:c.x,radius) ||
                SameOutside(a.y,b.y,c.y,quad?d.y:c.y,radius)) return;
            float limit = radius * ClipScale;
            float limitSq = limit * limit;
            // Most visible triangles and edge quads are entirely inside the
            // inscribed circle. Don't run 48 clip planes on those primitives.
            if (a.sqrMagnitude <= limitSq && b.sqrMagnitude <= limitSq &&
                c.sqrMagnitude <= limitSq && (!quad || d.sqrMagnitude <= limitSq))
            {
                int index = mesh.currentVertCount;
                mesh.AddVert(center + a, tint, Vector2.zero);
                mesh.AddVert(center + b, tint, Vector2.zero);
                mesh.AddVert(center + c, tint, Vector2.zero);
                mesh.AddTriangle(index, index + 1, index + 2);
                if (quad)
                {
                    mesh.AddVert(center + d, tint, Vector2.zero);
                    mesh.AddTriangle(index, index + 2, index + 3);
                }
                return;
            }
            var input = BufferA; var output = BufferB;
            input.Clear(); input.Add(a); input.Add(b); input.Add(c); if (quad) input.Add(d);
            foreach (var normal in Normals)
            {
                output.Clear();
                Vector2 prev = input[input.Count - 1];
                float pd = limit - Vector2.Dot(prev, normal);
                foreach (var p in input)
                {
                    float dist = limit - Vector2.Dot(p, normal);
                    if ((dist >= 0f) != (pd >= 0f))
                        output.Add(Vector2.LerpUnclamped(prev, p, pd / (pd - dist)));
                    if (dist >= 0f) output.Add(p);
                    prev = p; pd = dist;
                }
                if (output.Count < 3) return;
                var swap = input; input = output; output = swap;
            }
            int first = mesh.currentVertCount;
            foreach (var p in input)
                mesh.AddVert(center + p, tint, Vector2.zero);
            for (int i = 1; i + 1 < input.Count; i++)
                mesh.AddTriangle(first, first+i, first+i+1);
        }

        private static bool SameOutside(float a, float b, float c, float d, float r) =>
            (a > r && b > r && c > r && d > r) || (a < -r && b < -r && c < -r && d < -r);
    }
}
