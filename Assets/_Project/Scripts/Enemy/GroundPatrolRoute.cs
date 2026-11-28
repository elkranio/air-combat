using System.Collections.Generic;
using UnityEngine;

namespace AirCombat
{
    // Scene-owned route data, reusable by multiple units. Not a navigation mesh
    // or a combat trigger: the drawn path is the actual path a motor follows.
    [DisallowMultipleComponent]
    public sealed class GroundPatrolRoute : MonoBehaviour
    {
        public enum Traversal { Loop, PingPong, Once }

        [SerializeField] private Terrain terrain;
        [SerializeField] private Traversal traversal = Traversal.Loop;
        [SerializeField] private List<Vector3> localPoints = new List<Vector3>();
        [SerializeField, Min(0f), Tooltip("Desired turn radius in world units. Short legs clamp the radius; zero restores sharp corners.")]
        private float cornerRadius = 9f;
        [SerializeField] private bool showRoute = true;
        [SerializeField] private Color routeColor = new Color(.25f, .9f, .95f, 1f);

        public int Count => localPoints.Count;
        public Traversal Mode => traversal;
        public Terrain Surface => terrain;
        public float CornerRadius => Mathf.Max(0f, cornerRadius);
        public GroundPatrolPath BuildPath() => new GroundPatrolPath(this);

        public Vector3 GetPoint(int index) => Project(transform.TransformPoint(localPoints[index]), out _);

        // Used by both the runtime motor and authoring preview. Terrain only for
        // v0; routes must avoid buildings and separate plateau meshes.
        public Vector3 Project(Vector3 world, out Vector3 normal)
        {
            if (GroundSurface.TryTerrain(terrain, world, out float height, out normal)) world.y = height;
            return world;
        }

#if UNITY_EDITOR
        public void ConfigureAuthoring(Terrain surface, IEnumerable<Vector3> points)
        {
            terrain = surface;
            localPoints.Clear();
            foreach (Vector3 point in points) AddAuthoringPoint(point);
        }

        public void AddAuthoringPoint(Vector3 world) => localPoints.Add(transform.InverseTransformPoint(Project(world, out _)));
        public void MoveAuthoringPoint(int index, Vector3 world) => localPoints[index] = transform.InverseTransformPoint(Project(world, out _));

        private void OnDrawGizmos()
        {
            if (!showRoute || Count == 0) return;
            Gizmos.color = routeColor;
            for (int i = 0; i < Count; i++)
            {
                Vector3 point = GetPoint(i) + Vector3.up * .6f;
                Gizmos.DrawWireSphere(point, 2.5f);
            }
            foreach (var segment in BuildPath().Segments) DrawSegment(segment);
        }

        private void DrawSegment(GroundPatrolPath.Segment segment)
        {
            int steps = Mathf.Clamp(Mathf.CeilToInt(segment.Length / (segment.Radius > 0f ? 1f : 4f)), 1, 512);
            Vector3 previous = Project(segment.Start, out _) + Vector3.up * .6f;
            for (int i = 1; i <= steps; i++)
            {
                Vector3 point = Project(segment.Position(i / (float)steps), out _) + Vector3.up * .6f;
                Gizmos.DrawLine(previous, point);
                previous = point;
            }
            if (segment.Radius > 0f) return;
            Vector3 direction = Quaternion.Euler(0f, segment.Heading(.6f), 0f) * Vector3.forward;
            float arrowSize = Mathf.Min(3f, segment.Length * .15f);
            DrawArrow(segment.Position(.6f), direction, arrowSize);
            if (traversal == Traversal.PingPong) DrawArrow(segment.Position(.35f), -direction, arrowSize);
        }

        private void DrawArrow(Vector3 arrow, Vector3 direction, float size)
        {
            Vector3 tip = Project(arrow + direction * size, out _) + Vector3.up * .8f;
            Vector3 side = Vector3.Cross(Vector3.up, direction) * size * .65f;
            Gizmos.DrawLine(tip, Project(arrow - direction * size * .65f + side, out _) + Vector3.up * .8f);
            Gizmos.DrawLine(tip, Project(arrow - direction * size * .65f - side, out _) + Vector3.up * .8f);
        }
#endif
    }
}
