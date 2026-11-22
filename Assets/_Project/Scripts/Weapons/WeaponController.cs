using UnityEngine;
using UnityEngine.InputSystem;

namespace AirCombat
{
    // Common player weapon controller: mode routing, bounded aim, shot scheduling,
    // lock gating and scene-independent weapon presentation. Inspector tuning
    // remains here; the WeaponLabCannon subclass preserves existing scene data.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(130)] // sample aim after camera follow and CameraLookAhead
    public class WeaponController : MonoBehaviour
    {
        [SerializeField] private Material tracerMaterial;
        [SerializeField] private AudioClip shotClip; // synthetic fallback if imported MG02 is missing
        [SerializeField] private AudioClip[] soundVariants; // imported variants kept; only MG02 is used
        [SerializeField, Min(1f)] private float roundsPerSecond = 14f;
        [SerializeField, Range(2, 8)] private int burstSize = 4;
        [SerializeField, Min(.05f)] private float burstCooldown = .45f;
        [SerializeField, Min(.1f)] private float gaussCooldown = 1.5f;
        [SerializeField, Min(1f)] private float gaussVelocity = 300f;
        [SerializeField, Min(1f)] private float gaussRange = 250f;
        [SerializeField, Range(10f, 80f)] private float gaussSightLength = 36f;
        [SerializeField, Min(1f)] private float muzzleVelocity = 420f;
        [SerializeField, Min(1f)] private float maxRange = 430f;
        [SerializeField, Min(.1f)] private float tracerLength = 8f;
        [SerializeField, Range(.01f, 1f)] private float soundVolume = .30f;

        private AutocannonEmitter burstEmitter;
        private GaussEmitter gaussEmitter;
        private BoundedAim freeAim;
        private TargetLock missileLock;
        private WeaponSelection selection;
        private uint lastSelectionRevision;
        private Autocannon autocannon;
        private GaussCannon gaussCannon;
        private System.Action registerHitCallback;
        private bool gaussSelected;
        private bool missileSelected;
        private bool bombSelected;

        public int FiredCount { get; private set; }
        public int HitCount { get; private set; }
        public string FireModeLabel => bombSelected ? "BOMB" : missileSelected ? "MISSILE" : gaussSelected
            ? (gaussCannon == null || gaussCannon.IsReady(Time.time) ? "GAUSS READY" : "GAUSS " + gaussCannon.RemainingCooldown(Time.time).ToString("F1") + "s")
            : "BURST " + (autocannon != null ? autocannon.BurstSize : Mathf.Max(2, burstSize));
        public string AimModeLabel => bombSelected ? "IMPACT AREA" : missileSelected ? "CURSOR" :
            gaussSelected ? "FIXED" : freeAim != null ? freeAim.ModeLabel : "FIXED";
        public string SoundModeLabel => soundVariants != null && soundVariants.Length == 4 && soundVariants[1] != null
            ? "MG 02" : "SYNTH FALLBACK";

        public void Configure(Material tracer, AudioClip shot)
        {
            tracerMaterial = tracer;
            shotClip = shot;
        }

        // Editor-side importer sets four short CC0 variants; raw source stays in Resources/Audio.
        public void ConfigureSoundVariants(AudioClip[] clips)
        {
            if (clips == null || clips.Length != 4 || System.Array.Exists(clips, c => c == null))
                throw new System.ArgumentException("Expected four imported machine gun clips.");
            soundVariants = (AudioClip[])clips.Clone();
            burstEmitter?.SetSoundVariants(soundVariants);
        }

        public void ConfigureAim(BoundedAim component) { freeAim = component; }
        public void ConfigureMissileLock(TargetLock component) { missileLock = component; }
        public void ConfigureSelection(WeaponSelection component)
        {
            selection = component;
            lastSelectionRevision = component != null ? component.SelectionRevision : 0;
            // An existing component may be re-equipped when entering a new scene.
            // Use the selected slot without changing cooldown or shot counters.
            if (component != null)
            {
                gaussSelected = component.Selected == WeaponMode.Gauss;
                missileSelected = component.Selected == WeaponMode.Missile;
                bombSelected = component.Selected == WeaponMode.Bomb;
                autocannon?.ClearPending();
            }
        }

        protected virtual void Awake()
        {
            registerHitCallback = RegisterHit;
            // Scene-authored settings are still serialized on WeaponLabCannon through
            // this base class. Shot presentation belongs to independent emitters.
            burstEmitter = GetComponent<AutocannonEmitter>();
            if (burstEmitter == null) burstEmitter = gameObject.AddComponent<AutocannonEmitter>();
            gaussEmitter = GetComponent<GaussEmitter>();
            if (gaussEmitter == null) gaussEmitter = gameObject.AddComponent<GaussEmitter>();
            // Prefab jets use chassis sockets. Legacy scene jets retain original offsets.
            var assembly = GetComponent<JetAssembly>();
            if (assembly != null && assembly.IsConfigured)
            {
                burstEmitter.BindChassis(assembly.Chassis);
                gaussEmitter.BindChassis(assembly.Chassis);
            }
            burstEmitter.Configure(tracerMaterial, shotClip, soundVariants, soundVolume,
                muzzleVelocity, maxRange, tracerLength);
            gaussEmitter.Configure(tracerMaterial, soundVolume,
                gaussVelocity, gaussRange, gaussSightLength);

            autocannon = new Autocannon(roundsPerSecond, burstSize, burstCooldown, Fire);
            gaussCannon = new GaussCannon(gaussCooldown, FireGauss);
        }

        protected virtual void LateUpdate()
        {
            burstEmitter?.TickVisuals();
            if (selection != null && lastSelectionRevision != selection.SelectionRevision)
            {
                lastSelectionRevision = selection.SelectionRevision;
                // Any slot-key press (including reselect) cancels the remaining
                // burst, while retaining its ordinary inter-burst cooldown.
                autocannon?.CancelBurst(Time.time);
                gaussSelected = selection.Selected == WeaponMode.Gauss;
                missileSelected = selection.Selected == WeaponMode.Missile;
                bombSelected = selection.Selected == WeaponMode.Bomb;
            }
            // TargetLock runs later in LateUpdate (order 200), so mode changes are immediate.
            if (missileLock != null) missileLock.SetLockingEnabled(missileSelected);
            gaussEmitter?.TickVisuals(gaussSelected);

            var mouse = Mouse.current;
            bool held = Application.isFocused && mouse != null && mouse.leftButton.isPressed;
            bool cannonSelected = !gaussSelected && !missileSelected && !bombSelected;
            bool burstActive = cannonSelected && autocannon != null && autocannon.HasPendingShots;
            if (freeAim != null) freeAim.Sample(cannonSelected, held || burstActive);
            if (missileSelected || bombSelected) return; // Separate launchers own these slots.
            if (gaussSelected)
            {
                gaussCannon?.Tick(mouse != null && Application.isFocused &&
                    mouse.leftButton.wasPressedThisFrame, Time.time);
                return;
            }
            if (tracerMaterial == null)
            {
                autocannon?.ResetMissingMaterial(Time.time);
                return;
            }
            autocannon?.Tick(held, Time.time);
        }

        private void Fire()
        {
            Vector3 direction = freeAim != null ? freeAim.FireDirection : transform.forward;
            burstEmitter.Emit(direction, registerHitCallback, FiredCount);
            FiredCount++;
        }

        private void FireGauss()
        {
            gaussEmitter.Emit(registerHitCallback);
            FiredCount++;
        }

        public void RegisterHit() { HitCount++; }
    }
}
