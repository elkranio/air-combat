using System.Collections.Generic;
using UnityEngine;

namespace AirCombat
{
    // Visual-only aircraft loss. Copies authored rigid hull geometry, never
    // instantiates the combat prefab, colliders, controllers, weapons or sockets.
    // The scene FX owner steps and disposes it independently of encounter state.
    public sealed class AircraftWreck
    {
        private readonly Transform root;
        private readonly GroundSurface surface;
        private readonly Vector3 anchorLocal;
        private readonly Vector3 heading;
        private readonly Quaternion initialRotation;
        private readonly float rollRate, turnRate;
        private readonly float initialSpeed;
        private float elapsed, fallSpeed = 7f;
        public readonly bool HasAnchor;
        public Vector3 AnchorPosition => root.TransformPoint(anchorLocal);
        public Vector3 PreviousAnchor;
        public float SmokeDebt = 1f, CloudDebt = 1f, FireDebt = 1f;
        public Vector3 ImpactPoint { get; private set; }
        public bool HitGround { get; private set; }

        private AircraftWreck(Transform root, Vector3 forward, float speed,
            bool hasAnchor, Vector3 anchor, System.Random rng, GroundSurface surface)
        {
            this.root = root;
            this.surface = surface;
            heading = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            initialRotation = root.rotation;
            initialSpeed = Mathf.Clamp(speed, 15f, 180f);
            float sign = rng.Next(2) == 0 ? -1f : 1f;
            rollRate = sign * Mathf.Lerp(125f, 205f, (float)rng.NextDouble());
            turnRate = sign * Mathf.Lerp(6f, 16f, (float)rng.NextDouble());
            HasAnchor = hasAnchor;
            anchorLocal = hasAnchor ? root.InverseTransformPoint(anchor) : Vector3.zero;
            PreviousAnchor = AnchorPosition;
        }

        public static AircraftWreck Create(Transform aircraft, MeshFilter[] hull,
            float speed, DamageSurfaceAnchor anchor, System.Random rng) =>
            aircraft == null ? null : Create(aircraft, hull, speed, anchor, rng,
                new GroundSurface(aircraft.gameObject.scene));

        internal static AircraftWreck Create(Transform aircraft, MeshFilter[] hull,
            float speed, DamageSurfaceAnchor anchor, System.Random rng, GroundSurface surface)
        {
            if (aircraft == null || hull == null || hull.Length == 0) return null;
            var go = new GameObject("Aircraft wreck / " + aircraft.name);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, aircraft.gameObject.scene);
            var root = go.transform;
            root.SetPositionAndRotation(aircraft.position, aircraft.rotation);
            root.localScale = aircraft.lossyScale;
            var copies = new Dictionary<Transform, Transform> { { aircraft, root } };
            int count = 0;
            foreach (var filter in hull)
            {
                if (filter == null || filter.sharedMesh == null || !filter.gameObject.activeInHierarchy) continue;
                var source = filter.GetComponent<MeshRenderer>();
                if (source == null || !source.enabled) continue;
                var part = CopyPath(filter.transform, aircraft, copies);
                if (part == null) continue;
                part.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var renderer = part.gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = source.sharedMaterials;
                renderer.shadowCastingMode = source.shadowCastingMode;
                renderer.receiveShadows = source.receiveShadows;
                // Copy existing material overrides without duplicating materials.
                var block = new MaterialPropertyBlock();
                source.GetPropertyBlock(block);
                if (!block.isEmpty) renderer.SetPropertyBlock(block);
                for (int i = 0; i < source.sharedMaterials.Length; i++)
                {
                    source.GetPropertyBlock(block, i);
                    if (!block.isEmpty) renderer.SetPropertyBlock(block, i);
                }
                count++;
            }
            if (count == 0) { Object.Destroy(go); return null; }
            return new AircraftWreck(root, aircraft.forward, speed, anchor.IsValid,
                anchor.IsValid ? anchor.WorldPosition : aircraft.position, rng, surface);
        }

        private static Transform CopyPath(Transform source, Transform aircraft,
            Dictionary<Transform, Transform> copies)
        {
            if (source == null) return null;
            if (copies.TryGetValue(source, out var existing)) return existing;
            if (!source.IsChildOf(aircraft)) return null;
            var parent = CopyPath(source.parent, aircraft, copies);
            if (parent == null) return null;
            var part = new GameObject(source.name);
            part.layer = source.gameObject.layer;
            var transform = part.transform;
            transform.SetParent(parent, false);
            transform.localPosition = source.localPosition;
            transform.localRotation = source.localRotation;
            transform.localScale = source.localScale;
            copies.Add(source, transform);
            return transform;
        }

        // True while airborne. Small substeps catch the terrain crossing even
        // during low FPS; no physics body or colliders participate in combat.
        public bool Step(float dt)
        {
            if (root == null || HitGround) return false;
            float remaining = Mathf.Min(Mathf.Max(0f, dt), .25f);
            while (remaining > .00001f)
            {
                float step = Mathf.Min(remaining, 1f / 60f);
                remaining -= step;
                Vector3 before = root.position;
                elapsed += step;
                fallSpeed += 30f * step;
                Vector3 direction = Quaternion.AngleAxis(turnRate * elapsed, Vector3.up) * heading;
                float speed = initialSpeed * Mathf.Exp(-.85f * elapsed);
                Vector3 next = before + (direction * speed - Vector3.up * fallSpeed) * step;
                float ground = GroundHeight(next);
                if (next.y <= ground + 1.5f)
                {
                    float previousClearance = before.y - GroundHeight(before) - 1.5f;
                    float nextClearance = next.y - ground - 1.5f;
                    float fraction = Mathf.Clamp01(previousClearance /
                        Mathf.Max(.0001f, previousClearance - nextClearance));
                    Vector3 contact = Vector3.Lerp(before, next, fraction);
                    ImpactPoint = new Vector3(contact.x, GroundHeight(contact), contact.z);
                    root.position = ImpactPoint + Vector3.up * 1.5f;
                    HitGround = true;
                    return false;
                }
                float pitch = Mathf.Lerp(0f, 68f, Mathf.Clamp01(elapsed / 1.2f));
                root.SetPositionAndRotation(next,
                    Quaternion.AngleAxis(turnRate * elapsed, Vector3.up) * initialRotation *
                    Quaternion.Euler(pitch, 0f, rollRate * elapsed));
            }
            return elapsed < 8f; // Safety bound for unsupported/off-map terrain.
        }

        private float GroundHeight(Vector3 point) => surface.HeightOr(point);

        public void Dispose()
        {
            if (root == null) return;
            root.gameObject.SetActive(false);
            Object.Destroy(root.gameObject);
        }
    }
}
