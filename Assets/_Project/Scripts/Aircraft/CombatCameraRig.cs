using UnityEngine;

namespace AirCombat
{
    // Reusable camera composition; mission chooses player and lock target at runtime.
    // The camera prefab deliberately has no cross-scene object references.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera), typeof(TargetLock))]
    public sealed class CombatCameraRig : MonoBehaviour
    {
        private Camera viewCamera;
        private TopDownCameraFollow follow;
        private CameraLookAhead lookAhead;
        private TargetLock targetLock;
        private ArcadeCameraFollow arcadeFollow;

        public Camera ViewCamera => viewCamera != null ? viewCamera : GetComponent<Camera>();
        public TargetLock Lock => targetLock != null ? targetLock : GetComponent<TargetLock>();

        public void BindPlayer(Transform player, Afterburner boost, bool allowPitchHotkeys = false)
        {
            if (player == null) throw new System.ArgumentNullException(nameof(player));
            if (viewCamera == null) viewCamera = GetComponent<Camera>();
            // The game camera has one authoritative pose writer. Legacy labs
            // still use TopDownCameraFollow; the new arcade prefab uses only
            // ArcadeCameraFollow, with no competing TopDown/LookAhead components.
            if (arcadeFollow == null) arcadeFollow = GetComponent<ArcadeCameraFollow>();
            if (arcadeFollow != null && arcadeFollow.enabled)
            {
                arcadeFollow.Configure(player, boost);
                return;
            }

            if (follow == null) follow = GetComponent<TopDownCameraFollow>();
            if (follow == null)
                throw new System.InvalidOperationException("Camera rig is missing a usable follow component.");
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
