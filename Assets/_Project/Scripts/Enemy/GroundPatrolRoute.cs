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
        [SerializeField] private bool showRoute = true;
        [SerializeField] private Color routeColor = new Color(.25f, .9f, .95f, 1f);

        public int Count => localPoints.Count;
        public Traversal Mode => traversal;
        public Terrain Surface => terrain;

        public Vector3 GetPoint(int index) => Project(transform.TransformPoint(localPoints[index]), out _);

        // Used by both the runtime motor and authoring preview. Terrain only for
        // v0; routes must avoid buildings and separate plateau meshes.
        public Vector3 Project(Vector3 world, out Vector3 normal)
        {
            normal = Vector3.up;
            if (terrain == null || terrain.terrainData == null) return world;
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            float u = (world.x - origin.x) / size.x;
            float v = (world.z - origin.z) / size.z;
            if (u < 0f || u > 1f || v < 0f || v > 1f) return world;
            world.y = terrain.SampleHeight(world) + origin.y;
            normal = terrain.transform.TransformDirection(terrain.terrainData.GetInterpolatedNormal(u, v)).normalized;
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
                if (i < Count - 1) DrawLeg(GetPoint(i), GetPoint(i + 1));
            }
            if (traversal == Traversal.Loop && Count > 2) DrawLeg(GetPoint(Count - 1), GetPoint(0));
        }

        private void DrawLeg(Vector3 from, Vector3 to)
        {
            Vector3 horizontal = to - from;
            horizontal.y = 0f;
            if (horizontal.sqrMagnitude < .01f) return;
            int steps = Mathf.Clamp(Mathf.CeilToInt(horizontal.magnitude / 4f), 1, 512);
            Vector3 previous = Project(from, out _) + Vector3.up * .6f;
            for (int i = 1; i <= steps; i++)
            {
                Vector3 point = Project(Vector3.Lerp(from, to, i / (float)steps), out _) + Vector3.up * .6f;
                Gizmos.DrawLine(previous, point);
                previous = point;
            }
            Vector3 direction = horizontal.normalized;
            float arrowSize = Mathf.Min(3f, horizontal.magnitude * .15f);
            DrawArrow(Vector3.Lerp(from, to, .6f), direction, arrowSize);
            if (traversal == Traversal.PingPong || (traversal == Traversal.Loop && Count == 2))
                DrawArrow(Vector3.Lerp(from, to, .35f), -direction, arrowSize);
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
