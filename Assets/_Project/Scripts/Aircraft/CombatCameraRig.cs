using UnityEngine;

namespace AirCombat
{
    // Reusable camera composition; mission chooses the player at runtime.
    // The camera prefab deliberately has no cross-scene object references.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class CombatCameraRig : MonoBehaviour
    {
        private Camera viewCamera;
        private ArcadeCameraFollow arcadeFollow;

        public Camera ViewCamera => viewCamera != null ? viewCamera : GetComponent<Camera>();

        public void BindPlayer(Transform player, Afterburner boost, bool allowPitchHotkeys = false)
        {
            if (player == null) throw new System.ArgumentNullException(nameof(player));
            if (viewCamera == null) viewCamera = GetComponent<Camera>();
            // The arcade follow is the only supported camera pose writer.
            if (arcadeFollow == null) arcadeFollow = GetComponent<ArcadeCameraFollow>();
            if (arcadeFollow == null || !arcadeFollow.enabled)
                throw new System.InvalidOperationException("Camera rig is missing an enabled ArcadeCameraFollow.");
            arcadeFollow.Configure(player, boost);
        }
    }
}
