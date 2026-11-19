using System.Collections.Generic;
using UnityEngine;
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
            if (!force && !appearanceChanged && center == previousCenter && radius == previousRadius)
                return;
            if (appearanceChanged)
                ClearChunks();

            // Keep only the tiles around the camera; world positions never move or wrap.
            expired.Clear();
            foreach (var pair in chunks)
            {
                var delta = pair.Key - center;
                if (Mathf.Abs(delta.x) > radius.x || Mathf.Abs(delta.y) > radius.y)
                    expired.Add(pair.Key);
            }
            foreach (var key in expired)
            {
                Release(chunks[key]);
                chunks.Remove(key);
            }

            for (int z = -radius.y; z <= radius.y; z++)
            {
                for (int x = -radius.x; x <= radius.x; x++)
                {
                    var key = center + new Vector2Int(x, z);
                    if (!chunks.ContainsKey(key))
                        chunks.Add(key, CreateChunk(key));
                }
            }
            previousCenter = center;
            previousRadius = radius;
            previousSeed = seed;
            previousDensity = treeDensity;
        }

        private Chunk CreateChunk(Vector2Int key)
        {
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
            foreach (var pair in chunks)
                Release(pair.Value);
            chunks.Clear();
        }

        private static void Release(Chunk chunk)
        {
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
