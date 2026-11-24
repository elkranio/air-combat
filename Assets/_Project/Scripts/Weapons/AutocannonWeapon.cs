using UnityEngine;

namespace AirCombat
{
    // Independent, scene-neutral cannon mount. Place its prefab below either
    // a player or enemy JetAssembly. No input, targeting or mission ownership.
    [DisallowMultipleComponent]
    public sealed class AutocannonWeapon : MonoBehaviour, ITriggeredWeapon, IWeaponHudStatus
    {
        [SerializeField] private AutocannonPreset preset;
        [SerializeField] private AutocannonEmitter emitter;

        private JetAssembly aircraft;
        private Autocannon cadence;
        private ContinuousCannon continuousCadence;

        public AutocannonPreset Preset => preset;
        public string HudName => (preset != null ? preset.WeaponId : "AUTOCANNON") +
            (continuousCadence != null && continuousCadence.IsOverheated(Time.time) ? " / HOT" : "");
        public float Readiness01(float now) => !isActiveAndEnabled ? 0f :
            continuousCadence != null ? continuousCadence.Readiness01(now) :
            cadence != null ? cadence.Readiness01(now) : 0f;
        public int FiredCount { get; private set; }
        public int HitCount { get; private set; }
        public bool HasPendingShots => cadence != null && cadence.HasPendingShots;

#if UNITY_EDITOR
        public void SetAuthoringPreset(AutocannonPreset variant) { preset = variant; }
#endif

        private void Awake()
        {
            aircraft = GetComponentInParent<JetAssembly>();
            if (preset == null || preset.TracerMaterial == null || aircraft == null ||
                !aircraft.IsConfigured)
            {
                Debug.LogError("AutocannonWeapon requires a preset with tracer and a configured parent JetAssembly.", this);
                enabled = false;
                return;
            }
            if (emitter == null) emitter = GetComponent<AutocannonEmitter>();
            if (emitter == null) emitter = gameObject.AddComponent<AutocannonEmitter>();
            emitter.BindChassis(aircraft.Chassis);
            emitter.BindOwner(aircraft.transform);
            // Visual-only settings live on the variant, not player controls or AI.
            emitter.ConfigureArcadeVisuals(preset.GlowMaterial, preset.ArcadeCoreMaterial,
                preset.ArcadeHeadMaterial, preset.ArcadeCoreWidth, preset.ArcadeGlowWidth,
                preset.ArcadeGlowAlpha, preset.ArcadeOuterGlowWidth,
                preset.ArcadeOuterGlowAlpha, preset.ArcadeHeadSize,
                preset.ArcadeImpactScale, preset.ArcadeFlashSeconds);
            emitter.Configure(preset.TracerMaterial, preset.FallbackShot,
                preset.SoundVariants, preset.SoundVolume, preset.MuzzleVelocity,
                preset.MaxRange, preset.TracerLength, preset.HitDamage);
            if (preset.Continuous)
                continuousCadence = new ContinuousCannon(preset.RoundsPerSecond,
                    preset.ContinuousCapacity, preset.ContinuousRefillSeconds,
                    preset.EmptyLockoutSeconds, FireRound, Time.time);
            else
                cadence = new Autocannon(preset.RoundsPerSecond, preset.BurstSize,
                    preset.BurstCooldown, FireRound);
        }

        // A held LMB and an AI firing request enter here in exactly the same way.
        public void TickTrigger(bool held, float now)
        {
            if (!isActiveAndEnabled) return;
            if (continuousCadence != null) continuousCadence.Tick(held, now);
            else cadence?.Tick(held, now);
        }

        public void CeaseFire(float now)
        {
            cadence?.CancelBurst(now);
            continuousCadence?.Tick(false, now);
        }

        public void ResetContinuous(float now) { continuousCadence?.Reset(now); }

        private void LateUpdate() { emitter?.TickVisuals(); }
        private void OnDisable() { CeaseFire(Time.time); }

        private void FireRound()
        {
            // Always nose-forward: no camera projection or mouse angle.
            emitter.Emit(aircraft.transform.forward, RegisterHit, FiredCount);
            FiredCount++;
        }

        private void RegisterHit() { HitCount++; }
    }
}
