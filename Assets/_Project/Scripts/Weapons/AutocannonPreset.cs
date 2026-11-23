using UnityEngine;

namespace AirCombat
{
    // Reusable variant data. Duplicate this asset to create a different cannon
    // without forking pilot/AI code or editing the shared aircraft prefab.
    [CreateAssetMenu(menuName = "Air Combat/Weapons/Autocannon Preset")]
    public sealed class AutocannonPreset : ScriptableObject
    {
        [SerializeField] private string weaponId = "AC-01";
        [Header("Burst")]
        [SerializeField, Min(1f)] private float roundsPerSecond = 14f;
        [SerializeField, Range(2, 8)] private int burstSize = 4;
        [SerializeField, Min(.05f)] private float burstCooldown = .45f;
        [Header("Physical round")]
        [SerializeField, Min(1f)] private float muzzleVelocity = 420f;
        [SerializeField, Min(1f)] private float maxRange = 430f;
        [SerializeField, Min(1)] private int hitDamage = 1;
        [Header("Presentation")]
        [SerializeField] private Material tracerMaterial;
        [SerializeField, Min(.1f)] private float tracerLength = 8f;
        [Header("Arcade tracer (optional, modular mounts only)")]
        [SerializeField] private Material glowMaterial;
        [SerializeField, Range(.3f, 1.5f)] private float arcadeCoreWidth = .72f;
        [SerializeField, Range(1f, 5f)] private float arcadeGlowWidth = 2.8f;
        [SerializeField, Range(.1f, 1f)] private float arcadeGlowAlpha = .64f;
        [SerializeField, Range(.04f, .2f)] private float arcadeFlashSeconds = .085f;
        [SerializeField] private AudioClip fallbackShot;
        [SerializeField] private AudioClip[] soundVariants;
        [SerializeField, Range(.01f, 1f)] private float soundVolume = .3f;

        public string WeaponId => weaponId;
        public float RoundsPerSecond => roundsPerSecond;
        public int BurstSize => burstSize;
        public float BurstCooldown => burstCooldown;
        public float MuzzleVelocity => muzzleVelocity;
        public float MaxRange => maxRange;
        public int HitDamage => hitDamage;
        public Material TracerMaterial => tracerMaterial;
        public float TracerLength => tracerLength;
        public Material GlowMaterial => glowMaterial;
        public float ArcadeCoreWidth => arcadeCoreWidth;
        public float ArcadeGlowWidth => arcadeGlowWidth;
        public float ArcadeGlowAlpha => arcadeGlowAlpha;
        public float ArcadeFlashSeconds => arcadeFlashSeconds;
        public AudioClip FallbackShot => fallbackShot;
        public AudioClip[] SoundVariants => soundVariants;
        public float SoundVolume => soundVolume;

#if UNITY_EDITOR
        // One-time migration of the exact accepted lab sound/ballistics settings.
        public void SetAuthoringBaseline(Material tracer, AudioClip shot, AudioClip[] variants,
            float rate, int count, float cooldown, float velocity, float range,
            float length, float volume)
        {
            weaponId = "AC-01 BURST";
            tracerMaterial = tracer;
            fallbackShot = shot;
            soundVariants = variants != null ? (AudioClip[])variants.Clone() : null;
            roundsPerSecond = rate;
            burstSize = count;
            burstCooldown = cooldown;
            muzzleVelocity = velocity;
            maxRange = range;
            tracerLength = length;
            soundVolume = volume;
            hitDamage = 1;
        }
#endif
    }
}
