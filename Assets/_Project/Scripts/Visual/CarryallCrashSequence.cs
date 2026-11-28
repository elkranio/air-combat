using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AirCombat
{
    // Presentation only. Takes over the existing preview actors, never spawns combat entities.
    // Cargo and hull are stepped separately against the explicitly supplied terrain.
    internal sealed class CarryallCrashSequence : System.IDisposable
    {
        private readonly Transform carrier, cargo, owner;
        private readonly Terrain terrain;
        private readonly List<Vector3> hullBounds, cargoBounds;
        private readonly List<Paint> paints = new List<Paint>();
        private readonly Material plain, soft;
        private readonly Texture2D mask;
        private readonly GroundCrashFeedback impact;
        private readonly ParticleSystem smoke;
        private readonly CargoBreakupVisual cargoBreakup;
        private Vector3 hullVelocity, cargoVelocity;
        private Quaternion hullRotation, cargoRotation;
        private float elapsed, smokeCredit, sign, acceleration, drift;
        private bool hullLanded, cargoLanded, loaded;
        public bool Active { get; private set; }
        public bool HadPayload => loaded;
        public bool HullLanded => hullLanded;
        public bool CargoLanded => cargoLanded;
        public float RotorFactor => !Active || hullLanded ? 0f : Mathf.Exp(-elapsed * 2.3f);

        private sealed class Paint
        {
            public MeshRenderer renderer;
            public int slot;
            public Color color;
            public MaterialPropertyBlock previous;
        }

        public CarryallCrashSequence(Transform carrier, Transform cargo, Transform owner, Terrain terrain)
        {
            this.carrier = carrier; this.cargo = cargo; this.owner = owner; this.terrain = terrain;
            var visual = carrier.Find("Visual");
            hullBounds = BoundsPoints(carrier, visual);
            cargoBounds = BoundsPoints(cargo, cargo);
            cargoBreakup = new CargoBreakupVisual(cargo, owner, terrain);
            // Only authored/animated hull renderers; exhausts and cargo are not charred.
            if (visual != null)
            foreach (var r in visual.GetComponentsInChildren<MeshRenderer>(true))
            {
                var materials = r.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null || !materials[i].HasProperty("_BaseColor")) continue;
                    var previous = new MaterialPropertyBlock();
                    r.GetPropertyBlock(previous, i);
                    paints.Add(new Paint { renderer = r, slot = i, previous = previous,
                        color = materials[i].GetColor("_BaseColor") });
                }
            }
            var shader = Shader.Find("AirCombat/SandboxEffect");
            if (shader == null) return;
            plain = new Material(shader) { name = "CA05 debris (runtime)" };
            soft = RocketImpactFx.CreateMaterial(shader, out var texture);
            mask = texture;
            impact = new GroundCrashFeedback(owner, plain, soft, soft);
            smoke = CreateSmoke(owner, soft);
        }

        public void Begin(bool hasPayload, Vector3 velocity, float fallAcceleration, float minimumDrift)
        {
            if (Active) return;
            Active = true; loaded = hasPayload; hullLanded = false; cargoLanded = !loaded;
            elapsed = smokeCredit = 0f;
            acceleration = Mathf.Max(8f, fallAcceleration);
            drift = Mathf.Max(0f, minimumDrift);
            hullRotation = carrier.rotation;
            sign = carrier.right.x < 0f ? -1f : 1f;
            Vector3 forward = Vector3.ProjectOnPlane(carrier.forward, Vector3.up).normalized;
            Vector3 planar = Vector3.ClampMagnitude(Vector3.ProjectOnPlane(velocity, Vector3.up), 42f);
            hullVelocity = planar + forward * Mathf.Max(0f, drift - planar.magnitude);
            hullVelocity.y = Mathf.Clamp(velocity.y, -25f, 4f) - 2f;
            if (loaded)
            {
                // Preserve the contact pose exactly. Detachment and subsequent fall are independent.
                cargo.SetParent(owner, true);
                cargoRotation = cargo.rotation;
                // Inherit the actual crash drift, including the minimum forward impulse.
                // A small lateral separation keeps the two falling silhouettes readable.
                cargoVelocity = Vector3.ProjectOnPlane(hullVelocity, Vector3.up) * 1.05f
                    - carrier.right * sign * 1.5f;
                cargoVelocity.y = Mathf.Min(0f, velocity.y);
            }
            // Small loss-of-thrust flash, not the main explosion.
            Emit(carrier.position + carrier.up * 2f, Vector3.up * 2f, 5f, .22f,
                new Color(1f, .66f, .22f, .9f));
        }

        // Same CG01 breakup used by a direct ground kill, even without a hull crash.
        public void DestroyGroundCargo()
        {
            cargoBreakup.Break(Vector3.zero);
        }

        public void AdvanceCargoDebris(float dt) => cargoBreakup.Advance(dt);

        public void Advance(float dt)
        {
            if (!Active || dt <= 0f) return;
            float remaining = Mathf.Min(dt, .25f);
            while (remaining > .00001f && (!hullLanded || !cargoLanded))
            {
                float step = Mathf.Min(remaining, 1f / 60f); remaining -= step;
                elapsed += step;
                if (!hullLanded) StepHull(step);
                if (!cargoLanded) StepCargo(step);
            }
            cargoBreakup.Advance(dt);
            if (!hullLanded)
            {
                smokeCredit += dt * 16f;
                while (smokeCredit >= 1f)
                {
                    smokeCredit -= 1f;
                    Emit(carrier.TransformPoint(new Vector3(9f * sign, 3f, -4f)), Vector3.up * 2f,
                        4.5f, 1.8f, new Color(.17f, .16f, .15f, .72f));
                }
            }
        }

        private void StepHull(float dt)
        {
            hullVelocity.y -= acceleration * dt;
            Vector3 horizontal = Vector3.ProjectOnPlane(hullVelocity, Vector3.up);
            horizontal *= Mathf.Exp(-.24f * dt);
            hullVelocity = horizontal + Vector3.up * hullVelocity.y;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 1.25f));
            Quaternion rotation = hullRotation * Quaternion.Euler(18f * t, 10f * sign * t, 35f * sign * t);
            Vector3 p = carrier.position + hullVelocity * dt;
            float floor = ContactY(p, rotation, hullBounds);
            if (p.y > floor) { carrier.SetPositionAndRotation(p, rotation); return; }
            p.y = floor;
            carrier.SetPositionAndRotation(p, rotation);
            hullLanded = true;
            CharHull();
            impact?.Emit(new Vector3(p.x, GroundAt(p), p.z), null, 0f, 1.35f);
        }

        private void StepCargo(float dt)
        {
            cargoVelocity.y -= acceleration * 1.15f * dt;
            Vector3 p = cargo.position + cargoVelocity * dt;
            // Small, progressively increasing tilt; retain the actual release orientation.
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 1.15f));
            Quaternion rotation = cargoRotation * Quaternion.Euler(12f * t, -8f * sign * t, -24f * sign * t);
            float floor = ContactY(p, rotation, cargoBounds);
            if (p.y > floor) { cargo.SetPositionAndRotation(p, rotation); return; }
            p.y = floor;
            cargo.SetPositionAndRotation(p, rotation);
            cargoLanded = true;
            cargoBreakup.Break(cargoVelocity);
            // Restrained impact flash, not a second aircraft-sized fireball.
            Emit(p + Vector3.up * 1.4f, Vector3.up * 1.5f, 4f, .18f,
                new Color(1f, .77f, .38f, .82f));
            for (int i = 0; i < 10; i++)
            {
                float angle = i * Mathf.PI * .2f;
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 at = p + radial * 3f;
                at.y = GroundAt(at) + .6f;
                Emit(at, radial * 6f + Vector3.up * .5f, 6f, 1.1f, new Color(.75f, .61f, .43f, .45f));
            }
        }

        // Conservative mesh-bounds corners rather than one root-height threshold.
        // Flat preview pad: not a collision solver, and scenery is intentionally ignored.
        private static List<Vector3> BoundsPoints(Transform actor, Transform visual)
        {
            var points = new List<Vector3> { Vector3.zero };
            if (visual == null) return points;
            foreach (var f in visual.GetComponentsInChildren<MeshFilter>(true))
            {
                if (f.sharedMesh == null) continue;
                var b = f.sharedMesh.bounds;
                for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                    points.Add(actor.InverseTransformPoint(f.transform.TransformPoint(b.center +
                        Vector3.Scale(b.extents, new Vector3(x, y, z)))));
            }
            return points;
        }

        private float ContactY(Vector3 p, Quaternion rotation, List<Vector3> points)
        {
            float floor = float.NegativeInfinity;
            foreach (var local in points)
            {
                Vector3 offset = rotation * local;
                floor = Mathf.Max(floor, GroundAt(p + offset) - offset.y);
            }
            return floor + .12f;
        }

        private float GroundAt(Vector3 p) => GroundSurface.TerrainHeightClamped(terrain, p);

        private void CharHull()
        {
            foreach (var p in paints)
            {
                if (p.renderer == null) continue;
                var block = new MaterialPropertyBlock();
                p.renderer.GetPropertyBlock(block, p.slot);
                Color color = p.color;
                block.SetColor("_BaseColor", new Color(color.r * .32f, color.g * .30f, color.b * .28f, color.a));
                p.renderer.SetPropertyBlock(block, p.slot);
            }
        }

        private static ParticleSystem CreateSmoke(Transform owner, Material material)
        {
            var go = new GameObject("CA05 smoke and cargo dust"); go.transform.SetParent(owner, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = true; main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 128; main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = ps.emission; emission.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var fade = ps.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(.65f, 0f), new GradientAlphaKey(1f, .15f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var size = ps.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, .7f, 1f, 1.65f));
            var r = ps.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = material;
            r.renderMode = ParticleSystemRenderMode.Billboard; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            r.SetActiveVertexStreams(new List<ParticleSystemVertexStream> { ParticleSystemVertexStream.Position,
                ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV });
            ps.Play(); return ps;
        }

        private void Emit(Vector3 p, Vector3 velocity, float size, float life, Color color)
        {
            if (smoke == null) return;
            smoke.Emit(new ParticleSystem.EmitParams { position = p, velocity = velocity, startSize = size,
                startLifetime = life, startColor = color }, 1);
        }

        public void Reset()
        {
            cargoBreakup.Reset();
            Active = loaded = hullLanded = cargoLanded = false;
            elapsed = smokeCredit = 0f;
            foreach (var p in paints)
                if (p.renderer != null) p.renderer.SetPropertyBlock(p.previous.isEmpty ? null : p.previous, p.slot);
            impact?.Clear();
            if (smoke != null) smoke.Clear();
        }

        public void Dispose()
        {
            Reset(); cargoBreakup.Dispose(); impact?.Dispose();
            if (smoke != null) Object.Destroy(smoke.gameObject);
            if (plain != null) Object.Destroy(plain);
            if (soft != null) Object.Destroy(soft);
            if (mask != null) Object.Destroy(mask);
        }
    }
}
