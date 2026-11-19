using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;
using UnityEngine.Rendering;

namespace AirCombat
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class ProceduralLandscape : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private GameObject debugGround;
        [SerializeField] private GameObject debugGrid;
        [SerializeField] private int seed = 42;
        [SerializeField, Range(0f, 1f)] private float treeDensity = 0.85f;
        [SerializeField, Min(0)] private int preloadChunks = 1;
        [SerializeField] private GameObject[] treeModels;
        [SerializeField] private GameObject[] rockModels;
        [SerializeField] private GameObject[] bushModels;

        private static readonly ProfilerMarker GenerateMarker = new ProfilerMarker("Landscape.GenerateChunk");
        private static readonly ProfilerMarker ReleaseMarker = new ProfilerMarker("Landscape.ReleaseChunk");
        private readonly List<Vector2Int> pending = new List<Vector2Int>();
        private readonly Dictionary<Vector2Int, Chunk> chunks = new Dictionary<Vector2Int, Chunk>();
        private readonly List<Vector2Int> expired = new List<Vector2Int>();
        private Material[] materials;
        private Vector2Int previousCenter;
        private Vector2Int previousRadius;
        private int previousSeed;
        private float previousDensity;
        private bool initialized;
        private bool groundWasActive;
        private bool gridWasActive;

        private void OnEnable()
        {
            if (viewCamera == null || !viewCamera.orthographic)
            {
                Debug.LogError("ProceduralLandscape needs an orthographic camera.", this);
                enabled = false;
                return;
            }

            materials = LandscapePalette.Create();
            if (debugGround != null)
            {
                groundWasActive = debugGround.activeSelf;
                debugGround.SetActive(false);
            }
            if (debugGrid != null)
            {
                gridWasActive = debugGrid.activeSelf;
                debugGrid.SetActive(false);
            }
            initialized = true;
            Refresh(true);
        }

        private void LateUpdate()
        {
            if (initialized && viewCamera != null)
                Refresh(false);
        }

        private void Refresh(bool force)
        {
            const int size = LandscapeChunkBuilder.Size;
            var p = viewCamera.transform.position;
            var center = new Vector2Int(Mathf.FloorToInt(p.x / size), Mathf.FloorToInt(p.z / size));
            var margin = Mathf.Max(0, preloadChunks);
            var radius = new Vector2Int(
                Mathf.CeilToInt(viewCamera.orthographicSize * viewCamera.aspect / size) + margin,
                Mathf.CeilToInt(viewCamera.orthographicSize / size) + margin);
            var appearanceChanged = previousSeed != seed || !Mathf.Approximately(previousDensity, treeDensity);
            if (force || appearanceChanged || center != previousCenter || radius != previousRadius)
            {
                if (appearanceChanged) ClearChunks();

                // Rebuild the small work lists only when the streaming window moves.
                // Keep an extra ring of loaded tiles so reversals do not churn the border.
                expired.Clear();
                foreach (var pair in chunks)
                {
                    var delta = pair.Key - center;
                    if (Mathf.Abs(delta.x) > radius.x + 1 || Mathf.Abs(delta.y) > radius.y + 1)
                        expired.Add(pair.Key);
                }
                pending.Clear();
                for (int z = -radius.y; z <= radius.y; z++)
                for (int x = -radius.x; x <= radius.x; x++)
                {
                    var key = center + new Vector2Int(x, z);
                    if (!chunks.ContainsKey(key)) pending.Add(key);
                }
                previousCenter = center;
                previousRadius = radius;
                previousSeed = seed;
                previousDensity = treeDensity;

                if (force || appearanceChanged)
                {
                    // Warm the visible area before the first rendered frame (or on a
                    // deliberate Inspector seed/density change). Prefetch the margin later.
                    float halfHeight = viewCamera.orthographicSize;
                    float halfWidth = halfHeight * viewCamera.aspect;
                    for (int i = pending.Count - 1; i >= 0; i--)
                    {
                        var key = pending[i];
                        if (DistanceToView(key, p, halfWidth, halfHeight) > 0f) continue;
                        chunks.Add(key, CreateChunk(key));
                        pending.RemoveAt(i);
                    }
                    return;
                }
            }

            // Hard limit: at most one complete new tile per rendered frame. The existing
            // preload margin gives us time to finish the queue before it becomes visible.
            if (pending.Count > 0)
            {
                float halfHeight = viewCamera.orthographicSize;
                float halfWidth = halfHeight * viewCamera.aspect;
                int best = 0;
                float bestVisible = float.PositiveInfinity, bestDistance = float.PositiveInfinity;
                for (int i = 0; i < pending.Count; i++)
                {
                    var key = pending[i];
                    float visible = DistanceToView(key, p, halfWidth, halfHeight);
                    float dx = (key.x + 0.5f) * size - p.x;
                    float dz = (key.y + 0.5f) * size - p.z;
                    float distance = dx * dx + dz * dz;
                    if (visible < bestVisible || (visible == bestVisible && distance < bestDistance))
                    {
                        best = i;
                        bestVisible = visible;
                        bestDistance = distance;
                    }
                }
                var next = pending[best];
                pending.RemoveAt(best);
                chunks.Add(next, CreateChunk(next));
            }
            // Also spread destruction. The extra ring is never inside the view.
            // Defer it while generation is pending instead of stacking both costs.
            else if (expired.Count > 0)
            {
                int last = expired.Count - 1;
                var key = expired[last];
                expired.RemoveAt(last);
                Release(chunks[key]);
                chunks.Remove(key);
            }
        }

        private static float DistanceToView(Vector2Int key, Vector3 cameraPosition, float halfWidth, float halfHeight)
        {
            const int size = LandscapeChunkBuilder.Size;
            float dx = Mathf.Max(0f, Mathf.Abs((key.x + 0.5f) * size - cameraPosition.x) - halfWidth - size * 0.5f);
            float dz = Mathf.Max(0f, Mathf.Abs((key.y + 0.5f) * size - cameraPosition.z) - halfHeight - size * 0.5f);
            return dx * dx + dz * dz;
        }

        private Chunk CreateChunk(Vector2Int key)
        {
            using var sample = GenerateMarker.Auto();
            var owner = new GameObject("Landscape " + key.x + "," + key.y);
            owner.transform.SetParent(transform, false);
            owner.transform.position = new Vector3(key.x * (float)LandscapeChunkBuilder.Size, 0f,
                key.y * (float)LandscapeChunkBuilder.Size);
            var mesh = LandscapeChunkBuilder.Build(key, seed, treeDensity, owner.transform,
                treeModels, rockModels, bushModels, materials, out var groundTexture);
            owner.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = owner.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            // Only the ground uses the per-chunk color texture. The other submeshes
            // (river and fallback trees) keep their existing shared materials.
            var ground = new MaterialPropertyBlock();
            ground.SetTexture("_BaseMap", groundTexture);
            ground.SetColor("_BaseColor", Color.white);
            renderer.SetPropertyBlock(ground, LandscapePalette.Grass);
            // Geometry is purely decorative; no physics and no terrain shadow-map cost.
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return new Chunk(owner, mesh, groundTexture);
        }

        private void OnDisable()
        {
            if (!initialized)
                return;
            initialized = false;
            ClearChunks();
            if (materials != null)
            {
                foreach (var material in materials)
                    if (material != null) Destroy(material);
                materials = null;
            }
            if (debugGround != null) debugGround.SetActive(groundWasActive);
            if (debugGrid != null) debugGrid.SetActive(gridWasActive);
        }

        private void ClearChunks()
        {
            pending.Clear();
            expired.Clear();
            foreach (var pair in chunks)
                Release(pair.Value);
            chunks.Clear();
        }

        private static void Release(Chunk chunk)
        {
            using var sample = ReleaseMarker.Auto();
            if (chunk.Owner != null)
            {
                chunk.Owner.SetActive(false);
                Destroy(chunk.Owner);
            }
            if (chunk.Mesh != null) Destroy(chunk.Mesh);
            if (chunk.GroundTexture != null) Destroy(chunk.GroundTexture);
        }

        private sealed class Chunk
        {
            public readonly GameObject Owner;
            public readonly Mesh Mesh;
            public readonly Texture2D GroundTexture;

            public Chunk(GameObject owner, Mesh mesh, Texture2D groundTexture)
            {
                Owner = owner;
                Mesh = mesh;
                GroundTexture = groundTexture;
            }
        }
    }
}
