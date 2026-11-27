using System.Collections.Generic;
using UnityEngine;

namespace AirCombat
{
    // BD02: one ground-domain target per prefab. Authored parts stay in the
    // original hierarchy, so retry never creates extra colliders or wrecks.
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
    public sealed class DepotBuildingTarget : DamageableTarget
    {
        [SerializeField, Min(1)] private int maxHp = 3;
        [SerializeField] private int hp;
        public int Hp => hp;
        public int MaxHp => maxHp;
        public bool IsDestroyed => initialized && hp <= 0;
        public override CombatTargetDomain Domain => CombatTargetDomain.Ground;
        public event System.Action<DepotBuildingTarget> Destroyed;

        private sealed class Part
        {
            public Transform tr;
            public Bounds bounds;
            public Vector3 origin, destination, scale;
            public Quaternion rotation, collapsedRotation;
            public bool foundation;
        }
        private sealed class Paint
        {
            public MeshRenderer renderer;
            public int slot;
            public Color color;
            public MaterialPropertyBlock previous;
        }
        private readonly List<Part> parts = new List<Part>();
        private readonly List<Paint> paints = new List<Paint>();
        private BoxCollider hitbox;
        private Transform visual;
        private Material plain, soft;
        private Texture2D mask;
        private GroundCrashFeedback impact;
        private bool initialized;
        private float age;
        private const float CollapseSeconds = .85f, CleanupStart = 3.5f, CleanupSeconds = 4f;

