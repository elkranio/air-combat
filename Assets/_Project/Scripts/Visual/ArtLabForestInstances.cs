using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace AirCombat
{
    // ArtLab-only. Draw shared imported meshes directly; no per-tree objects or combined geometry.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class ArtLabForestInstances : MonoBehaviour
    {
        [Serializable]
        public sealed class Batch
        {
            public Mesh mesh;
            public int submesh;
            public Material material;
            public Matrix4x4[] matrices;
            public Bounds bounds;
        }

        [SerializeField] private Camera viewCamera;
        [SerializeField] private Batch[] batches;
        private readonly Plane[] planes = new Plane[6];

        public void Configure(Camera camera, Batch[] data)
        { viewCamera = camera; batches = data; }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (!SystemInfo.supportsInstancing || viewCamera == null || batches == null || batches.Length == 0)
            {
                Debug.LogError("ArtLab forest needs GPU instancing support and generated batches.", this);
                enabled = false; return;
            }
            foreach (var b in batches)
            {
                if (b == null || b.mesh == null || b.material == null || !b.material.enableInstancing ||
                    b.matrices == null || b.matrices.Length == 0 || b.matrices.Length > 256 ||
                    b.submesh < 0 || b.submesh >= b.mesh.subMeshCount)
                {
                    Debug.LogError("Invalid ArtLab instanced forest batch. Regenerate V11.", this);
                    enabled = false; return;
                }
            }
        }

        private void LateUpdate()
        {
            if (viewCamera == null || batches == null) return;
            GeometryUtility.CalculateFrustumPlanes(viewCamera, planes);
            foreach (var b in batches)
            {
                // Keep off-screen casters in the submission margin. Shadows must not pop at the viewport edge.
                var padded = b.bounds;
                padded.Expand(new Vector3(120f, 80f, 120f));
                if (!GeometryUtility.TestPlanesAABB(planes, padded)) continue;
                var rp = new RenderParams(b.material)
                {
                    camera = viewCamera,
                    worldBounds = b.bounds,
                    shadowCastingMode = ShadowCastingMode.On,
                    receiveShadows = true,
                    lightProbeUsage = LightProbeUsage.Off,
                    layer = gameObject.layer
                };
                Graphics.RenderMeshInstanced(rp, b.mesh, b.submesh, b.matrices);
            }
        }
    }
}
