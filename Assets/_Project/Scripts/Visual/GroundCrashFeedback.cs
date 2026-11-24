using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AirCombat
{
    // Scene-owned, visual-only ground explosion. Reusable for other destroyed
    // objects; no damage, colliders, transient lights or per-impact GameObjects.
    public sealed class GroundCrashFeedback
    {
        private readonly ParticleSystem fire, smoke, dust, fragments, sparks;
        private readonly AudioClip boom;
        private readonly System.Random random = new System.Random(4281);

        public GroundCrashFeedback(Transform parent, Material plain, Material hot, Material cloud)
        {
            fire = Make(parent, "Crash / fireball", hot, 96, 0f, .55f, 1.65f, false);
            smoke = Make(parent, "Crash / rising smoke", cloud, 192, 0f, .55f, 2.0f, true);
            dust = Make(parent, "Crash / ground dust", cloud, 192, 0f, .55f, 1.9f, true);
            fragments = Make(parent, "Crash / fragments", plain, 192, 2.8f, 1f, .8f, false);
            sparks = Make(parent, "Crash / sparks", plain, 128, 1.7f, 1f, .65f, false, true);
            boom = MakeBoom();
        }

        private static ParticleSystem Make(Transform parent, string name, Material material,
            int capacity, float gravity, float startScale, float endScale, bool soft, bool stretch = false)
        {
            if (material == null) return null;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.maxParticles = capacity;
            main.gravityModifier = gravity;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f) }, soft
                ? new[] { new GradientAlphaKey(.15f, 0f), new GradientAlphaKey(1f, .13f),
                    new GradientAlphaKey(.65f, .55f), new GradientAlphaKey(0f, 1f) }
                : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(.9f, .2f),
                    new GradientAlphaKey(0f, 1f) });
            fade.color = new ParticleSystem.MinMaxGradient(gradient);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, startScale, 1f, endScale));
            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            renderer.lengthScale = stretch ? 2f : 1f;
            renderer.velocityScale = stretch ? .06f : 0f;
            renderer.cameraVelocityScale = 0f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
            {
                ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color,
                ParticleSystemVertexStream.UV
            });
            ps.Play();
            return ps;
        }

        // Strength is presentation scale only, not blast radius or damage.
        public void Emit(Vector3 point, AudioSource audio, float volume, float strength = 1f)
        {
            float scale = Mathf.Clamp(strength, .25f, 3f);
            Vector3 origin = point + Vector3.up * 1.3f * scale;
            EmitOne(fire, origin, Vector3.up * 3f, 17f * scale, .18f,
                new Color(1f, .96f, .69f, 1f));
            for (int i = 0; i < 12; i++)
            {
                Vector3 outward = Radial(i, 12);
                EmitOne(fire, origin + outward * R(.5f, 2.5f) * scale,
                    (outward * R(6f, 15f) + Vector3.up * R(5f, 13f)) * scale,
                    R(6f, 11f) * scale, R(.28f, .65f), i % 3 == 0
                        ? new Color(1f, .86f, .31f, .95f) : new Color(1f, .36f, .06f, .88f));
            }
            for (int i = 0; i < 18; i++)
            {
                Vector3 outward = Radial(i, 18);
                float tone = R(.15f, .24f);
                EmitOne(smoke, origin + outward * R(.3f, 3f) * scale,
                    (outward * R(1f, 4.5f) + Vector3.up * R(3f, 8f)) * scale,
                    R(5.5f, 11f) * scale, R(1.5f, 2.8f), new Color(tone, tone * .95f, tone * .89f, R(.42f, .65f)));
            }
            for (int i = 0; i < 20; i++)
            {
                Vector3 outward = Radial(i, 20);
                EmitOne(dust, point + (outward * R(1f, 3f) + Vector3.up * .9f) * scale,
                    (outward * R(9f, 20f) + Vector3.up * R(.5f, 2f)) * scale,
                    R(4f, 8f) * scale, R(.8f, 1.5f), new Color(.66f, .53f, .39f, R(.27f, .46f)));
            }
            for (int i = 0; i < 22; i++)
            {
                Vector3 outward = Radial(i, 22);
                EmitOne(fragments, origin, (outward * R(10f, 25f) + Vector3.up * R(7f, 19f)) * scale,
                    R(.35f, 1.15f) * scale, R(.55f, 1.25f), new Color(.16f, .14f, .12f, 1f));
            }
            for (int i = 0; i < 18; i++)
            {
                Vector3 outward = Radial(i, 18);
                EmitOne(sparks, origin, (outward * R(15f, 38f) + Vector3.up * R(8f, 25f)) * scale,
                    R(.3f, .65f) * scale, R(.25f, .6f), new Color(1f, .66f, .18f, 1f));
            }
            if (audio != null && boom != null && volume > 0f)
                audio.PlayOneShot(boom, Mathf.Clamp01(volume));
        }

        private void EmitOne(ParticleSystem system, Vector3 point, Vector3 velocity,
            float size, float life, Color color)
        {
            if (system == null) return;
            system.Emit(new ParticleSystem.EmitParams
            {
                position = point, velocity = velocity, startSize = size,
                startLifetime = life, startColor = color, rotation = R(0f, 360f)
            }, 1);
        }

        private Vector3 Radial(int i, int count)
        {
            float angle = (i + R(-.45f, .45f)) * Mathf.PI * 2f / count;
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }

        private float R(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());

        private static AudioClip MakeBoom()
        {
            const int rate = 22050;
            var data = new float[(int)(rate * .85f)];
            var rng = new System.Random(1907);
            float low = 0f, mid = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)rate;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                low = Mathf.Lerp(low, noise, .035f);
                mid = Mathf.Lerp(mid, noise, .19f);
                float body = Mathf.Sin(2f * Mathf.PI * (65f * t - 19f * t * t)) * Mathf.Exp(-t * 8f);
                float attack = Mathf.Clamp01(t / .004f);
                float tail = Mathf.Clamp01((.85f - t) / .09f);
                data[i] = Mathf.Clamp((body * .48f + low * 1.4f * Mathf.Exp(-t * 4.8f)
                    + mid * .7f * Mathf.Exp(-t * 11f) + (noise - mid) * .24f * Mathf.Exp(-t * 45f))
                    * attack * tail, -1f, 1f);
            }
            var clip = AudioClip.Create("Ground crash boom", data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public void Clear()
        {
            if (fire != null) fire.Clear();
            if (smoke != null) smoke.Clear();
            if (dust != null) dust.Clear();
            if (fragments != null) fragments.Clear();
            if (sparks != null) sparks.Clear();
        }

        public void Dispose()
        {
            if (fire != null) Object.Destroy(fire.gameObject);
            if (smoke != null) Object.Destroy(smoke.gameObject);
            if (dust != null) Object.Destroy(dust.gameObject);
            if (fragments != null) Object.Destroy(fragments.gameObject);
            if (sparks != null) Object.Destroy(sparks.gameObject);
            if (boom != null) Object.Destroy(boom);
        }
    }
}
