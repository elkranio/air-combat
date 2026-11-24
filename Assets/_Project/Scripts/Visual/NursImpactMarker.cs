using UnityEngine;
using UnityEngine.Rendering;

namespace AirCombat
{
    // World-space projected ground footprint. No colliders, screen-space
    // ellipse tricks or target attraction. Each vertex follows the terrain.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(240)] // After the current shot/flight sample (130).
    public sealed class NursImpactMarker : MonoBehaviour
    {
        [SerializeField, Min(.05f)] private float strokeWidth = .65f;
        [SerializeField, Min(.05f)] private float surfaceLift = .22f;
        [SerializeField, Min(0f)] private float tickLength = 1.8f;
        [SerializeField] private Color readyColor = new Color(1f, .77f, .27f, .88f);
        [SerializeField, Range(0f, 1f)] private float reloadAlpha = .34f;
        private const int Segments = 96;
        private const int RingVertices = (Segments + 1) * 2;
        private readonly Vector3[] vertices = new Vector3[RingVertices + 16];
        private readonly Color[] colors = new Color[RingVertices + 16];
        private ArcadeNursLauncher source;
        private Terrain terrain;
        private TerrainCollider ground;
        private Mesh mesh;
        private Material material;
        private MeshRenderer renderer;
        private Transform marker;

        public void Bind(ArcadeNursLauncher launcher, Shader shader)
        {
            source = launcher;
            var root = new GameObject("NURS / terrain footprint", typeof(MeshFilter), typeof(MeshRenderer));
            marker = root.transform;
            marker.SetParent(transform, false);
            renderer = root.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            material = new Material(shader) { name = "NURS amber footprint" };
            renderer.sharedMaterial = material;
            mesh = new Mesh { name = "Dynamic terrain-conforming NURS ellipse" };
            mesh.MarkDynamic();
            root.GetComponent<MeshFilter>().sharedMesh = mesh;
            var triangles = new int[(Segments + 4) * 6];
            for (int i = 0; i < Segments; i++) SetQuad(triangles, i * 6, i * 2, i * 2 + 1, i * 2 + 2, i * 2 + 3);
            for (int i = 0; i < 4; i++)
            {
                int v = RingVertices + i * 4;
                SetQuad(triangles, (Segments + i) * 6, v, v + 1, v + 2, v + 3);
            }
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = new Vector2[vertices.Length]; // SandboxEffect uses a white mask by default.
        }

        private static void SetQuad(int[] indices, int start, int a, int b, int c, int d)
        {
            indices[start] = a; indices[start + 1] = b; indices[start + 2] = c;
            indices[start + 3] = c; indices[start + 4] = b; indices[start + 5] = d;
        }

        private void LateUpdate()
        {
            if (renderer == null) return;
            renderer.enabled = false;
            if (source == null || !source.MarkerVisible) return;
            if (terrain == null || ground == null)
            {
                terrain = Terrain.activeTerrain;
                ground = terrain != null ? terrain.GetComponent<TerrainCollider>() : null;
            }
            if (ground == null || !source.TryGetFootprint(ground, out Vector3 center,
                out Vector3 forward, out Vector2 radii)) return;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            float halfWidth = Mathf.Min(strokeWidth * .5f, Mathf.Min(radii.x, radii.y) * .45f);
            for (int i = 0; i <= Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                Vector3 offset = right * (Mathf.Cos(a) * radii.x) + forward * (Mathf.Sin(a) * radii.y);
                Vector3 normal = (right * (Mathf.Cos(a) / radii.x) + forward * (Mathf.Sin(a) / radii.y)).normalized;
                if (!Place(i * 2, center + offset - normal * halfWidth) ||
                    !Place(i * 2 + 1, center + offset + normal * halfWidth)) return;
            }
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * .5f;
                Vector3 radial = (right * Mathf.Cos(a) + forward * Mathf.Sin(a)).normalized;
                Vector3 tangent = Vector3.Cross(Vector3.up, radial);
                Vector3 point = center + right * (Mathf.Cos(a) * radii.x) + forward * (Mathf.Sin(a) * radii.y);
                int v = RingVertices + i * 4;
                if (!Place(v, point - tangent * halfWidth) || !Place(v + 1, point + tangent * halfWidth) ||
                    !Place(v + 2, point + radial * tickLength - tangent * halfWidth) ||
                    !Place(v + 3, point + radial * tickLength + tangent * halfWidth)) return;
            }
            Color tint = readyColor;
            if (source.AmmoRemaining == 0) tint.a *= reloadAlpha;
            for (int i = 0; i < colors.Length; i++) colors[i] = tint;
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.RecalculateBounds();
            renderer.enabled = true;
        }

        private bool Place(int i, Vector3 world)
        {
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            if (world.x < origin.x || world.x > origin.x + size.x ||
                world.z < origin.z || world.z > origin.z + size.z) return false;
            world.y = origin.y + terrain.SampleHeight(world) + surfaceLift;
            vertices[i] = marker.InverseTransformPoint(world);
            return true;
        }

        private void OnDisable() { if (renderer != null) renderer.enabled = false; }
        private void OnDestroy()
        {
            if (marker != null) Destroy(marker.gameObject);
            if (mesh != null) Destroy(mesh);
            if (material != null) Destroy(material);
        }
    }
}
