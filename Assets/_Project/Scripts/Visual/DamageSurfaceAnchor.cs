using UnityEngine;

namespace AirCombat
{
    // Model-local attachment, reusable by aircraft, wrecks and other rigid meshes.
    // Authored DamageSockets take priority. Otherwise sample a real triangle by
    // surface area, never a random point in a bounding box or a gameplay hitbox.
    public readonly struct DamageSurfaceAnchor
    {
        private readonly Transform part;
        private readonly Vector3 localPoint;
        public bool IsValid => part != null;
        public Vector3 WorldPosition => part.TransformPoint(localPoint);

        private DamageSurfaceAnchor(Transform part, Vector3 point)
        {
            this.part = part;
            localPoint = point;
        }

        public static DamageSurfaceAnchor Choose(Transform visual, MeshFilter[] filters, System.Random rng)
        {
            if (visual == null) return default;
            foreach (var group in visual.GetComponentsInChildren<Transform>(true))
            {
                if (group.name != "DamageSockets" || group.childCount == 0) continue;
                return new DamageSurfaceAnchor(group.GetChild(rng.Next(group.childCount)), Vector3.zero);
            }
            DamageSurfaceAnchor chosen = default;
            double totalArea = 0;
            if (filters == null) return chosen;
            foreach (var filter in filters)
            {
                if (filter == null || !filter.gameObject.activeInHierarchy) continue;
                var renderer = filter.GetComponent<MeshRenderer>();
                var mesh = filter.sharedMesh;
                if (renderer == null || !renderer.enabled || mesh == null || !mesh.isReadable) continue;
                var vertices = mesh.vertices;
                var matrix = filter.transform.localToWorldMatrix;
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    if (mesh.GetTopology(sub) != MeshTopology.Triangles) continue;
                    var indices = mesh.GetIndices(sub);
                    for (int i = 0; i + 2 < indices.Length; i += 3)
                    {
                        Vector3 a = vertices[indices[i]], b = vertices[indices[i + 1]], c = vertices[indices[i + 2]];
                        float area = Vector3.Cross(matrix.MultiplyVector(b - a), matrix.MultiplyVector(c - a)).magnitude;
                        if (area <= 1e-8f) continue;
                        totalArea += area;
                        // Weighted reservoir sampling: no persistent triangle table.
                        if (rng.NextDouble() * totalArea >= area) continue;
                        float u = (float)rng.NextDouble(), v = (float)rng.NextDouble();
                        if (u + v > 1f) { u = 1f - u; v = 1f - v; }
                        chosen = new DamageSurfaceAnchor(filter.transform, a + u * (b - a) + v * (c - a));
                    }
                }
            }
            return chosen;
        }
    }
}
