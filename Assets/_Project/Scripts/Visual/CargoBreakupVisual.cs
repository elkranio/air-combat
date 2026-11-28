using System.Collections.Generic;
using UnityEngine;

namespace AirCombat
{
    // Reusable visual-only CG01 breakup. Copies only the authored mesh assemblies.
    // The original cargo hierarchy, capture sockets, meshes and materials stay intact.
    internal sealed class CargoBreakupVisual : System.IDisposable
    {
        private sealed class Piece
        {
            public Transform transform;
            public Bounds bounds;
            public Vector3 velocity, spin, scale;
            public bool landed;
        }

        private readonly Transform cargo, visual, owner;
        private readonly Terrain terrain;
        private readonly bool originalVisibility;
        private readonly List<Piece> pieces = new List<Piece>();
        private GameObject debris;
        private float age;
        private bool broken;

        internal CargoBreakupVisual(Transform cargo, Transform owner, Terrain terrain)
        {
            this.cargo = cargo;
            this.owner = owner;
            this.terrain = terrain;
            visual = cargo.Find("Visual");
            originalVisibility = visual != null && visual.gameObject.activeSelf;
        }

        internal void Break(Vector3 inheritedVelocity)
        {
            if (broken || visual == null) return;
            var filters = visual.GetComponentsInChildren<MeshFilter>(false);
            if (filters.Length == 0) return;
            debris = new GameObject("CG01 / impact wreckage");
            debris.transform.SetParent(owner, false);
            Vector3 carry = Vector3.ClampMagnitude(Vector3.ProjectOnPlane(inheritedVelocity, Vector3.up), 24f) * .35f;
            // Independent, repeatable fragment profiles: heavy base, larger panels,
            // lighter fixtures. No Rigidbody or colliders on these visual copies.
            var rng = new System.Random(3051);
            float Next(float lo, float hi) => Mathf.Lerp(lo, hi, (float)rng.NextDouble());
            foreach (var f in filters)
            {
                var source = f.GetComponent<MeshRenderer>();
                if (f.sharedMesh == null || source == null || !source.enabled) continue;
                var go = new GameObject(f.name + " / wreck", typeof(MeshFilter), typeof(MeshRenderer));
                go.layer = f.gameObject.layer;
                var tr = go.transform;
                tr.SetParent(debris.transform, false);
                tr.SetPositionAndRotation(f.transform.position, f.transform.rotation);
                tr.localScale = f.transform.lossyScale; // Preview owner is unit scale.
                go.GetComponent<MeshFilter>().sharedMesh = f.sharedMesh;
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterials = source.sharedMaterials;
                renderer.shadowCastingMode = source.shadowCastingMode;
                renderer.receiveShadows = source.receiveShadows;
                // Copy any existing per-instance paint overrides as well.
                var block = new MaterialPropertyBlock();
                source.GetPropertyBlock(block);
                if (!block.isEmpty) renderer.SetPropertyBlock(block);
                for (int i = 0; i < source.sharedMaterials.Length; i++)
                {
                    source.GetPropertyBlock(block, i);
                    if (!block.isEmpty) renderer.SetPropertyBlock(block, i);
                }
                Vector3 radial = Vector3.ProjectOnPlane(tr.position - cargo.position, Vector3.up);
                if (radial.sqrMagnitude < .1f)
                    radial = cargo.right * (f.name == "Lid" ? 1f : -1f);
                radial = (radial.normalized + Vector3.ProjectOnPlane(cargo.forward, Vector3.up) * Next(-.25f, .25f)).normalized;
                bool heavy = f.name == "Cradle" || f.name == "Core";
                bool small = f.name.StartsWith("Capture") || f.name.StartsWith("Rib");
                float push = heavy ? Next(1.2f, 3f) : small ? Next(7f, 11f) : Next(4.5f, 7.5f);
                float lift = heavy ? Next(.8f, 2f) : small ? Next(4.5f, 8f) : Next(3.5f, 5.5f);
                float spin = heavy ? 18f : small ? 150f : 70f;
                pieces.Add(new Piece { transform = tr, bounds = f.sharedMesh.bounds, scale = tr.localScale,
                    velocity = carry * (heavy ? .5f : 1f) + radial * push + Vector3.up * lift,
                    spin = new Vector3(Next(-spin, spin), Next(-spin, spin), Next(-spin, spin)) });
            }
            if (pieces.Count == 0) { Object.Destroy(debris); debris = null; return; }
            if (pieces.Count != 13) Debug.LogWarning("CG01 breakup: expected 13 visual assemblies, got " + pieces.Count, cargo);
            broken = true;
            age = 0f;
            visual.gameObject.SetActive(false);
        }

        internal void Advance(float dt)
        {
            if (!broken || debris == null || dt <= 0f) return;
            age += dt;
            const float fadeStart = 4.5f, lifetime = 6f;
            if (age >= lifetime) { RemoveDebris(); return; }
            float remaining = Mathf.Min(dt, .25f);
            while (remaining > .00001f)
            {
                float step = Mathf.Min(remaining, 1f / 60f);
                remaining -= step;
                foreach (var p in pieces)
                {
                    if (p.landed) continue;
                    p.velocity += Vector3.down * (24f * step);
                    var tr = p.transform;
                    Quaternion rotation = tr.rotation * Quaternion.Euler(p.spin * step);
                    Vector3 next = tr.position + p.velocity * step;
                    float floor = ContactY(next, rotation, p.bounds, p.scale);
                    if (next.y <= floor)
                    {
                        next.y = floor;
                        if (p.velocity.y <= 0f) p.landed = true;
                    }
                    tr.SetPositionAndRotation(next, rotation);
                }
            }
            if (age >= fadeStart)
            {
                float scale = Mathf.Clamp01((lifetime - age) / (lifetime - fadeStart));
                foreach (var p in pieces)
                {
                    var tr = p.transform;
                    tr.localScale = p.scale * scale;
                    // Keep the shrinking fragment on its contact plane rather than floating.
                    if (p.landed)
                    {
                        Vector3 pos = tr.position;
                        pos.y = ContactY(pos, tr.rotation, p.bounds, tr.localScale);
                        tr.position = pos;
                    }
                }
            }
        }

        private float ContactY(Vector3 position, Quaternion rotation, Bounds bounds, Vector3 scale)
        {
            float floor = float.NegativeInfinity;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                Vector3 offset = rotation * Vector3.Scale(corner, scale);
                Vector3 point = position + offset;
                float ground = GroundSurface.TerrainHeightClamped(terrain, point);
                floor = Mathf.Max(floor, ground - offset.y);
            }
            return floor + .08f;
        }

        private void RemoveDebris()
        {
            if (debris != null)
            {
                debris.SetActive(false); // Destroy is deferred; never show both versions on R.
                Object.Destroy(debris);
            }
            debris = null;
            pieces.Clear();
        }

        internal void Reset()
        {
            RemoveDebris();
            if (broken && visual != null) visual.gameObject.SetActive(originalVisibility);
            broken = false;
            age = 0f;
        }

        public void Dispose() => Reset();
    }
}
