using UnityEngine;

namespace AirCombat
{
    // Scene overrides on the player prefab. No global state or time-scale changes.
    // In a player build every option is inert, even if saved enabled in a scene.
    [DisallowMultipleComponent]
    [AddComponentMenu("Air Combat/Mission Cheats")]
    public sealed class MissionCheats : MonoBehaviour
    {
        [Header("Editor-only test options")]
        [Tooltip("Start at the end of the normal takeoff. Applies on Play and R.")]
        [SerializeField] private bool skipTakeoff;
        [Tooltip("Hold Space for triple player displacement; does not accelerate simulation.")]
        [UnityEngine.Serialization.FormerlySerializedAs("doubleSpeed")]
        [SerializeField] private bool tripleSpeed;
        [Tooltip("Ignore aircraft terrain collision. Operation boundary still applies.")]
        [SerializeField] private bool noClip;
        [Tooltip("Ignore aircraft HP damage. Terrain and mission failure are independent.")]
        [SerializeField] private bool invulnerable;
        [Tooltip("Keep the shared W/S maneuver fuel tank full.")]
        [SerializeField] private bool infiniteAfterburner;
        [Tooltip("Allow assisted landing before primary objectives are complete, where supported.")]
        [SerializeField] private bool allowEarlyLanding;

        public bool SkipTakeoff => Allows(skipTakeoff);
        public bool TripleSpeed => Allows(tripleSpeed);
        public bool NoClip => Allows(noClip);
        public bool Invulnerable => Allows(invulnerable);
        public bool InfiniteAfterburner => Allows(infiniteAfterburner);
        public bool AllowEarlyLanding => Allows(allowEarlyLanding);

        private bool Allows(bool option)
        {
#if UNITY_EDITOR
            return isActiveAndEnabled && option;
#else
            return false;
#endif
        }
    }
}
