using UnityEngine;
using UnityEngine.Rendering;

namespace AirCombat
{
    // Runs immediately after the launch sequence (-40), including its accepted retry request.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-30)]
    public sealed class Mission01ReconEncounter : MonoBehaviour
    {
        [SerializeField] private Mission01LaunchSequence launch;
        [SerializeField] private FlightController player;
        [SerializeField] private WatcherTarget[] targets;
        [SerializeField] private float groundY;
        private AircraftRadar radar;
        private CombatHitFeedback effects;
        private ParticleSystem smoke, fire;
        private Material particleMaterial;
        private Texture2D mask;
        private bool ready;
        public bool InFlight => ready && launch != null && launch.isActiveAndEnabled && launch.State == Mission01LaunchSequence.Phase.Flight;
        public int Eliminated
        {
            get
            {
                int n = 0;
                if (ready) foreach (var target in targets) if (target != null && !target.IsAlive) n++;
                return n;
            }
        }
#if UNITY_EDITOR
        public void ConfigureForAuthoring(Mission01LaunchSequence sequence, FlightController flight, WatcherTarget[] units, float floor)
        { launch = sequence; player = flight; targets = units; groundY = floor; }
#endif
        private void Start()
        {
            if (launch == null || player == null || targets == null || targets.Length != 3)
            { Debug.LogError("RE01: missing mission references.", this); enabled = false; return; }
            radar = player.GetComponent<AircraftRadar>();
            foreach (var target in targets)
            {
                if (target == null || !target.Initialize(this, player.CruiseSpeed, groundY))
                { Debug.LogError("RE01: invalid target authoring.", this); enabled = false; return; }
                radar?.Register(target);
            }
            var fx = new GameObject("RE01 / combat FX");
            fx.transform.SetParent(transform, false);
            effects = fx.AddComponent<CombatHitFeedback>();
            var rig = FindFirstObjectByType<ArcadeCameraFollow>();
            effects.Bind(rig != null ? rig.GetComponent<Camera>() : null);
            CreateTrails();
            ready = true;
        }

        private void Update()
        {
            if (!ready || MissionPause.IsTacticalMapPausedFor(gameObject.scene)) return;
            if (MissionRetry.WasRequestedThisFrame(gameObject.scene))
            {
                // The mission has already reset the player and SAMs this frame.
                foreach (var round in FindObjectsByType<BallisticProjectile>(FindObjectsSortMode.None))
                    if (round.gameObject.scene == gameObject.scene && round.IsOwnedBy(player.transform))
                    { round.enabled = false; Destroy(round.gameObject); }
                effects?.ResetEffects(); smoke?.Clear(); fire?.Clear();
                foreach (var target in targets) target.ResetTarget();
                return;
            }
            if (!InFlight) return;
            foreach (var target in targets) target.Tick(Time.deltaTime);
        }

        public void AirBurst(Vector3 p, Vector3 forward) => effects?.EmitInterceptionBurst(p, forward);
        public void GroundBurst(Vector3 p, Vector3 forward)
        {
            if (player != null && (player.transform.position - p).sqrMagnitude < 350f * 350f)
                effects?.EmitTerrainCrash(p);
            else effects?.EmitInterceptionBurst(p, forward);
        }

        private void CreateTrails()
        {
            var shader = Shader.Find("AirCombat/SandboxEffect");
            if (shader == null) { Debug.LogWarning("RE01: smoke shader missing.", this); return; }
            mask = new Texture2D(64, 64, TextureFormat.RGBA32, false, true) { name = "RE01 smoke mask", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[64 * 64];
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                float r = new Vector2((x + .5f) / 32f - 1f, (y + .5f) / 32f - 1f).magnitude;
                float a = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(.12f, 1f, r));
                pixels[y * 64 + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * a));
            }
            mask.SetPixels32(pixels); mask.Apply(false, true);
            particleMaterial = new Material(shader) { name = "RE01 smoke (runtime)" };
            particleMaterial.SetTexture("_ParticleMask", mask);
            smoke = MakeTrail("Watcher smoke", 384, false);
            fire = MakeTrail("Watcher fire", 128, true);
        }

        private ParticleSystem MakeTrail(string name, int budget, bool hot)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false);
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

        private void OnGUI()
        {
            if (!InFlight || MissionPause.IsTacticalMapPausedFor(gameObject.scene) || Event.current.type != EventType.Repaint) return;
            int count = Eliminated;
            var style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * .022f, 15, 25)), fontStyle = FontStyle.Bold };
            string label = count == targets.Length ? "RECON COMPLETE  /  3 OF 3" : $"RECON  /  {count} OF {targets.Length}";
            style.normal.textColor = new Color(.05f, .07f, .08f, .9f); GUI.Label(new Rect(21, 21, 440, 40), label, style);
            style.normal.textColor = count == targets.Length ? new Color(.58f, .94f, .76f) : Color.white; GUI.Label(new Rect(20, 20, 440, 40), label, style);
        }

        private void OnDestroy()
        {
            if (radar != null && targets != null) foreach (var t in targets) if (t != null) radar.Unregister(t);
            if (particleMaterial != null) Destroy(particleMaterial);
            if (mask != null) Destroy(mask);
        }
    }
}
