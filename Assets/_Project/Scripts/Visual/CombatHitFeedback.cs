using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AirCombat
{
    // Production encounter presentation. Observes confirmed HP damage, never
    // modifies damage, hitboxes, weapon cadence or flight. One bounded FX/audio
    // set for the whole encounter; no scene discovery in the hit path.
    [DisallowMultipleComponent]
    public sealed class CombatHitFeedback : MonoBehaviour
    {
        [Header("Hull flash")]
        [SerializeField, Min(.01f)] private float flashSeconds = .09f;
        [Header("Enemy impact puff")]
        [SerializeField, Min(.1f)] private float impactFlashSize = 3.5f;
        [SerializeField, Min(.1f)] private float smokeSize = 3.2f;
        [SerializeField, Min(.1f)] private float smokeLifetime = .72f;
        [Header("Persistent damage trail (JUICE-02a)")]
        [SerializeField, Range(.01f, 1f)] private float criticalHp = .4f;
        [SerializeField, Range(.01f, 1f)] private float severeHp = .2f;
        [SerializeField, Min(.1f)] private float trailSmokeSpacing = .78f;
        [SerializeField, Min(.1f)] private float trailCloudSpacing = 1.5f;
        [SerializeField, Min(.1f)] private float trailSmokeSize = 2.6f;
        [SerializeField, Min(.1f)] private float trailSmokeLifetime = .72f;
        [SerializeField, Min(1f)] private float trailFireRate = 22f;
        [SerializeField, Min(.1f)] private float trailFireSize = 1.8f;
        [Header("Camera impulse (world units / seconds)")]
        [SerializeField, Min(0f)] private float hitShake = .28f;
        [SerializeField, Min(0f)] private float hitShakeSeconds = .12f;
        [SerializeField, Min(0f)] private float receivedShake = .65f;
        [SerializeField, Min(0f)] private float receivedShakeSeconds = .19f;
        [SerializeField, Min(0f)] private float killShake = 2.1f;
        [SerializeField, Min(0f)] private float killShakeSeconds = .38f;
        [Header("Ground crash (JUICE-02c)")]
        [SerializeField, Min(.1f)] private float crashScale = 1f;
        [SerializeField, Min(0f)] private float crashShake = 2.5f;
        [SerializeField, Min(0f)] private float crashShakeSeconds = .45f;
        [SerializeField, Min(1f)] private float crashFullDistance = 45f;
        [SerializeField, Min(1f)] private float crashMaxDistance = 240f;
        [SerializeField, Range(0f, 1f)] private float crashVolume = .8f;
        [Header("Sound / incoming damage")]
        [SerializeField, Range(0f, 1f)] private float hitVolume = .35f;
        [SerializeField, Range(0f, 1f)] private float killVolume = .60f;
        [SerializeField, Range(0f, 1f)] private float damageEdgeAlpha = .55f;
        [SerializeField, Min(.01f)] private float damageEdgeSeconds = .22f;

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private sealed class Slot
        {
            public Renderer Renderer;
            public int Index;
            public MaterialPropertyBlock Before = new MaterialPropertyBlock();
            public MaterialPropertyBlock Flash = new MaterialPropertyBlock();
            public bool HasEmission;
        }
        private sealed class Tracked
        {
            public bool IsPlayer;
            public bool Flashing;
            public float FlashUntil;
            public Transform Visual;
            public MeshFilter[] HullMeshes;
            public DamageSurfaceAnchor DamageAnchor;
            public bool AnchorAttempted, TrailActive, HiddenForWreck;
            public Vector3 PreviousAnchor;
            public float SmokeDebt, CloudDebt, FireDebt;
            public readonly List<Slot> Slots = new List<Slot>();
        }
        private readonly Dictionary<PlayerHitPoints, Tracked> targets =
            new Dictionary<PlayerHitPoints, Tracked>();
        private readonly List<AircraftWreck> wrecks = new List<AircraftWreck>();
        // Separate random stream: presentation cannot change enemy spawn/AI RNG.
        private readonly System.Random random = new System.Random(8153);
        private GroundCrashFeedback groundCrash;
        private Transform playerListener;
        private Camera view;
        private ArcadeCameraFollow cameraFollow;
        private ParticleSystem sparks, debris, impactFlash, smoke, criticalSmoke, criticalCloud, criticalFire;
        private ParticleSystem missileSmoke, missileCloud, hostileSmoke, hostileCloud, hostileGlow;
        private Material particleMaterial, puffMaterial, trailMaterial, cloudMaterial;
        private Texture2D puffMask, trailMask, cloudMask;
        private AudioSource audioSource;
        private AudioClip hitClip, killClip;
        private Texture2D edgeTexture;
        private float edgeUntil;
        private float nextHitSound;

        public void Bind(Camera camera)
        {
            view = camera;
            cameraFollow = camera != null ? camera.GetComponent<ArcadeCameraFollow>() : null;
            if (audioSource != null) return;
            // The existing sandbox effect shader reads vertex/particle colors.
            // The optional white-default mask is used only by the impact puffs.
            Shader shader = Shader.Find("AirCombat/SandboxEffect");
            if (shader != null)
            {
                particleMaterial = new Material(shader) { name = "Combat hit particles (runtime)" };
                sparks = MakeParticles("Hit sparks", true, 192);
                debris = MakeParticles("Hit debris", false, 128);
                puffMask = MakePuffMask();
                puffMaterial = new Material(particleMaterial) { name = "Impact puff (runtime)" };
                puffMaterial.SetTexture("_ParticleMask", puffMask);
                impactFlash = MakePuffs("Enemy impact flash", 48, false);
                smoke = MakePuffs("Enemy impact smoke", 192, true);
                criticalSmoke = MakePuffs("Critical aircraft smoke", 768, true);
                criticalCloud = MakePuffs("Critical aircraft smoke turbulence", 384, true);
                criticalFire = MakePuffs("Critical aircraft fire", 256, false);
                // Keep the accepted impact puffs unchanged; only persistent trails
                // get the softer, higher-resolution alpha mask.
                trailMask = MakePuffMask(true);
                trailMaterial = new Material(puffMaterial) { name = "Soft damage trail (runtime)" };
                trailMaterial.SetTexture("_ParticleMask", trailMask);
                criticalSmoke.GetComponent<ParticleSystemRenderer>().sharedMaterial = trailMaterial;
                cloudMask = MakePuffMask(true, true);
                cloudMaterial = new Material(trailMaterial) { name = "Damage trail turbulence (runtime)" };
                cloudMaterial.SetTexture("_ParticleMask", cloudMask);
                criticalCloud.GetComponent<ParticleSystemRenderer>().sharedMaterial = cloudMaterial;
                ConfigureDamageTrail();
                missileSmoke = MakePuffs("AAM exhaust core", 768, true);
                missileCloud = MakePuffs("AAM exhaust turbulence", 384, true);
                missileSmoke.GetComponent<ParticleSystemRenderer>().sharedMaterial = trailMaterial;
                missileCloud.GetComponent<ParticleSystemRenderer>().sharedMaterial = cloudMaterial;
                ConfigureSmokeLayer(missileSmoke, false);
                ConfigureSmokeLayer(missileCloud, true);
                // Independent particle budgets keep dense enemy volleys from evicting player exhaust.
                hostileSmoke = MakePuffs("Hostile AAM exhaust core", 1536, true);
                hostileCloud = MakePuffs("Hostile AAM exhaust turbulence", 768, true);
                hostileGlow = MakePuffs("Hostile AAM motor glow", 128, false);
                hostileSmoke.GetComponent<ParticleSystemRenderer>().sharedMaterial = trailMaterial;
                hostileCloud.GetComponent<ParticleSystemRenderer>().sharedMaterial = cloudMaterial;
                ConfigureSmokeLayer(hostileSmoke, false);
                ConfigureSmokeLayer(hostileCloud, true);
            }
            else Debug.LogWarning("Combat hit particles: SandboxEffect shader missing.", this);
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            hitClip = MakeHitClip(false);
            killClip = MakeHitClip(true);
            edgeTexture = MakeEdgeTexture();
            groundCrash = new GroundCrashFeedback(transform, particleMaterial, puffMaterial, cloudMaterial);
        }

        public void Track(PlayerHitPoints health, bool isPlayer)
        {
            if (health == null || targets.ContainsKey(health)) return;
            if (isPlayer) playerListener = health.transform;
            var tracked = new Tracked { IsPlayer = isPlayer };
            var jet = health.GetComponent<JetAssembly>();
            if (jet != null && jet.VisualRoot != null)
            {
                tracked.Visual = jet.VisualRoot;
                // Capture only the active authored hull at registration, not runtime
                // flashes or exhaust which might be visible at the critical hit.
                var filters = new List<MeshFilter>();
                foreach (var filter in tracked.Visual.GetComponentsInChildren<MeshFilter>(false))
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (renderer != null && renderer.enabled && filter.sharedMesh != null)
                        filters.Add(filter);
                }
                tracked.HullMeshes = filters.ToArray();
                foreach (var renderer in jet.VisualRoot.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                        !(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        var material = materials[i];
                        if (material == null || !material.HasProperty(BaseColor)) continue;
                        tracked.Slots.Add(new Slot { Renderer = renderer, Index = i,
                            HasEmission = material.HasProperty(EmissionColor) });
                    }
                }
            }
            targets.Add(health, tracked);
            health.Damaged += OnDamage;
        }

        public void Untrack(PlayerHitPoints health)
        {
            if (health == null || !targets.TryGetValue(health, out var tracked)) return;
            health.Damaged -= OnDamage;
            Restore(tracked);
            targets.Remove(health);
        }

        // Called once by the encounter before removing a killed aircraft. The
        // damage source is preserved in model-local coordinates on the wreck.
        public void SpawnWreck(PlayerHitPoints health, float speed)
        {
            if (health == null || health.IsAlive || !targets.TryGetValue(health, out var tracked)) return;
            Restore(tracked);
            if (!tracked.DamageAnchor.IsValid && !tracked.AnchorAttempted)
                tracked.DamageAnchor = DamageSurfaceAnchor.Choose(tracked.Visual, tracked.HullMeshes, random);
            var wreck = AircraftWreck.Create(health.transform, tracked.HullMeshes,
                speed, tracked.DamageAnchor, random);
            if (wreck == null) return;
            wrecks.Add(wreck);
            if (tracked.IsPlayer && tracked.Visual != null)
            {
                tracked.HiddenForWreck = true;
                tracked.Visual.gameObject.SetActive(false);
            }
        }

        private void OnDamage(PlayerHitPoints health, Vector3 point)
        {
            if (!isActiveAndEnabled || !targets.TryGetValue(health, out var tracked)) return;
            bool killed = !health.IsAlive;
            Flash(tracked);
            EmitBurst(point, health.transform.forward, killed, !tracked.IsPlayer);
            if (!tracked.IsPlayer) EmitPuffs(point, killed);
            if (cameraFollow != null)
            {
                if (killed) cameraFollow.AddHitShake(killShake, killShakeSeconds);
                else if (tracked.IsPlayer) cameraFollow.AddHitShake(receivedShake, receivedShakeSeconds);
                else cameraFollow.AddHitShake(hitShake, hitShakeSeconds);
            }
            if (tracked.IsPlayer) edgeUntil = Time.time + damageEdgeSeconds;
            if (audioSource == null) return;
            if (killed)
            {
                audioSource.PlayOneShot(killClip, killVolume);
                nextHitSound = Time.time + .08f;
            }
            else if (Time.time >= nextHitSound)
            {
                audioSource.PlayOneShot(hitClip, hitVolume * (tracked.IsPlayer ? 1f : .85f));
                nextHitSound = Time.time + .055f;
            }
        }

        private void Flash(Tracked tracked)
        {
            tracked.FlashUntil = Time.time + flashSeconds;
            if (tracked.Flashing) return;
            tracked.Flashing = true;
            foreach (var slot in tracked.Slots)
            {
                if (slot.Renderer == null) continue;
                slot.Renderer.GetPropertyBlock(slot.Before, slot.Index);
                slot.Renderer.GetPropertyBlock(slot.Flash, slot.Index);
                // Preserve any renderer-level overrides if no per-slot block exists.
                if (slot.Flash.isEmpty) slot.Renderer.GetPropertyBlock(slot.Flash);
                slot.Flash.SetColor(BaseColor, new Color(1f, .94f, .70f, 1f));
                if (slot.HasEmission) slot.Flash.SetColor(EmissionColor, new Color(1.8f, 1.2f, .48f, 1f));
                slot.Renderer.SetPropertyBlock(slot.Flash, slot.Index);
            }
        }

        private static void Restore(Tracked tracked)
        {
            if (!tracked.Flashing) return;
            foreach (var slot in tracked.Slots)
                if (slot.Renderer != null)
                    slot.Renderer.SetPropertyBlock(slot.Before.isEmpty ? null : slot.Before, slot.Index);
            tracked.Flashing = false;
        }

        private void LateUpdate()
        {
            foreach (var pair in targets)
            {
                var tracked = pair.Value;
                if (tracked.Flashing && Time.time >= tracked.FlashUntil) Restore(tracked);
                TickDamageTrail(pair.Key, tracked, Time.deltaTime);
            }
            TickWrecks(Time.deltaTime);
        }

        private void TickWrecks(float dt)
        {
            for (int i = wrecks.Count - 1; i >= 0; i--)
            {
                var wreck = wrecks[i];
                bool airborne = wreck.Step(dt);
                if (airborne && wreck.HasAnchor && dt > 0f)
                {
                    Vector3 point = wreck.AnchorPosition;
                    EmitSmokeLayer(criticalSmoke, wreck.PreviousAnchor, point,
                        trailSmokeSpacing, ref wreck.SmokeDebt, false, true, 1.6f);
                    EmitSmokeLayer(criticalCloud, wreck.PreviousAnchor, point,
                        trailCloudSpacing, ref wreck.CloudDebt, true, true, 1.6f);
                    EmitFireTrail(wreck.PreviousAnchor, point, dt, ref wreck.FireDebt, 1.8f);
                    wreck.PreviousAnchor = point;
                }
                if (airborne) continue;
                // Only an actual ground contact triggers the explosion, never a
                // safety timeout or the reset path. Pure presentation, no damage.
                if (wreck.HitGround) EmitGroundCrash(wreck.ImpactPoint);
                wreck.Dispose();
                wrecks.RemoveAt(i);
            }
        }

        private void EmitGroundCrash(Vector3 point)
        {
            if (groundCrash == null) return;
            // Horizontal distance matches the arcade map and avoids penalizing
            // the fixed flight altitude when a wreck hits directly underneath.
            Vector3 delta = playerListener != null ? playerListener.position - point
                : view != null ? view.transform.position - point : Vector3.zero;
            delta.y = 0f;
            float near = crashFullDistance;
            float far = Mathf.Max(near + 1f, crashMaxDistance);
            float strength = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(near, far, delta.magnitude));
            groundCrash.Emit(point, audioSource, crashVolume * strength, crashScale);
            if (cameraFollow != null) cameraFollow.AddHitShake(crashShake * strength, crashShakeSeconds);
        }

        private void ClearWrecks()
        {
            foreach (var wreck in wrecks) wreck.Dispose();
            wrecks.Clear();
        }

        public void ResetEffects()
        {
            foreach (var tracked in targets.Values)
            {
                Restore(tracked);
                ResetTrail(tracked);
                if (tracked.HiddenForWreck && tracked.Visual != null)
                    tracked.Visual.gameObject.SetActive(true);
                tracked.HiddenForWreck = false;
            }
            ClearWrecks();
            groundCrash?.Clear();
            if (missileSmoke != null) missileSmoke.Clear();
            if (missileCloud != null) missileCloud.Clear();
            if (hostileSmoke != null) hostileSmoke.Clear();
            if (hostileCloud != null) hostileCloud.Clear();
            if (hostileGlow != null) hostileGlow.Clear();
            if (criticalSmoke != null) criticalSmoke.Clear();
            if (criticalCloud != null) criticalCloud.Clear();
            if (criticalFire != null) criticalFire.Clear();
            if (sparks != null) sparks.Clear();
            if (debris != null) debris.Clear();
            if (impactFlash != null) impactFlash.Clear();
            if (smoke != null) smoke.Clear();
            if (audioSource != null) audioSource.Stop();
            if (cameraFollow != null) cameraFollow.ClearHitShake();
            edgeUntil = nextHitSound = 0f;
        }

        private static void ResetTrail(Tracked tracked)
        {
            tracked.DamageAnchor = default;
            tracked.AnchorAttempted = tracked.TrailActive = false;
            tracked.SmokeDebt = tracked.CloudDebt = tracked.FireDebt = 0f;
        }

        private void ConfigureDamageTrail()
        {
            ConfigureSmokeLayer(criticalSmoke, false);
            ConfigureSmokeLayer(criticalCloud, true);
            var fireFade = criticalFire.colorOverLifetime;
            var fireGradient = new Gradient();
            fireGradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(1f, .34f, .08f), .55f),
                new GradientColorKey(new Color(.75f, .14f, .03f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(.85f, .35f), new GradientAlphaKey(0f, 1f) });
            fireFade.color = new ParticleSystem.MinMaxGradient(fireGradient);
        }

        private static void ConfigureSmokeLayer(ParticleSystem ps, bool cloud)
        {
            var fade = ps.colorOverLifetime;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                cloud ? new[] { new GradientAlphaKey(.12f, 0f), new GradientAlphaKey(1f, .25f),
                    new GradientAlphaKey(.55f, .62f), new GradientAlphaKey(0f, 1f) }
                : new[] { new GradientAlphaKey(.8f, 0f), new GradientAlphaKey(1f, .12f),
                    new GradientAlphaKey(.6f, .5f), new GradientAlphaKey(0f, 1f) });
            fade.color = new ParticleSystem.MinMaxGradient(gradient);
            var size = ps.sizeOverLifetime;
            size.size = new ParticleSystem.MinMaxCurve(1f, cloud
                ? AnimationCurve.Linear(0f, .72f, 1f, 1.6f)
                : AnimationCurve.Linear(0f, .88f, 1f, 1.35f));

            // World-space particles curl independently; no transform-driven trail.
            var noise = ps.noise;
            noise.enabled = true;
            noise.separateAxes = false;
            noise.strength = cloud ? 1.3f : .38f;
            noise.frequency = cloud ? .65f : .42f;
            noise.scrollSpeed = .65f;
            noise.damping = true;
            noise.quality = ParticleSystemNoiseQuality.Medium;
            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.separateAxes = false;
            rotation.z = new ParticleSystem.MinMaxCurve(cloud ? -1.1f : -.6f, cloud ? 1.1f : .6f);
        }

        private void TickDamageTrail(PlayerHitPoints health, Tracked tracked, float dt)
        {
            if (criticalSmoke == null || health == null || !health.isActiveAndEnabled || !health.IsAlive)
            {
                tracked.TrailActive = false;
                return;
            }
            float hp = health.Hp / (float)Mathf.Max(1, health.MaxHp);
            if (hp > criticalHp) { ResetTrail(tracked); return; }
            if (!tracked.AnchorAttempted)
            {
                tracked.AnchorAttempted = true;
                tracked.DamageAnchor = DamageSurfaceAnchor.Choose(tracked.Visual, tracked.HullMeshes, random);
                if (!tracked.DamageAnchor.IsValid)
                    Debug.LogWarning("Critical smoke needs readable hull geometry or authored DamageSockets.", health);
            }
            // Never emit from a fabricated point when no valid surface is available.
            if (!tracked.DamageAnchor.IsValid) return;
            Vector3 point = tracked.DamageAnchor.WorldPosition;
            bool severe = hp <= Mathf.Min(criticalHp, severeHp);
            if (!tracked.TrailActive || (point - tracked.PreviousAnchor).sqrMagnitude > 30f * 30f)
            {
                tracked.PreviousAnchor = point;
                tracked.SmokeDebt = tracked.CloudDebt = 1f; // Seed at the real surface.
                tracked.FireDebt = severe ? 1f : 0f;
            }
            tracked.TrailActive = true;
            // Constant spatial density at cruise, boost and tight turn. Each
            // layer carries its own fractional spacing across frames.
            if (dt > 0f)
            {
                EmitSmokeLayer(criticalSmoke, tracked.PreviousAnchor, point,
                    trailSmokeSpacing, ref tracked.SmokeDebt, false, severe);
                EmitSmokeLayer(criticalCloud, tracked.PreviousAnchor, point,
                    trailCloudSpacing, ref tracked.CloudDebt, true, severe);
            }
            if (severe)
                EmitFireTrail(tracked.PreviousAnchor, point, dt, ref tracked.FireDebt);
            else tracked.FireDebt = 0f;
            tracked.PreviousAnchor = point;
        }

        // Same soft layered smoke as the accepted wreck trail, with a narrower,
        // lighter exhaust. Each missile owns its own distance accumulators.
        public void EmitMissileExhaust(Vector3 previous, Vector3 current,
            ref float coreDebt, ref float cloudDebt, bool hostile = false)
        {
            if (!isActiveAndEnabled) return;
            if (hostile)
            {
                EmitSmokeLayer(hostileSmoke, previous, current, .55f, ref coreDebt, false, false, 1f, true, true);
                EmitSmokeLayer(hostileCloud, previous, current, 1.05f, ref cloudDebt, true, false, 1f, true, true);
                if (hostileGlow != null)
                    hostileGlow.Emit(new ParticleSystem.EmitParams {
                        position = current, velocity = Vector3.zero, startSize = 2.4f,
                        startLifetime = .045f, startColor = new Color(1f, .24f, .035f, 1f)
                    }, 1);
                return;
            }
            EmitSmokeLayer(missileSmoke, previous, current, .55f, ref coreDebt, false, false, .72f, true);
            EmitSmokeLayer(missileCloud, previous, current, 1.05f, ref cloudDebt, true, false, .72f, true);
        }

        private void EmitSmokeLayer(ParticleSystem system, Vector3 previous, Vector3 current,
            float spacing, ref float debt, bool cloud, bool severe, float intensity = 1f, bool missile = false, bool hostile = false)
        {
            if (system == null) return;
            float before = debt;
            float step = Mathf.Max(.15f, spacing) * (severe ? .88f : 1f);
            float amount = Vector3.Distance(previous, current) / step;
            float total = before + amount;
            int count = Mathf.Min(64, Mathf.FloorToInt(total));
            debt = total - Mathf.Floor(total); // Bounded even during a frame hitch.
            for (int i = 0; i < count; i++)
            {
                float t = amount > .0001f ? Mathf.Clamp01((i + 1f - before) / amount) : 1f;
                float tone = cloud ? Range(.20f, .28f) : Range(.13f, .19f);
                float alpha = cloud ? Range(.11f, .20f) : Range(.19f, .29f);
                if (missile)
                {
                    tone = cloud ? Range(.60f, .72f) : Range(.68f, .82f);
                    alpha *= 1.35f;
                    if (hostile)
                    {
                        tone = cloud ? Range(.36f, .48f) : Range(.23f, .34f);
                        alpha *= 1.25f;
                    }
                }
                var p = new ParticleSystem.EmitParams
                {
                    position = Vector3.Lerp(previous, current, t),
                    // No offset at birth. Velocity/noise open the cloud after emission.
                    velocity = cloud
                        ? new Vector3(Range(-2.1f, 2.1f), Range(1.3f, 3.4f), Range(-2.1f, 2.1f))
                        : new Vector3(Range(-.45f, .45f), Range(.5f, 1.4f), Range(-.45f, .45f)),
                    startSize = trailSmokeSize * (cloud ? Range(1.15f, 1.8f) : Range(.9f, 1.25f))
                        * (severe ? 1.10f : 1f) * Mathf.Sqrt(intensity) * (missile ? .75f : 1f),
                    startLifetime = trailSmokeLifetime * Range(.85f, 1.12f) * (missile ? .8f : 1f),
                    startColor = new Color(tone, tone * .96f, tone * .91f,
                        Mathf.Clamp01(alpha * (severe ? 1.2f : 1f) * intensity)),
                    rotation = Range(0f, 360f)
                };
                system.Emit(p, 1);
            }
        }

        private void EmitFireTrail(Vector3 previous, Vector3 current, float dt, ref float debt, float intensity = 1f)
        {
            if (criticalFire == null || dt <= 0f) return;
            float before = debt;
            float amount = Mathf.Max(0f, dt * trailFireRate * intensity);
            float total = before + amount;
            int count = Mathf.Min(12, Mathf.FloorToInt(total));
            debt = total - Mathf.Floor(total);
            for (int i = 0; i < count; i++)
            {
                float t = amount > 0f ? Mathf.Clamp01((i + 1f - before) / amount) : 1f;
                var p = new ParticleSystem.EmitParams
                {
                    position = Vector3.Lerp(previous, current, t),
                    velocity = Vector3.up * Range(.3f, 1.2f),
                    startSize = trailFireSize * Range(.75f, 1.2f) * Mathf.Sqrt(intensity),
                    startLifetime = Range(.10f, .19f),
                    startColor = new Color(1f, .85f, .31f, .85f),
                    rotation = Range(0f, 360f)
                };
                criticalFire.Emit(p, 1);
            }
        }

        private ParticleSystem MakeParticles(string objectName, bool stretch, int capacity)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = capacity;
            main.gravityModifier = stretch ? 0f : 1.3f;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(.9f, .25f), new GradientAlphaKey(0f, 1f) });
            fade.color = new ParticleSystem.MinMaxGradient(gradient);
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = particleMaterial;
            renderer.renderMode = stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            renderer.lengthScale = stretch ? 2f : 1f;
            renderer.velocityScale = stretch ? .07f : 0f;
            renderer.cameraVelocityScale = 0f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            ps.Play();
            return ps;
        }

        private ParticleSystem MakePuffs(string objectName, int capacity, bool isSmoke)
        {
            var ps = MakeParticles(objectName, false, capacity);
            var main = ps.main;
            main.gravityModifier = 0f;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = puffMaterial;
            renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
            {
                ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color,
                ParticleSystemVertexStream.UV
            });
            var fade = ps.colorOverLifetime;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                isSmoke ? new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .12f),
                    new GradientAlphaKey(.55f, .60f), new GradientAlphaKey(0f, 1f) }
                : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(.8f, .18f),
                    new GradientAlphaKey(0f, 1f) });
            fade.color = new ParticleSystem.MinMaxGradient(gradient);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, isSmoke
                ? AnimationCurve.Linear(0f, .65f, 1f, 1.8f)
                : AnimationCurve.Linear(0f, 1f, 1f, 1.25f));
            return ps;
        }

        // Soft irregular silhouette, rather than an opaque billboard square.
        // Generated once per encounter; the persistent smoke gets a separate mask.
        private static Texture2D MakePuffMask(bool softTrail = false, bool cloud = false)
        {
            int size = softTrail ? 128 : 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = cloud ? "Turbulent trail mask" : softTrail ? "Soft trail mask" : "Impact puff mask", wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2((x + .5f) / size * 2f - 1f,
                    (y + .5f) / size * 2f - 1f);
                float radius = p.magnitude;
                float noise = Mathf.PerlinNoise(p.x * 2.8f + 11f, p.y * 2.8f + 19f);
                if (cloud)
                {
                    noise = .7f * Mathf.PerlinNoise(p.x * 3.5f + 31f, p.y * 3.5f + 7f)
                        + .3f * Mathf.PerlinNoise(p.x * 7f + 5f, p.y * 7f + 41f);
                }
                float edge = softTrail
                    ? Mathf.Clamp01((.88f + (noise - .5f) * (cloud ? .4f : .18f) - radius) / (cloud ? .68f : .64f))
                    : Mathf.Clamp01((.82f + (noise - .5f) * .32f - radius) / .34f);
                float alpha = Mathf.SmoothStep(0f, 1f, edge) * Mathf.Lerp(cloud ? .48f : softTrail ? .88f : .72f, 1f, noise);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private void EmitPuffs(Vector3 point, bool killed)
        {
            // World-space positions: no aircraft attachment or inherited speed.
            // A hot pop first, then a dark spreading cloud left in the sky.
            if (impactFlash != null)
            {
                var flash = new ParticleSystem.EmitParams
                {
                    position = point, velocity = Vector3.zero,
                    startSize = impactFlashSize * (killed ? 1.7f : 1f),
                    startLifetime = killed ? .14f : .085f,
                    startColor = new Color(1f, .87f, .48f, .95f),
                    rotation = Range(0f, 360f)
                };
                impactFlash.Emit(flash, 1);
            }
            if (smoke == null) return;
            Vector3 right = view != null ? view.transform.right : Vector3.right;
            Vector3 up = view != null ? view.transform.up : Vector3.up;
            int count = killed ? 5 : 3;
            for (int i = 0; i < count; i++)
            {
                float angle = (i + Range(-.3f, .3f)) * Mathf.PI * 2f / count;
                Vector3 drift = Mathf.Cos(angle) * right + Mathf.Sin(angle) * up;
                var puff = new ParticleSystem.EmitParams
                {
                    position = point + drift * Range(.15f, .75f),
                    velocity = drift * Range(.35f, 1.6f) + Vector3.up * .3f,
                    startSize = smokeSize * Range(.75f, 1.2f) * (killed ? 1.4f : 1f),
                    startLifetime = smokeLifetime * Range(.8f, 1.2f),
                    startColor = new Color(.12f, .11f, .10f, Range(.58f, .78f)),
                    rotation = Range(0f, 360f)
                };
                smoke.Emit(puff, 1);
            }
        }

        private float Range(float min, float max) => min + (max - min) * (float)random.NextDouble();

        private void EmitBurst(Vector3 point, Vector3 forward, bool killed, bool enemyHit)
        {
            Vector3 right = view != null ? view.transform.right : Vector3.right;
            Vector3 up = view != null ? view.transform.up : Vector3.up;
            // Existing AC-01/02 impact streaks remain; add fast bright fragments
            // and slower dark debris at the exact accepted damage point.
            int count = killed ? 16 : enemyHit ? 11 : 7;
            for (int i = 0; i < count; i++)
            {
                float angle = (i + Range(-.25f, .25f)) * Mathf.PI * 2f / count;
                Vector3 direction = Mathf.Cos(angle) * right + Mathf.Sin(angle) * up;
                if (sparks != null)
                {
                    var p = new ParticleSystem.EmitParams
                    {
                        position = point + direction * .25f,
                        velocity = direction * Range(12f, killed ? 42f : enemyHit ? 32f : 27f) + forward * 5f,
                        startSize = Range(enemyHit ? .35f : .22f, killed ? .85f : enemyHit ? .75f : .48f),
                        startLifetime = Range(.10f, killed ? .31f : enemyHit ? .24f : .20f),
                        startColor = i % 3 == 0 ? new Color(1f, 1f, .82f) : new Color(1f, .67f, .15f)
                    };
                    sparks.Emit(p, 1);
                }
                if (debris != null && i < (killed ? 10 : 4))
                {
                    var p = new ParticleSystem.EmitParams
                    {
                        position = point,
                        velocity = direction * Range(6f, 17f) + forward * 12f,
                        startSize = Range(.22f, killed ? .80f : .50f),
                        startLifetime = Range(.25f, killed ? .65f : .40f),
                        startColor = new Color(.19f, .16f, .13f),
                        rotation = Range(0f, 360f)
                    };
                    debris.Emit(p, 1);
                }
            }
        }

        private static AudioClip MakeHitClip(bool killed)
        {
            const int rate = 22050;
            var data = new float[Mathf.RoundToInt(rate * (killed ? .32f : .15f))];
            var rng = new System.Random(killed ? 157 : 83);
            float low = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)rate;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                low = Mathf.Lerp(low, noise, killed ? .08f : .28f);
                float attack = Mathf.Clamp01(t / .003f);
                float body = Mathf.Sin(2f * Mathf.PI * (killed ? 85f : 230f) * t) * Mathf.Exp(-t * (killed ? 14f : 37f));
                float crack = (noise - low) * Mathf.Exp(-t * 65f);
                float tail = low * Mathf.Exp(-t * (killed ? 11f : 30f));
                float fade = Mathf.Clamp01((data.Length - 1 - i) / (rate * .02f));
                data[i] = Mathf.Clamp((body * .38f + crack * .45f + tail * .65f) * attack * fade, -1f, 1f);
            }
            var clip = AudioClip.Create(killed ? "Aircraft fatal impact" : "Aircraft hit", data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static Texture2D MakeEdgeTexture()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Damage edge", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float edge = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));
                float a = 1f - Mathf.Clamp01(edge / 9f);
                pixels[y * size + x] = new Color32(255, 34, 16, (byte)Mathf.RoundToInt(255f * a * a));
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || edgeTexture == null || view == null || Time.time >= edgeUntil) return;
            float fade = Mathf.Clamp01((edgeUntil - Time.time) / damageEdgeSeconds);
            var previous = GUI.color;
            int previousDepth = GUI.depth;
            GUI.depth = 10;
            GUI.color = new Color(1f, 1f, 1f, damageEdgeAlpha * fade * fade);
            Rect pixels = view.pixelRect;
            GUI.DrawTexture(new Rect(pixels.x, Screen.height - pixels.yMax, pixels.width, pixels.height), edgeTexture);
            GUI.color = previous;
            GUI.depth = previousDepth;
        }

        private void OnDisable() { ResetEffects(); }

        private void OnDestroy()
        {
            ClearWrecks();
            groundCrash?.Dispose();
            groundCrash = null;
            foreach (var pair in targets)
            {
                if (pair.Key != null) pair.Key.Damaged -= OnDamage;
                Restore(pair.Value);
            }
            targets.Clear();
            if (sparks != null) Destroy(sparks.gameObject);
            if (debris != null) Destroy(debris.gameObject);
            if (audioSource != null) Destroy(audioSource);
            if (impactFlash != null) Destroy(impactFlash.gameObject);
            if (smoke != null) Destroy(smoke.gameObject);
            if (missileSmoke != null) Destroy(missileSmoke.gameObject);
            if (missileCloud != null) Destroy(missileCloud.gameObject);
            if (hostileSmoke != null) Destroy(hostileSmoke.gameObject);
            if (hostileCloud != null) Destroy(hostileCloud.gameObject);
            if (hostileGlow != null) Destroy(hostileGlow.gameObject);
            if (criticalSmoke != null) Destroy(criticalSmoke.gameObject);
            if (criticalCloud != null) Destroy(criticalCloud.gameObject);
            if (criticalFire != null) Destroy(criticalFire.gameObject);
            if (puffMaterial != null) Destroy(puffMaterial);
            if (puffMask != null) Destroy(puffMask);
            if (trailMaterial != null) Destroy(trailMaterial);
            if (trailMask != null) Destroy(trailMask);
            if (cloudMaterial != null) Destroy(cloudMaterial);
            if (cloudMask != null) Destroy(cloudMask);
            if (particleMaterial != null) Destroy(particleMaterial);
            if (hitClip != null) Destroy(hitClip);
            if (killClip != null) Destroy(killClip);
            if (edgeTexture != null) Destroy(edgeTexture);
        }
    }
}
