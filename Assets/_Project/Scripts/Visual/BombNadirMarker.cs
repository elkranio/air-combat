using UnityEngine;
using UnityEngine.Rendering;

namespace AirCombat
{
    // World-space altitude reference, NOT a bombing trajectory / impact predictor.
    // GroundAttackLab equips this only while slot 4 (Bomb) is selected.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(225)] // after WeaponSelection (120) and aircraft movement
    public sealed class BombNadirMarker : MonoBehaviour
    {
        private const int DashCount = 18;
        private const int RingSegments = 32;
        private const int QuadCount = DashCount + RingSegments;
        private const float RayLength = 600f;
        private const float DashWidth = .22f;
        private const float RingRadius = 2.5f;
        private const float RingWidth = .28f;
        private static readonly RaycastHit[] Hits = new RaycastHit[24];

        private WeaponSelection selection;
        private Camera viewCamera;
        private GameObject visual;
        private Mesh mesh;
        private MeshRenderer meshRenderer;
        private Material material;
        private Vector3[] vertices;

        public void Configure(WeaponSelection weaponSelection, Camera camera)
        {
            if (visual != null) return;
            selection = weaponSelection;
            viewCamera = camera;
            if (selection == null || viewCamera == null)
            {
                Debug.LogError("BombNadirMarker: missing weapon selector or camera.", this);
                enabled = false;
                return;
            }
            var shader = Shader.Find("AirCombat/SandboxEffect");
            if (shader == null)
            {
                Debug.LogError("BombNadirMarker: AirCombat/SandboxEffect shader is missing.", this);
                enabled = false;
                return;
            }

            material = new Material(shader) { name = "Bomb nadir guide (runtime)" };
            visual = new GameObject("Bomb nadir / line and ground ring");
            mesh = new Mesh { name = "Bomb nadir guide mesh" };
            mesh.MarkDynamic();
            vertices = new Vector3[QuadCount * 4];
            var colors = new Color32[vertices.Length];
            var triangles = new int[QuadCount * 6];
            for (int i = 0; i < QuadCount; i++)
            {
                int v = i * 4, t = i * 6;
                triangles[t] = v;
                triangles[t + 1] = v + 1;
                triangles[t + 2] = v + 2;
                triangles[t + 3] = v + 2;
                triangles[t + 4] = v + 1;
                triangles[t + 5] = v + 3;
                var tint = new Color32(201, 219, 219, i < DashCount ? (byte)95 : (byte)140);
                colors[v] = colors[v + 1] = colors[v + 2] = colors[v + 3] = tint;
            }
            mesh.vertices = vertices;
            mesh.colors32 = colors;
            mesh.triangles = triangles;
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;
            meshRenderer = visual.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            meshRenderer.enabled = false;
        }

        private void LateUpdate()
        {
            // The helper must never appear with guns, GAUSS or air-to-air missiles.
            if (visual == null || selection == null || selection.Selected != WeaponMode.Bomb)
            {
                Hide();
                return;
            }

            Vector3 jetPosition = transform.position;
            Vector3 rayStart = jetPosition + Vector3.up * 1.5f;
            int count = Physics.RaycastNonAlloc(rayStart, Vector3.down, Hits,
                RayLength, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            RaycastHit surface = default;
            for (int i = 0; i < count; i++)
            {
                var hit = Hits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform) ||
                    hit.distance >= nearest) continue;
                nearest = hit.distance;
                surface = hit;
            }
            if (float.IsPositiveInfinity(nearest))
            {
                Hide();
                return;
            }

            // Strictly vertical: no horizontal lead, no predicted release/impact.
            Vector3 top = jetPosition + Vector3.down * .9f;
            Vector3 bottom = surface.point + surface.normal * .16f;
            Vector3 axis = bottom - top;
            if (axis.sqrMagnitude < 1f)
            {
                Hide();
                return;
            }
            Vector3 centre = (top + bottom) * .5f;
            Vector3 toCamera = viewCamera.transform.position - centre;
            Vector3 side = Vector3.Cross(axis.normalized, toCamera.normalized);
            if (side.sqrMagnitude < .0001f) side = viewCamera.transform.right;
            side = side.normalized * (DashWidth * .5f);

            visual.transform.SetPositionAndRotation(jetPosition, Quaternion.identity);
            for (int i = 0; i < DashCount; i++)
            {
                // Evenly spaced dashes, leaving air at both ends of the guide.
                float start = (i + .12f) / DashCount;
                float end = (i + .60f) / DashCount;
                Vector3 a = Vector3.Lerp(top, bottom, start) - jetPosition;
                Vector3 b = Vector3.Lerp(top, bottom, end) - jetPosition;
                WriteQuad(i, a - side, a + side, b - side, b + side);
            }

            Vector3 normal = surface.normal.normalized;
            Vector3 right = Vector3.ProjectOnPlane(Vector3.right, normal);
            if (right.sqrMagnitude < .001f)
                right = Vector3.ProjectOnPlane(Vector3.forward, normal);
            right.Normalize();
            Vector3 forward = Vector3.Cross(normal, right);
            for (int i = 0; i < RingSegments; i++)
            {
                float a = (i / (float)RingSegments) * Mathf.PI * 2f;
                float b = ((i + 1) / (float)RingSegments) * Mathf.PI * 2f;
                Vector3 ca = right * Mathf.Cos(a) + forward * Mathf.Sin(a);
                Vector3 cb = right * Mathf.Cos(b) + forward * Mathf.Sin(b);
                Vector3 origin = bottom - jetPosition;
                WriteQuad(DashCount + i,
                    origin + ca * (RingRadius - RingWidth * .5f),
                    origin + ca * (RingRadius + RingWidth * .5f),
                    origin + cb * (RingRadius - RingWidth * .5f),
                    origin + cb * (RingRadius + RingWidth * .5f));
            }
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
            meshRenderer.enabled = true;
        }

        private void WriteQuad(int quad, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int offset = quad * 4;
            vertices[offset] = a;
            vertices[offset + 1] = b;
            vertices[offset + 2] = c;
            vertices[offset + 3] = d;
        }

        private void Hide()
        {
            if (meshRenderer != null) meshRenderer.enabled = false;
        }

        private void OnDisable() { Hide(); }

        private void OnDestroy()
        {
            if (visual != null) Destroy(visual);
            if (mesh != null) Destroy(mesh);
            if (material != null) Destroy(material);
        }
    }
}
