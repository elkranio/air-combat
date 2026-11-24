using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AirCombat
{
    // Vertical world-space decal evaluated on the camera's opaque depth.
    // The visible receiver can be Terrain, a separate plateau mesh or a roof;
    // no mesh tessellation, collider or modification of receiver materials.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(240)]
    public sealed class NursImpactMarker : MonoBehaviour
    {
        [SerializeField, Min(.05f)] private float strokeWidth = .65f;
        [SerializeField, Min(0f)] private float tickLength = 1.8f;
        [SerializeField, Min(1f)] private float aircraftClearance = 5f;
        [SerializeField] private Color readyColor = new Color(1f, .77f, .27f, .88f);
        [SerializeField, Range(0f, 1f)] private float reloadAlpha = .34f;
        private ArcadeNursLauncher source;
        private TerrainCollider ground;
        private Mesh mesh;
        private Material material;
        private MeshRenderer renderer;
        private Transform marker;

        public void Bind(ArcadeNursLauncher launcher, Camera view)
        {
            source = launcher;
            var shader = Resources.Load<Shader>("NursGroundProjection");
            if (view == null || shader == null)
            {
                Debug.LogError("NURS projection: missing camera or projection shader.", this);
                enabled = false;
                return;
            }
            // Explicitly request the opaque depth only on the ground-test view.
            // The air encounter and legacy labs never instantiate this component.
            view.GetUniversalAdditionalCameraData().requiresDepthTexture = true;
            var root = new GameObject("NURS / world-surface projection", typeof(MeshFilter), typeof(MeshRenderer));
            marker = root.transform;
            marker.SetParent(transform, false);
            renderer = root.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            material = new Material(shader) { name = "NURS amber surface projection" };
            renderer.sharedMaterial = material;
            // Clip-space quad: the fragment shader reconstructs the visible world
            // position from depth. Broad bounds prevent ordinary world frustum culling.
            mesh = new Mesh { name = "NURS projection screen quad" };
            mesh.vertices = new[] { new Vector3(-1f, -1f, 0f), new Vector3(1f, -1f, 0f),
                new Vector3(-1f, 1f, 0f), new Vector3(1f, 1f, 0f) };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 10000f);
            root.GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        private void LateUpdate()
        {
            if (renderer == null) return;
            renderer.enabled = false;
            if (source == null || !source.MarkerVisible) return;
            if (ground == null)
            {
                var terrain = Terrain.activeTerrain;
                ground = terrain != null ? terrain.GetComponent<TerrainCollider>() : null;
            }
            if (ground == null || !source.TryGetFootprint(ground, out Vector3 center,
                out Vector3 forward, out Vector2 radii)) return;
            Color tint = readyColor;
            if (source.AmmoRemaining == 0) tint.a *= reloadAlpha;
            material.SetVector("_Centre", new Vector4(center.x, center.y, center.z, 0f));
            material.SetVector("_Forward", new Vector4(forward.x, 0f, forward.z, 0f));
            material.SetVector("_Radii", new Vector4(radii.x, radii.y, 0f, 0f));
            material.SetFloat("_Stroke", strokeWidth);
            material.SetFloat("_TickLength", tickLength);
            // Never paint the player's hull, even if it overlaps the aim area.
            material.SetFloat("_MaxReceiverY", transform.position.y - aircraftClearance);
            material.SetColor("_Tint", tint);
            renderer.enabled = true;
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
