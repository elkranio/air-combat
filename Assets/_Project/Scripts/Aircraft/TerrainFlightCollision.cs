using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    // Mission01 flies on a fixed altitude plane. Sweep a forgiving circular
    // aircraft footprint against the very same rock sections as the radar.
    // No physics bodies, mesh colliders, or decorative rubble participate.
    internal static class TerrainFlightCollision
    {
        private static readonly ProfilerMarker Marker = new ProfilerMarker("AirCombat.TerrainCollision.Sweep");
        private const float Epsilon = 1e-6f;

        internal static bool TryHit(Scene scene, Vector3 from, Vector3 to, float radius, out Vector3 impact)
        {
            using (Marker.Auto())
            {
                var a = new Vector2(from.x, from.z);
                var b = new Vector2(to.x, to.z);
                var delta = b - a;
                float best = 2f;
                // Conservative broad phase: a circle containing the entire sweep.
                var midpoint = (a + b) * .5f;
                float range = delta.magnitude * .5f + radius;
                foreach (var rock in RadarTerrainObstacle.Active)
                {
                    if (rock == null || rock.gameObject.scene != scene ||
                        !rock.IsNear(midpoint, range, to.y)) continue;
                    rock.Slice(to.y);
                    Scan(a, delta, radius, rock.Edges, ref best);
                    if (best <= 0f) break;
                }
                impact = best <= 1f ? Vector3.LerpUnclamped(from, to, best) : to;
                return best <= 1f;
            }
        }

        private static void Scan(Vector2 start, Vector2 delta, float radius,
            List<Vector2> edges, ref float best)
        {
            // An aircraft spawned inside a closed section must also crash.
            bool inside = false;
            for (int i = 0; i + 1 < edges.Count; i += 2)
            {
                Vector2 a = edges[i], b = edges[i+1];
                if ((a.y > start.y) != (b.y > start.y) &&
                    start.x < a.x + (b.x-a.x)*(start.y-a.y)/(b.y-a.y)) inside = !inside;
                SweepEdge(start, delta, a, b, radius, ref best);
                if (best <= 0f) return;
            }
            if (inside) best = 0f;
        }

        private static void SweepEdge(Vector2 start, Vector2 delta, Vector2 a, Vector2 b,
            float radius, ref float best)
        {
            // Cheap rejection against the swept bounding box.
            Vector2 end = start + delta;
            if (Mathf.Max(a.x,b.x) < Mathf.Min(start.x,end.x)-radius ||
                Mathf.Min(a.x,b.x) > Mathf.Max(start.x,end.x)+radius ||
                Mathf.Max(a.y,b.y) < Mathf.Min(start.y,end.y)-radius ||
                Mathf.Min(a.y,b.y) > Mathf.Max(start.y,end.y)+radius) return;
            Vector2 edge = b-a;
            float length = edge.magnitude;
            if (length < Epsilon) { SweepPoint(start, delta, a, radius, ref best); return; }
            Vector2 axis = edge/length;
            Vector2 normal = new Vector2(-axis.y, axis.x);
            float along = Vector2.Dot(start-a, axis);
            float across = Vector2.Dot(start-a, normal);
            Vector2 nearest = a + axis*Mathf.Clamp(along, 0f, length);
            if ((start-nearest).sqrMagnitude <= radius*radius) { best = 0f; return; }
            // The segment's capsule consists of a strip and two endpoint discs.
            float vn = Vector2.Dot(delta, normal);
            float vt = Vector2.Dot(delta, axis);
            if (Mathf.Abs(vn) > Epsilon)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    float t = (side*radius-across)/vn;
                    float s = along+t*vt;
                    if (t >= 0f && t <= 1f && t < best && s >= 0f && s <= length) best = t;
                }
            }
            SweepPoint(start, delta, a, radius, ref best);
            SweepPoint(start, delta, b, radius, ref best);
        }

        private static void SweepPoint(Vector2 start, Vector2 delta, Vector2 p, float radius, ref float best)
        {
            Vector2 offset = start-p;
            // Double precision for the discriminant on long debug/boost sweeps.
            double a = (double)delta.x*delta.x + (double)delta.y*delta.y;
            double b = (double)offset.x*delta.x + (double)offset.y*delta.y;
            double c = (double)offset.x*offset.x + (double)offset.y*offset.y - (double)radius*radius;
            if (c <= 0d) { best = 0f; return; }
            if (a <= 1e-12 || b >= 0d) return;
            double disc = b*b-a*c;
            if (disc < 0d) return;
            double t = c/(-b+System.Math.Sqrt(disc));
            if (t >= 0d && t <= 1d && t < best) best = (float)t;
        }
    }
}
