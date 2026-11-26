using UnityEngine;
using UnityEngine.Rendering;

namespace AirCombat
{
    // Presentation only: sample actual movement after locomotion, not its requested speed.
    [DisallowMultipleComponent]
    public sealed class GroundMovementDust : MonoBehaviour
    {
        [SerializeField] private Shader particleShader;
        [SerializeField] private Vector3 leftWheel = new Vector3(-2.2f, .35f, -4.65f);
        [SerializeField] private Vector3 rightWheel = new Vector3(2.2f, .35f, -4.65f);
        [SerializeField, Min(.1f)] private float metersPerPuff = 1.8f;
        [SerializeField, Min(.1f)] private float puffSize = 2.8f;
        [SerializeField, Min(.1f)] private float puffLifetime = 1.05f;
        [SerializeField] private Color dustColor = new Color(.67f, .58f, .46f, .32f);
        [SerializeField, Range(0f, 1f)] private float surfaceDust = 1f;

        private ParticleSystem particles;
        private Material material;
        private Texture2D mask;
        private System.Random random;
        private Vector3 previous;
        private readonly float[] untilPuff = new float[2];

        // Future road/surface detection can supply this without touching locomotion.
        public void SetSurfaceDust(float amount) => surfaceDust = Mathf.Clamp01(amount);

        private void Awake()
        {
            random = new System.Random(System.Guid.NewGuid().GetHashCode());
            if (particleShader == null) return;
            mask = new Texture2D(32, 32, TextureFormat.RGBA32, false, true)
                { name = "Ground dust mask", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[32 * 32];
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
            {
                float r = new Vector2((x + .5f) / 16f - 1f, (y + .5f) / 16f - 1f).magnitude;
                float a = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(.12f, 1f, r));
                pixels[y * 32 + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * a));
            }
            mask.SetPixels32(pixels);
            mask.Apply(false, true);
            material = new Material(particleShader) { name = "Ground dust (runtime)" };
            material.SetTexture("_ParticleMask", mask);

            var go = new GameObject("Wheel dust");
            go.transform.SetParent(transform, false);
            particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.maxParticles = 64;
            main.startSpeed = 0f;
            var emission = particles.emission; emission.enabled = false;
            var shape = particles.shape; shape.enabled = false;
            var size = particles.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, .7f, 1f, 1.65f));
            var fade = particles.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(.65f, 0f), new GradientAlphaKey(1f, .18f), new GradientAlphaKey(0f, 1f) });
            fade.color = new ParticleSystem.MinMaxGradient(gradient);
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void OnEnable() => ResetTrail();

        // Call after a deliberate teleport/retry so old dust never survives the reset.
        public void ResetTrail()
        {
            previous = transform.position;
            if (random != null)
                for (int i = 0; i < 2; i++) untilPuff[i] = NextSpacing();
            if (particles == null) return;
            particles.Clear(true);
            if (isActiveAndEnabled) particles.Play(true);
        }

        private void LateUpdate()
        {
            Vector3 current = transform.position;
            Vector3 delta = current - previous;
            float dt = Time.deltaTime;
            float travel = new Vector2(delta.x, delta.z).magnitude;
            if (travel > 30f) { ResetTrail(); return; }
            if (particles != null && dt > 0f && travel / dt > .2f && surfaceDust > 0f)
            {
                float effectiveTravel = travel * surfaceDust;
                EmitWheel(leftWheel, 0, delta, effectiveTravel);
                EmitWheel(rightWheel, 1, delta, effectiveTravel);
            }
            previous = current;
        }

        private void EmitWheel(Vector3 wheel, int index, Vector3 delta, float travel)
        {
            float at = untilPuff[index];
            // Distance-based spacing, independently randomized for each wheel/vehicle.
            // Bound work after a frame hitch; never emit a catch-up cloud next frame.
            for (int i = 0; i < 8 && at <= travel; i++)
            {
                Vector3 p = transform.TransformPoint(wheel) - delta * (1f - at / travel);
                p += transform.right * NextFloat(-.3f, .3f);
                p.y += NextFloat(0f, .25f);
                Color color = dustColor;
                color.a *= NextFloat(.65f, 1f);
                var puff = new ParticleSystem.EmitParams
                {
                    position = p,
                    velocity = -transform.forward * NextFloat(.7f, 2.3f) + transform.right * NextFloat(-.9f, .9f) + Vector3.up * NextFloat(.45f, 1.2f),
                    startSize = puffSize * NextFloat(.75f, 1.25f),
                    startLifetime = puffLifetime * NextFloat(.75f, 1.25f),
                    startColor = color
                };
                particles.Emit(puff, 1);
                at += NextSpacing();
            }
            untilPuff[index] = at > travel ? at - travel : NextSpacing();
        }

        private float NextFloat(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
        private float NextSpacing() => Mathf.Max(.1f, metersPerPuff) * NextFloat(.65f, 1.35f);

        private void OnDisable()
        {
            if (particles != null) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void OnDestroy()
        {
            if (particles != null) Destroy(particles.gameObject);
            if (material != null) Destroy(material);
            if (mask != null) Destroy(mask);
        }
    }
}
