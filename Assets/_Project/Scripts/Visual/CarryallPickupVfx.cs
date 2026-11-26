using System;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace AirCombat
{
    // Instance-only presentation. The pickup state machine remains the sole pose/payload owner.
    internal sealed class CarryallPickupVfx : IDisposable
    {
        private readonly Transform carrier;
        private readonly Terrain terrain;
        private readonly Transform[] sockets = new Transform[4];
        private readonly Transform[] clamps = new Transform[4];
        private readonly Transform[] plumes = new Transform[4];
        private float dustCredit;
        private Mesh plumeMesh;
        private Material plumeMaterial, particleMaterial, dustMaterial;
        private Texture2D mask, dustMask;
        private ParticleSystem particles, dust;
        private float thrust, age;
        private bool wasLoaded;
        private bool disposed;

        private CarryallPickupVfx(Transform carrier, Terrain terrain)
        { this.carrier = carrier; this.terrain = terrain; }

        public static CarryallPickupVfx Create(Transform carrier, Transform owner, Terrain terrain)
        {
            var shader = Shader.Find("AirCombat/SandboxEffect");
            if (shader == null)
            {
                Debug.LogWarning("CA04: effect shader missing; pickup still works.", owner);
                return null;
            }
            var fx = new CarryallPickupVfx(carrier, terrain);
            try
            {
                int i = 0;
                foreach (string end in new[] { "Front", "Rear" })
                foreach (string side in new[] { "L", "R" })
                    fx.sockets[i++] = carrier.Find($"EngineSockets/Exhaust_{side}_{end}");
                for (i = 0; i < 4; i++)
                {
                    fx.clamps[i] = carrier.Find($"ClampSockets/Clamp_{i+1:00}");
                    if (fx.sockets[i] == null || fx.clamps[i] == null ||
                        Vector3.Dot(fx.sockets[i].forward, -carrier.up) < .99f)
                        throw new InvalidOperationException("Missing or misaligned CA01 socket.");
                }
                fx.BuildPlumes(shader);
                fx.BuildParticles(shader, owner);
                return fx;
            }
            catch (Exception ex)
            {
                fx.Dispose();
                Debug.LogWarning("CA04: visual setup failed; pickup still works. " + ex.Message, owner);
                return null;
            }
        }

        public void Reset()
        {
            if (disposed) return;
            Clear();
            wasLoaded = false;
            age = 0f;
            thrust = .62f;
            ApplyPlumes(true, 1f);
        }

        public void Clear()
        {
            dustCredit = 0f;
            foreach (var plume in plumes)
                if (plume != null) plume.gameObject.SetActive(false);
            if (particles != null) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (dust != null) dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public void Tick(float dt, CarryallPickupSequence.Phase phase, bool loaded,
            float exhaustScale, float dustRate, bool clampPulse)
        {
            if (disposed || dt <= 0f) return;
            bool active = phase != CarryallPickupSequence.Phase.Failed && carrier.gameObject.activeInHierarchy;
            if (!active) { Clear(); return; }
            age += dt;
            float target;
            switch (phase)
            {
                case CarryallPickupSequence.Phase.Descend: target = .40f; break;
                case CarryallPickupSequence.Phase.Clamp: target = .55f; break;
                case CarryallPickupSequence.Phase.Lift: target = 1f; break;
                case CarryallPickupSequence.Phase.Depart: target = .84f; break;
                case CarryallPickupSequence.Phase.Complete: target = .72f; break;
                default: target = .62f; break;
            }
            thrust = Mathf.Lerp(thrust, target, 1f - Mathf.Exp(-dt * 5f));
            ApplyPlumes(exhaustScale > 0f, Mathf.Max(0f, exhaustScale));
            if (particles == null) return;
            if (!particles.isPlaying) particles.Play(true);
            if (loaded && !wasLoaded && clampPulse) EmitClampPulse();
            wasLoaded = loaded;
            // Treat the four jets as one ground-level flow field. Keep the old
            // Inspector rate as a density knob (7 -> up to 56 particles/s total).
            if (dust == null) return;
            if (!dust.isPlaying) dust.Play(true);
            if (!GroundAt(carrier.position, out float y)) { dustCredit = 0f; return; }
            float clearance = carrier.position.y + .75f - y;
            float near = clearance > 0f ? 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(7f, 32f, clearance)) : 0f;
            if (near <= 0f || dustRate <= 0f) { dustCredit = 0f; return; }
            dustCredit += Mathf.Min(dt, .12f) * dustRate * 8f * near * Mathf.Lerp(.70f, 1.3f, thrust);
            int count = Mathf.Min(12, Mathf.FloorToInt(dustCredit));
            dustCredit = Mathf.Min(1f, dustCredit - count);
            for (int k = 0; k < count; k++) EmitDust(near);
        }

        private void ApplyPlumes(bool visible, float scale)
        {
            for (int i = 0; i < plumes.Length; i++)
            {
                var plume = plumes[i];
                if (plume == null) continue;
                float flicker = 1f + .055f * Mathf.Sin(age * 31f + i * 1.9f);
                float width = Mathf.Lerp(.75f, 1.35f, thrust) * Mathf.Sqrt(scale);
                float length = Mathf.Lerp(2.8f, 10f, thrust) * scale * flicker;
                if (GroundAt(sockets[i].position, out float ground))
                    length = Mathf.Min(length, Mathf.Max(0f, sockets[i].position.y - ground - .25f));
                bool on = visible && length > .03f && width > .03f;
                if (plume.gameObject.activeSelf != on) plume.gameObject.SetActive(on);
                if (on) plume.localScale = new Vector3(width, width, length);
            }
        }

        private bool GroundAt(Vector3 p, out float y)
        {
            y = 0f;
            if (terrain == null || terrain.terrainData == null) return false;
            var origin = terrain.transform.position;
            var size = terrain.terrainData.size;
            if (p.x < origin.x || p.x > origin.x + size.x || p.z < origin.z || p.z > origin.z + size.z) return false;
            y = terrain.SampleHeight(p) + origin.y;
            return true;
        }

        private void EmitDust(float near)
        {
            // Uniform azimuth, broad overlapping annulus, no engine-specific lobes.
            float angle = UnityEngine.Random.Range(0f, 2f * Mathf.PI);
            Vector3 outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            float radius = Mathf.Sqrt(UnityEngine.Random.Range(36f, 256f));
            Vector3 p = carrier.position + outward * radius;
            if (!GroundAt(p, out float y)) return;
            p.y = y + UnityEngine.Random.Range(.38f, .75f);
            var ink = new Color(.77f, .64f, .45f, Mathf.Lerp(.13f, .32f, near));
            dust.Emit(new ParticleSystem.EmitParams
            {
                position = p,
                velocity = outward * UnityEngine.Random.Range(5f, 9f) + Vector3.up * UnityEngine.Random.Range(.12f, .32f),
                startSize = UnityEngine.Random.Range(12f, 19f), startLifetime = UnityEngine.Random.Range(1.5f, 2.2f),
                rotation = UnityEngine.Random.Range(0f, 360f), startColor = ink
            }, 1);
        }

        private void EmitClampPulse()
        {
            foreach (var clamp in clamps)
            {
                if (clamp == null) continue;
                Vector3 outward = Vector3.ProjectOnPlane(clamp.position - carrier.position, Vector3.up).normalized;
                particles.Emit(new ParticleSystem.EmitParams
                {
                    position = clamp.position + outward * .45f + Vector3.up * .1f,
                    velocity = outward * 6f + Vector3.up * .5f,
                    startSize = 1.25f, startLifetime = .23f, startColor = new Color(1f, .76f, .35f, .8f)
                }, 1);
            }
        }

        private void BuildPlumes(Shader shader)
        {
            plumeMaterial = new Material(shader) { name = "CA01 downwash (runtime)" };
            plumeMesh = MakePlume();
            for (int i = 0; i < sockets.Length; i++)
            {
                var go = new GameObject("CA01 downward thrust", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(sockets[i], false);
                plumes[i] = go.transform;
                go.layer = carrier.gameObject.layer;
                go.GetComponent<MeshFilter>().sharedMesh = plumeMesh;
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = plumeMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                go.SetActive(false);
            }
        }

        private static Mesh MakePlume()
        {
            const int sides = 12;
            float[] z = { 0f, .18f, .48f, .78f, 1f };
            float[] r = { .82f, 1f, .70f, .38f, .025f };
            Color[] ink = { new Color(.84f,.95f,1f,.8f), new Color(.60f,.85f,1f,.68f),
                new Color(.42f,.72f,.92f,.40f), new Color(.36f,.64f,.83f,.13f), new Color(.36f,.64f,.83f,0f) };
            var v = new Vector3[z.Length * sides]; var colors = new Color[v.Length];
            var uv = new Vector2[v.Length]; var triangles = new int[(z.Length-1)*sides*6];
            for (int ring = 0; ring < z.Length; ring++)
            for (int side = 0; side < sides; side++)
            {
                int i = ring * sides + side;
                float a = side * 2f * Mathf.PI / sides;
                v[i] = new Vector3(r[ring]*Mathf.Cos(a), r[ring]*Mathf.Sin(a), z[ring]);
                colors[i] = ink[ring]; uv[i] = new Vector2(.5f,.5f);
                if (ring == z.Length-1) continue;
                int next = ring*sides+(side+1)%sides, t = i*6;
                triangles[t]=i; triangles[t+1]=next; triangles[t+2]=i+sides;
                triangles[t+3]=next; triangles[t+4]=next+sides; triangles[t+5]=i+sides;
            }
            var mesh = new Mesh { name = "CA01 tapered downwash", vertices = v, colors = colors, uv = uv, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }

        private void BuildParticles(Shader shader, Transform owner)
        {
            mask = new Texture2D(32, 32, TextureFormat.RGBA32, false, true)
                { name = "CA01 soft puff mask", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[32*32];
            for (int y=0; y<32; y++) for (int x=0; x<32; x++)
            {
                float r = new Vector2((x+.5f)/16f-1f,(y+.5f)/16f-1f).magnitude;
                float a = 1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.15f,1f,r));
                pixels[y*32+x] = new Color32(255,255,255,(byte)Mathf.RoundToInt(a*255f));
            }
            mask.SetPixels32(pixels); mask.Apply(false,true);
            particleMaterial = new Material(shader) { name = "CA01 dust and clamp pulse (runtime)" };
            particleMaterial.SetTexture("_ParticleMask",mask);
            var go = new GameObject("CA01 dust and clamp pulse");
            go.transform.SetParent(owner,false);
            go.layer = carrier.gameObject.layer;
            particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = true; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate; main.maxParticles = 256;
            main.startSpeed = 0f; main.gravityModifier = 0f;
            var emission = particles.emission; emission.enabled = false;
            var shape = particles.shape; shape.enabled = false;
            var size = particles.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f,AnimationCurve.Linear(0f,.65f,1f,1.6f));
            var fade = particles.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white,0f),new GradientColorKey(Color.white,1f) },
                new[] { new GradientAlphaKey(.85f,0f),new GradientAlphaKey(1f,.15f),new GradientAlphaKey(0f,1f) });
            fade.color = new ParticleSystem.MinMaxGradient(gradient);
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = particleMaterial; renderer.renderMode = ParticleSystemRenderMode.Billboard;
            // SandboxEffect consumes POSITION, COLOR and TEXCOORD0 (the radial mask UV).
            renderer.SetActiveVertexStreams(new System.Collections.Generic.List<ParticleSystemVertexStream>
            { ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV });
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            BuildGroundDust(shader, owner);
        }

        private void BuildGroundDust(Shader shader, Transform owner)
        {
            const int resolution = 64;
            dustMask = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, true)
                { name = "CA01 irregular ground dust mask", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[resolution * resolution];
            for (int y = 0; y < resolution; y++) for (int x = 0; x < resolution; x++)
            {
                float u = (x + .5f) / resolution, v = (y + .5f) / resolution;
                float r = new Vector2(u * 2f - 1f, v * 2f - 1f).magnitude;
                float edge = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.05f, 1f, r));
                float noise = .5f * Mathf.PerlinNoise(u * 4f + 7f, v * 4f + 11f) +
                    .5f * Mathf.PerlinNoise(u * 8f + 21f, v * 8f + 15f);
                float alpha = edge * Mathf.Lerp(.35f, 1f, noise);
                pixels[y * resolution + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
            dustMask.SetPixels32(pixels); dustMask.Apply(false, true);
            dustMaterial = new Material(shader) { name = "CA01 ground wash (runtime)" };
            dustMaterial.SetTexture("_ParticleMask", dustMask);
            var go = new GameObject("CA01 unified ground wash");
            go.transform.SetParent(owner, false);
            go.layer = carrier.gameObject.layer;
            dust = go.AddComponent<ParticleSystem>();
            dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = dust.main;
            main.loop = true; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate; main.maxParticles = 384;
            main.startSpeed = 0f; main.gravityModifier = 0f;
            var emission = dust.emission; emission.enabled = false;
            var shape = dust.shape; shape.enabled = false;
            var size = dust.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, .9f, 1f, 1.65f));
            var fade = dust.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(.35f, 0f), new GradientAlphaKey(1f, .2f), new GradientAlphaKey(.6f, .55f), new GradientAlphaKey(0f, 1f) });
            fade.color = new ParticleSystem.MinMaxGradient(gradient);
            var renderer = dust.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = dustMaterial;
            renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            renderer.SetActiveVertexStreams(new System.Collections.Generic.List<ParticleSystemVertexStream>
            { ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV });
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        }

        public void Dispose()
        {
            if (disposed) return;
            Clear(); disposed = true;
            foreach (var plume in plumes) if (plume != null) Object.Destroy(plume.gameObject);
            if (particles != null) Object.Destroy(particles.gameObject);
            if (plumeMesh != null) Object.Destroy(plumeMesh);
            if (plumeMaterial != null) Object.Destroy(plumeMaterial);
            if (particleMaterial != null) Object.Destroy(particleMaterial);
            if (mask != null) Object.Destroy(mask);
            if (dust != null) Object.Destroy(dust.gameObject);
            if (dustMaterial != null) Object.Destroy(dustMaterial);
            if (dustMask != null) Object.Destroy(dustMask);
        }
    }
}
