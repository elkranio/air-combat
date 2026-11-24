using System.Collections.Generic;
using UnityEngine;

namespace AirCombat
{
    // Shared authored path: straight legs joined by tangent circular fillets in XZ.
    // Height and normal are sampled from the route's Terrain at the actual position.
    public sealed class GroundPatrolPath
    {
        public readonly struct Segment
        {
            public readonly Vector3 Start, End;
            public readonly float Length, Radius;
            public readonly int FromWaypoint, ToWaypoint;
            private readonly Vector3 center, tangent;
            private readonly float sweep;

            public Segment(Vector3 start, Vector3 end, int from, int to,
                Vector3 arcCenter = default, float arcRadius = 0f, float arcSweep = 0f,
                Vector3 arcTangent = default)
            {
                Start = start; End = end; FromWaypoint = from; ToWaypoint = to;
                center = arcCenter; Radius = arcRadius; sweep = arcSweep;
                Vector3 delta = end - start; delta.y = 0f;
                Length = Radius > 0f ? Radius * Mathf.Abs(sweep) * Mathf.Deg2Rad : delta.magnitude;
                tangent = Radius > 0f ? arcTangent : delta.normalized;
            }

            public Vector3 Position(float t)
            {
                t = Mathf.Clamp01(t);
                if (Radius <= 0f || t == 0f || t == 1f) return Vector3.Lerp(Start, End, t);
                Vector3 p = center + Quaternion.AngleAxis(sweep * t, Vector3.up) * (Start - center);
                p.y = Mathf.Lerp(Start.y, End.y, t);
                return p;
            }

            public float Heading(float t, int direction = 1)
            {
                Vector3 forward = Quaternion.AngleAxis(sweep * Mathf.Clamp01(t), Vector3.up) * tangent * direction;
                return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            }
        }

        private struct Corner
        {
            public Vector3 Entry, Exit, Center, Tangent;
            public float Radius, Sweep;
        }

        private readonly List<Segment> segments = new List<Segment>();
        private readonly int[] starts;
        private readonly Vector3[] startPositions;
        public IReadOnlyList<Segment> Segments => segments;
        public int Count => segments.Count;

        public GroundPatrolPath(GroundPatrolRoute route)
        {
            int count = route.Count;
            starts = new int[count];
            startPositions = new Vector3[count];
            if (count == 0) return;
            bool loop = route.Mode == GroundPatrolRoute.Traversal.Loop;
            var points = new Vector3[count];
            var corners = new Corner[count];
            for (int i = 0; i < count; i++) points[i] = route.GetPoint(i);
            for (int i = 0; i < count; i++)
            {
                var c = new Corner { Entry = points[i], Exit = points[i] };
                if (count >= 3 && (loop || (i > 0 && i < count - 1)))
                    c = Round(points[(i + count - 1) % count], points[i], points[(i + 1) % count], route.CornerRadius);
                corners[i] = c;
                startPositions[i] = c.Exit;
            }
            // A waypoint's start pose is the exit tangent of its corner. This
            // avoids spawning on the sharp control point outside the actual path.
            for (int i = 0; i < count; i++)
            {
                starts[i] = segments.Count;
                if (i == count - 1 && !loop) break;
                int j = (i + 1) % count;
                Add(new Segment(corners[i].Exit, corners[j].Entry, i, j));
                Corner c = corners[j];
                if (c.Radius > 0f)
                    Add(new Segment(c.Entry, c.Exit, i, j, c.Center, c.Radius, c.Sweep, c.Tangent));
            }
        }

        private void Add(Segment segment)
        {
            if (segment.Length > .001f) segments.Add(segment);
        }

        public Vector3 StartPosition(int waypoint) => startPositions[waypoint];
        public int StartSegment(int waypoint) => starts[waypoint];

        private static Corner Round(Vector3 before, Vector3 point, Vector3 after, float radius)
        {
            var result = new Corner { Entry = point, Exit = point };
            Vector3 incoming = point - before, outgoing = after - point;
            incoming.y = outgoing.y = 0f;
            float a = incoming.magnitude, b = outgoing.magnitude;
            if (radius <= .001f || a <= .01f || b <= .01f) return result;
            incoming /= a; outgoing /= b;
            float angle = Vector3.SignedAngle(incoming, outgoing, Vector3.up);
            float abs = Mathf.Abs(angle);
            // Straight and near-reversal corners need no fillet. A true U-turn
            // uses the motor's deliberate in-place turnaround instead.
            if (abs < .5f || abs >= 175f) return result;
            float tangentFactor = Mathf.Tan(abs * .5f * Mathf.Deg2Rad);
            float cut = Mathf.Min(radius * tangentFactor, .45f * Mathf.Min(a, b));
            if (cut <= .001f) return result;
            result.Radius = cut / tangentFactor;
            result.Entry = Vector3.Lerp(point, before, cut / a);
            result.Exit = Vector3.Lerp(point, after, cut / b);
            result.Center = result.Entry + Vector3.Cross(Vector3.up, incoming) * Mathf.Sign(angle) * result.Radius;
            result.Tangent = incoming;
            result.Sweep = angle;
            return result;
        }
    }
}
