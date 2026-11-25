using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    // One cached union for stationary authored obstacles. Exact source section
    // segments, not a raster approximation, hull, or XOR of overlapping rocks.
    // During altitude changes the HUD uses its culled per-rock path instead.
    internal static class TerrainSectionUnion
    {
        private const float StableSeconds = .25f;
        private const float HeightTolerance = .001f;
        private const double EventEpsilon = .000001;
        private const double EdgeEpsilon = .001;
        private static readonly ProfilerMarker BuildMarker = new ProfilerMarker("AirCombat.TerrainSectionUnion.Build");
        internal static readonly List<Vector2> Edges = new List<Vector2>();
        internal static readonly List<Vector2> Fill = new List<Vector2>();
        private static Scene cachedScene;
        private static int version = -1;
        private static float height = float.NaN, since;
        private static bool attempted, ready;

        // Called by warning: never triggers a rebuild or uses stale geometry.
        internal static bool IsCached(Scene scene, float altitude) => ready &&
            cachedScene == scene && version == RadarTerrainObstacle.Revision &&
            Mathf.Abs(height - altitude) < HeightTolerance;

        // Called by the radar; only build once after altitude and membership settle.
        internal static bool TryGet(Scene scene, float altitude)
        {
            if (IsCached(scene, altitude)) return true;
            float now = Time.unscaledTime;
            if (cachedScene != scene || version != RadarTerrainObstacle.Revision ||
                float.IsNaN(height) || Mathf.Abs(height - altitude) >= HeightTolerance || now < since)
            {
                cachedScene = scene; version = RadarTerrainObstacle.Revision;
                height = altitude; since = now; ready = attempted = false;
                Edges.Clear(); Fill.Clear();
            }
            if (attempted || now - since < StableSeconds) return false;
            attempted = true;
            using (BuildMarker.Auto())
                ready = Build(scene, height);
            if (!ready)
            {
                Edges.Clear(); Fill.Clear();
                Debug.LogWarning("Terrain section union: inconsistent input; using per-rock radar sections.");
            }
            return ready;
        }

        private struct Edge
        {
            public double X, Z, EndX, EndZ, Slope;
            public int Solid;
            public Edge(Vector2 a, Vector2 b, int solid)
            {
                if (a.y > b.y) { var t = a; a = b; b = t; }
                X = a.x; Z = a.y; EndX = b.x; EndZ = b.y;
                Slope = (EndX - X) / (EndZ - Z);
                Solid = solid;
            }
            public double At(double z) => X + (z - Z) * Slope;
            public double MinX => Math.Min(X, EndX);
            public double MaxX => Math.Max(X, EndX);
        }

        private struct Hit : IComparable<Hit>
        {
            public double X;
            public int Index;
            public int CompareTo(Hit other)
            {
                int order = X.CompareTo(other.X);
                return order == 0 ? Index.CompareTo(other.Index) : order;
            }
        }

        private struct Span
        {
            public int Left, Right;
            public double Start;
            public Span(int l, int r, double start) { Left = l; Right = r; Start = start; }
        }

        private static bool Build(Scene scene, float altitude)
        {
            var edges = new List<Edge>();
            var events = new List<double>();
            int solids = 0;
            foreach (var rock in RadarTerrainObstacle.Active)
            {
                if (rock == null || rock.gameObject.scene != scene || !rock.ContainsHeight(altitude)) continue;
                rock.Slice(altitude);
                if (rock.Edges.Count == 0) continue;
                if (rock.Fill.Count == 0) return false; // don't hide a malformed section
                for (int i = 0; i < rock.Edges.Count; i += 2)
                {
                    Vector2 a = rock.Edges[i], b = rock.Edges[i + 1];
                    events.Add(a.y); events.Add(b.y);
                    if (Math.Abs((double)a.y - b.y) > EventEpsilon)
                        edges.Add(new Edge(a, b, solids));
                }
                solids++;
            }
            if (edges.Count == 0) return true;
            edges.Sort((a,b) => a.Z.CompareTo(b.Z));
            // Add all inter-solid crossings: between consecutive event heights,
            // the order of the linear boundaries cannot change.
            for (int i = 0; i < edges.Count; i++)
            {
                Edge a = edges[i];
                for (int j = i + 1; j < edges.Count; j++)
                {
                    Edge b = edges[j];
                    if (b.Z >= a.EndZ) break;
                    if (a.Solid == b.Solid || a.MaxX < b.MinX || b.MaxX < a.MinX) continue;
                    double lo = b.Z, hi = Math.Min(a.EndZ, b.EndZ);
                    double d0 = a.At(lo) - b.At(lo), d1 = a.At(hi) - b.At(hi);
                    if ((d0 < 0 && d1 > 0) || (d0 > 0 && d1 < 0))
                        events.Add(lo + (hi - lo) * d0 / (d0 - d1));
                }
            }
            events.Sort();
            int unique = 0;
            for (int i = 0; i < events.Count; i++)
            {
                double value = events[i];
                if (unique == 0 || value - events[unique - 1] > EventEpsilon)
                    events[unique++] = value;
            }
            var active = new List<int>();
            var hits = new List<Hit>();
            var inside = new bool[solids];
            var previous = new List<Span>();
            var current = new List<Span>();
            var continued = new List<bool>();
            int next = 0;
            for (int e = 0; e + 1 < unique; e++)
            {
                double lo = events[e], hi = events[e + 1], mid = (lo + hi) * .5;
                while (next < edges.Count && edges[next].Z < mid)
                { if (edges[next].EndZ > mid) active.Add(next); next++; }
                for (int i = active.Count - 1; i >= 0; i--)
                    if (edges[active[i]].EndZ <= mid) { active[i] = active[active.Count - 1]; active.RemoveAt(active.Count - 1); }
                hits.Clear();
                foreach (int index in active) hits.Add(new Hit { Index = index, X = edges[index].At(mid) });
                hits.Sort();
                current.Clear();
                int coverage = 0, left = -1;
                // Parity within each solid, logical OR across solids. Group
                // coincident crossings so touching/overlapping blocks don't add seams.
                for (int i = 0; i < hits.Count;)
                {
                    int j = i + 1;
                    while (j < hits.Count && hits[j].X - hits[i].X <= EventEpsilon) j++;
                    int before = coverage;
                    for (int k = i; k < j; k++)
                    {
                        int solid = edges[hits[k].Index].Solid;
                        inside[solid] = !inside[solid];
                        coverage += inside[solid] ? 1 : -1;
                    }
                    if (before == 0 && coverage > 0) left = hits[i].Index;
                    if (before > 0 && coverage == 0)
                    {
                        if (left < 0) return false;
                        current.Add(new Span(left, hits[j - 1].Index, lo)); left = -1;
                    }
                    i = j;
                }
                if (coverage != 0 || left != -1) return false;
                // Horizontal boundaries exist only where coverage changes at the
                // shared event line; do not draw the internal triangulation strips.
                HorizontalDifference(previous, current, edges, lo);
                HorizontalDifference(current, previous, edges, lo);
                // Coalesce adjacent strips with identical sides, for a small HUD mesh.
                continued.Clear();
                for (int i = 0; i < previous.Count; i++) continued.Add(false);
                for (int i = 0; i < current.Count; i++)
                {
                    Span span = current[i];
                    for (int j = 0; j < previous.Count; j++)
                    {
                        Span old = previous[j];
                        if (old.Left != span.Left || old.Right != span.Right) continue;
                        span.Start = old.Start; current[i] = span; continued[j] = true; break;
                    }
                }
                for (int i = 0; i < previous.Count; i++)
                    if (!continued[i]) Emit(previous[i], edges, lo);
                var swap = previous; previous = current; current = swap;
            }
            double end = events[unique - 1];
            current.Clear();
            HorizontalDifference(previous, current, edges, end);
            foreach (Span span in previous) Emit(span, edges, end);
            return true;
        }

        // A minus B at one event height. Both span lists are in X order;
        // intervals may collapse to points at a vertex/crossing.
        private static void HorizontalDifference(List<Span> a, List<Span> b, List<Edge> edges, double z)
        {
            int start = 0;
            foreach (Span span in a)
            {
                double l = edges[span.Left].At(z), r = edges[span.Right].At(z);
                while (start < b.Count && edges[b[start].Right].At(z) <= l + EventEpsilon) start++;
                double cursor = l;
                for (int i = start; i < b.Count; i++)
                {
                    double bl = edges[b[i].Left].At(z), br = edges[b[i].Right].At(z);
                    if (bl >= r - EventEpsilon) break;
                    if (bl > cursor + EdgeEpsilon) AddEdge(Point(cursor,z), Point(Math.Min(bl,r),z));
                    cursor = Math.Max(cursor, br);
                    if (cursor >= r - EventEpsilon) break;
                }
                if (r > cursor + EdgeEpsilon) AddEdge(Point(cursor,z), Point(r,z));
            }
        }

        private static void Emit(Span span, List<Edge> edges, double end)
        {
            if (end - span.Start <= EventEpsilon) return;
            Edge l = edges[span.Left], r = edges[span.Right];
            Vector2 a = Point(l.At(span.Start), span.Start), b = Point(r.At(span.Start), span.Start);
            Vector2 c = Point(r.At(end), end), d = Point(l.At(end), end);
            Fill.Add(a); Fill.Add(b); Fill.Add(c);
            Fill.Add(a); Fill.Add(c); Fill.Add(d);
            AddEdge(a,d); AddEdge(b,c);
        }

        private static Vector2 Point(double x, double z) => new Vector2((float)x,(float)z);
        private static void AddEdge(Vector2 a, Vector2 b)
        {
            if ((a-b).sqrMagnitude <= 1e-10f) return;
            Edges.Add(a); Edges.Add(b);
        }
    }
}
