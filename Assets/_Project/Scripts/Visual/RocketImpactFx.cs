using UnityEngine;
using UnityEngine.Rendering;

namespace AirCombat
{
    // Presentation only. Damage and collision remain owned by the projectile.
    // A shared material/mask belongs to the caller; the entire burst can be
    // removed immediately by the encounter's existing reset path.
    public static class RocketImpactFx
    {
        public static Material CreateMaterial(Shader shader, out Texture2D mask)
        {
            const int size = 32;
            mask = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "Rocket impact soft mask", wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float r = new Vector2((x + .5f) / size * 2f - 1f, (y + .5f) / size * 2f - 1f).magnitude;
                float a = 1f - Mathf.SmoothStep(.0f, 1f, Mathf.InverseLerp(.15f, .98f, r));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
            mask.SetPixels32(pixels);
            mask.Apply(false, true);
            var material = new Material(shader) { name = "Rocket impact particles" };
            material.SetTexture("_ParticleMask", mask);
            return material;
        }

        public static GameObject Spawn(Vector3 point, Material material, float radius)
        {
            var root = new GameObject("Rocket impact / fire, sparks, dust");
            root.transform.position = point + Vector3.up * .3f;
            float scale = Mathf.Clamp(radius / 8f, .35f, 2.5f);
            var rng = new System.Random(System.Guid.NewGuid().GetHashCode());

            var flash = MakeLayer(root.transform, "Flash", material, 2, 3,
                new Color(1f, .72f, .26f), .75f, 1.25f, .35f);
            Emit(flash, root.transform.position + Vector3.up * scale, Vector3.up * 2f * scale,
                7.5f * scale, .13f, new Color(1f, 1f, .84f, 1f));

            var fire = MakeLayer(root.transform, "Fireball", material, 12, 2,
                new Color(.75f, .12f, .025f), .65f, 1.4f, .75f);
            for (int i = 0; i < 8; i++)
            {
                Vector3 d = Direction(rng);
                Emit(fire, root.transform.position + Vector3.up * scale,
                    (d * Next(rng, 3f, 8f) + Vector3.up * Next(rng, 4f, 10f)) * scale,
                    Next(rng, 3.2f, 5.3f) * scale, Next(rng, .25f, .48f),
                    new Color(1f, Next(rng, .57f, .9f), .24f, .93f));
            }

            var sparks = MakeLayer(root.transform, "Sparks", material, 20, 4,
                new Color(1f, .35f, .05f), 1f, .18f, .2f);
            for (int i = 0; i < 14; i++)
            {
                Vector3 d = Direction(rng);
                Emit(sparks, root.transform.position + Vector3.up * scale,
                    (d * Next(rng, 9f, 22f) + Vector3.up * Next(rng, 3f, 12f)) * scale,
                    Next(rng, .32f, .72f) * scale, Next(rng, .22f, .48f),
                    new Color(1f, .91f, .48f, 1f));
            }

            var dust = MakeLayer(root.transform, "Ground dust", material, 16, 0,
                Color.white, .55f, 1.8f, .85f);
            for (int i = 0; i < 10; i++)
            {
                Vector3 d = Direction(rng);
                Emit(dust, root.transform.position + d * scale,
                    (d * Next(rng, 4f, 8f) + Vector3.up * Next(rng, 1f, 2.5f)) * scale,
                    Next(rng, 3f, 5f) * scale, Next(rng, .65f, 1.1f),
                    new Color(.66f, .53f, .37f, .55f));
            }

            var smoke = MakeLayer(root.transform, "Smoke", material, 8, 1,
                Color.white, .55f, 1.9f, .9f);
            for (int i = 0; i < 5; i++)
            {
                Vector3 d = Direction(rng);
                Emit(smoke, root.transform.position + Vector3.up * (1.8f * scale),
                    (d * Next(rng, 1f, 3f) + Vector3.up * Next(rng, 3f, 6f)) * scale,
                    Next(rng, 2.8f, 4.5f) * scale, Next(rng, .8f, 1.4f),
                    new Color(.23f, .22f, .20f, .62f));
            }
            Object.Destroy(root, 1.65f);
            return root;
        }

        private static ParticleSystem MakeLayer(Transform parent, string label, Material material,
            int budget, int order, Color endTint, float startSize, float endSize, float fadeAt)
        {
            var child = new GameObject(label);
            child.transform.SetParent(parent, false);
            var ps = child.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true; // Manual emission only; root owns the bounded lifetime.
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.maxParticles = budget;
            main.startSpeed = 0f;
            var emission = ps.emission; emission.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var size = ps.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, startSize, 1f, endSize));
            var fade = ps.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(endTint, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(.6f, fadeAt), new GradientAlphaKey(0f, 1f) });
            fade.color = new ParticleSystem.MinMaxGradient(gradient);
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = order;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            ps.Play();
            return ps;
        }

        private static void Emit(ParticleSystem ps, Vector3 point, Vector3 velocity, float size, float lifetime, Color color)
        {
            ps.Emit(new ParticleSystem.EmitParams { position = point, velocity = velocity,
                startSize = size, startLifetime = lifetime, startColor = color }, 1);
        }

        private static float Next(System.Random rng, float min, float max) =>
            Mathf.Lerp(min, max, (float)rng.NextDouble());

        private static Vector3 Direction(System.Random rng)
        {
            float angle = Next(rng, 0f, 2f * Mathf.PI);
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }
    }
}
