using UnityEngine;

namespace AirCombat
{
    // Reusable camera composition; mission chooses player and lock target at runtime.
    // The camera prefab deliberately has no cross-scene object references.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera), typeof(TopDownCameraFollow), typeof(TargetLock))]
    public sealed class CombatCameraRig : MonoBehaviour
    {
        private Camera viewCamera;
        private TopDownCameraFollow follow;
        private CameraLookAhead lookAhead;
        private TargetLock targetLock;

        public Camera ViewCamera => viewCamera != null ? viewCamera : GetComponent<Camera>();
        public TargetLock Lock => targetLock != null ? targetLock : GetComponent<TargetLock>();

        public void BindPlayer(Transform player, Afterburner boost, bool allowPitchHotkeys = false)
        {
            if (player == null) throw new System.ArgumentNullException(nameof(player));
            if (viewCamera == null) viewCamera = GetComponent<Camera>();
            if (follow == null) follow = GetComponent<TopDownCameraFollow>();
            if (lookAhead == null) lookAhead = GetComponent<CameraLookAhead>();
            follow.Configure(player, boost);
            follow.SetPitchHotkeysEnabled(allowPitchHotkeys);
            if (lookAhead != null) lookAhead.Configure(viewCamera, player, boost);
        }

        public void BindLockTarget(Transform enemy, float lockSeconds, float graceSeconds,
            ITargetLockEligibility eligibility = null)
        {
            Lock.ConfigureTarget(enemy, lockSeconds, graceSeconds, eligibility);
        }
    }
}