        private void Awake()
        {
            hitbox = GetComponent<BoxCollider>();
            visual = transform.Find("Visual");
            if (visual == null || visual.localPosition.sqrMagnitude > .000001f ||
                Quaternion.Angle(visual.localRotation, Quaternion.identity) > .01f ||
                (visual.localScale - Vector3.one).sqrMagnitude > .000001f)
            { Debug.LogError("BD02: expected identity Visual hierarchy.", this); hitbox.enabled = false; return; }
            foreach (Transform child in visual)
            {
                var filter = child.GetComponent<MeshFilter>();
                var renderer = child.GetComponent<MeshRenderer>();
                if (filter == null || filter.sharedMesh == null || renderer == null) continue;
                var part = new Part { tr = child, bounds = filter.sharedMesh.bounds,
                    origin = child.localPosition, rotation = child.localRotation, scale = child.localScale,
                    foundation = child.name == "Foundation" };
                PlanCollapse(part);
                parts.Add(part);
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null || !materials[i].HasProperty("_BaseColor")) continue;
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block, i);
                    paints.Add(new Paint { renderer = renderer, slot = i, previous = block,
                        color = materials[i].GetColor("_BaseColor") });
                }
            }
            if (parts.Count == 0)
            { Debug.LogError("BD02: no authored parts.", this); hitbox.enabled = false; return; }
            var shader = Shader.Find("AirCombat/SandboxEffect");
            if (shader != null)
            {
                plain = new Material(shader) { name = "BD02 fragments (runtime)" };
                soft = RocketImpactFx.CreateMaterial(shader, out mask);
                impact = new GroundCrashFeedback(transform, plain, soft, soft);
            }
            else Debug.LogWarning("BD02: FX shader missing; damage still works.", this);
            initialized = true;
            ResetTarget();
        }

        private static void PlanCollapse(Part p)
        {
            Vector3 shift = Vector3.zero;
            Vector3 tilt = Vector3.zero;
            float rest = .35f;
            switch (p.tr.name)
            {
                case "LeftWall": shift.x = -2.2f; tilt.z = 68f; break;
                case "RightWall": shift.x = 2.5f; tilt.z = -72f; break;
                case "RearWall": shift.z = -2.3f; tilt.x = -65f; break;
                case "FrontWall": shift.z = 2.7f; tilt.x = 72f; break;
                case "Roof01": shift = new Vector3(-1.3f, 0f, -.9f); tilt = new Vector3(9f, -8f, 11f); rest = .85f; break;
                case "Roof02": shift = new Vector3(1.1f, 0f, .7f); tilt = new Vector3(-12f, 7f, -14f); rest = .7f; break;
                case "Roof03": shift = new Vector3(-.7f, 0f, 1.6f); tilt = new Vector3(14f, -9f, 9f); rest = 1.1f; break;
                case "Cargo01": shift = new Vector3(-1.8f, 0f, 1.5f); tilt = new Vector3(6f, -15f, 20f); break;
                case "Cargo02": shift = new Vector3(1.8f, 0f, 1.4f); tilt = new Vector3(-8f, 18f, -16f); break;
            }
            p.collapsedRotation = Quaternion.Euler(tilt) * p.rotation;
            p.destination = p.origin + shift;
            p.destination.y = FloorY(p, p.collapsedRotation) + rest;
        }

        // Local mesh bounds, not rigid-body collision. The depot is authored
        // on a flat pad; corners must stay above its ground plane.
        private static float FloorY(Part p, Quaternion rotation)
        {
            float low = float.PositiveInfinity;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 corner = p.bounds.center + Vector3.Scale(p.bounds.extents, new Vector3(x,y,z));
                low = Mathf.Min(low, (rotation * Vector3.Scale(corner, p.tr.localScale)).y);
            }
            return -low;
        }

        public override bool ApplyDamage(Vector3 point, int damage)
        {
            if (!initialized || hp <= 0 || damage <= 0) return false;
            hp = Mathf.Max(0, hp - damage);
            if (hp > 0) return true;
            hitbox.enabled = false; // No further NURS damage or invisible full-height wreck collider.
            age = 0f;
            foreach (var paint in paints)
            {
                var block = new MaterialPropertyBlock();
                paint.renderer.GetPropertyBlock(block, paint.slot);
                var c = paint.color;
                block.SetColor("_BaseColor", new Color(c.r * .48f, c.g * .45f, c.b * .42f, c.a));
                paint.renderer.SetPropertyBlock(block, paint.slot);
            }
            impact?.Emit(transform.TransformPoint(new Vector3(hitbox.center.x, 0f, hitbox.center.z)), null, 0f, .95f);
            Destroyed?.Invoke(this);
            return true;
        }

        private void Update()
        {
            if (!IsDestroyed || age >= CleanupSeconds || MissionPause.IsTacticalMapPausedFor(gameObject.scene)) return;
            age = Mathf.Min(CleanupSeconds, age + Time.deltaTime);
            float t = Mathf.Clamp01(age / CollapseSeconds);
            float eased = t * t * (3f - 2f * t);
            float remaining = 1f - Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(CleanupStart, CleanupSeconds, age));
            foreach (var part in parts)
            {
                Quaternion rotation = part.foundation ? part.rotation : Quaternion.Slerp(part.rotation, part.collapsedRotation, eased);
                Vector3 position = part.foundation ? part.origin : Vector3.Lerp(part.origin, part.destination, eased);
                // Shrink only during the last half-second, with the bottom kept
                // at its previous ground clearance (no floating shrinking wreck).
                part.tr.localScale = part.scale;
                float floor = FloorY(part, rotation);
                float clearance = Mathf.Max(part.foundation ? 0f : .03f, position.y - floor);
                part.tr.localScale = part.scale * remaining;
                position.y = FloorY(part, rotation) + clearance * remaining;
                part.tr.localPosition = position;
                part.tr.localRotation = rotation;
            }
            // Keep the receiver active so mission state and R still see it.
            if (age >= CleanupSeconds) visual.gameObject.SetActive(false);
        }

        public void ResetTarget()
        {
            if (!initialized) return;
            hp = Mathf.Max(1, maxHp);
            age = 0f;
            visual.gameObject.SetActive(true);
            foreach (var part in parts)
            {
                part.tr.localScale = part.scale;
                part.tr.localPosition = part.origin;
                part.tr.localRotation = part.rotation;
            }
            foreach (var paint in paints)
                if (paint.renderer != null) paint.renderer.SetPropertyBlock(paint.previous.isEmpty ? null : paint.previous, paint.slot);
            impact?.Clear();
            hitbox.enabled = true;
        }

        private void OnDestroy()
        {
            impact?.Dispose();
            if (plain != null) Destroy(plain);
            if (soft != null) Destroy(soft);
            if (mask != null) Destroy(mask);
        }
    }
}
