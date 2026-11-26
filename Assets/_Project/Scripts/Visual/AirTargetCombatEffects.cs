using UnityEngine;
using UnityEngine.Rendering;

namespace AirCombat
{
    // Scene-local, reusable air-target effects. No objective, mission, or input dependency.
    internal sealed class AirTargetCombatEffects : IAirTargetEffects
    {
        private readonly Transform owner, player;
        private readonly GameObject fx;
        private CombatHitFeedback effects;
        private ParticleSystem smoke, fire;
        private Material particleMaterial;
        private Texture2D mask;

        internal AirTargetCombatEffects(Transform owner, Transform player, Camera camera)
        {
            this.owner = owner;
            this.player = player;
            fx = new GameObject("Air target combat FX");
            fx.transform.SetParent(owner, false);
            effects = fx.AddComponent<CombatHitFeedback>();
            effects.Bind(camera);
            CreateTrails();
        }

        public void AirBurst(Vector3 p, Vector3 forward) => effects?.EmitInterceptionBurst(p, forward);
        public void GroundBurst(Vector3 p, Vector3 forward)
        {
            if (player != null && (player.position - p).sqrMagnitude < 350f * 350f)
                effects?.EmitTerrainCrash(p);
            else effects?.EmitInterceptionBurst(p, forward);
        }

        private void CreateTrails()
        {
            var shader = Shader.Find("AirCombat/SandboxEffect");
            if (shader == null) { Debug.LogWarning("Air target FX: smoke shader missing.", owner); return; }
            mask = new Texture2D(64, 64, TextureFormat.RGBA32, false, true) { name = "Air target smoke mask", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[64 * 64];
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                float r = new Vector2((x + .5f) / 32f - 1f, (y + .5f) / 32f - 1f).magnitude;
                float a = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(.12f, 1f, r));
                pixels[y * 64 + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * a));
            }
            mask.SetPixels32(pixels); mask.Apply(false, true);
            particleMaterial = new Material(shader) { name = "Air target smoke (runtime)" };
            particleMaterial.SetTexture("_ParticleMask", mask);
            smoke = MakeTrail("Air target smoke", 384, false);
            fire = MakeTrail("Air target fire", 128, true);
        }

        private ParticleSystem MakeTrail(string name, int budget, bool hot)
        {
            var go = new GameObject(name); go.transform.SetParent(owner, false);
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = true; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = budget; main.startSpeed = 0; main.startLifetime = hot ? .45f : 2.8f;
            var emission = ps.emission; emission.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var size = ps.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, hot ? AnimationCurve.Linear(0f, 1f, 1f, .4f) : AnimationCurve.Linear(0f, .6f, 1f, 2.1f));
            var fade = ps.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(hot ? new Color(.9f, .24f, .04f) : Color.white, 1f) },
                new[] { new GradientAlphaKey(.9f, 0f), new GradientAlphaKey(.85f, .2f), new GradientAlphaKey(0f, 1f) });
            fade.color = new ParticleSystem.MinMaxGradient(gradient);
            var r = ps.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = particleMaterial;
            r.renderMode = ParticleSystemRenderMode.Billboard; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            ps.Play(); return ps;
        }

        public void EmitSmoke(Vector3 p, bool burning)
        {
            if (smoke == null) return;
            var e = new ParticleSystem.EmitParams { position = p, velocity = new Vector3(Random.Range(-1.6f, 1.6f), Random.Range(1f, 3f), Random.Range(-1.6f, 1.6f)),
                startSize = Random.Range(3.0f, 5.0f), startLifetime = Random.Range(2.0f, 3.0f), startColor = new Color(.18f, .18f, .17f, .68f) };
            smoke.Emit(e, 1);
            if (burning && fire != null && Random.value < .7f)
            {
                e.startSize = Random.Range(2.0f, 4.0f); e.startLifetime = Random.Range(.25f, .55f);
                e.startColor = new Color(1f, .65f, .14f, .95f); fire.Emit(e, 1);
            }
        }

        internal void Reset()
        {
            effects?.ResetEffects(); smoke?.Clear(); fire?.Clear();
        }

        internal void Dispose()
        {
            // Explicitly release owned scene children even if only the composition
            // component is removed; ordinary scene unload also destroys them.
            if (fx != null) Object.Destroy(fx);
            if (smoke != null) Object.Destroy(smoke.gameObject);
            if (fire != null) Object.Destroy(fire.gameObject);
            if (particleMaterial != null) Object.Destroy(particleMaterial);
            if (mask != null) Object.Destroy(mask);
            effects = null;
            smoke = fire = null;
            particleMaterial = null;
            mask = null;
        }
    }
}
