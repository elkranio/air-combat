using UnityEngine;

namespace AirCombat
{
    // Sandbox-only: recycles the surface under the camera without moving gameplay objects.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class SandboxSurface : MonoBehaviour
    {
        [SerializeField] private Transform cameraTarget;
        [SerializeField] private Transform grid;
        [SerializeField, Min(1f)] private float gridSpacing = 20f;

        private Vector3 groundOrigin;
        private Vector3 gridOrigin;

        private void Awake()
        {
            groundOrigin = transform.position;
            if (grid != null)
                gridOrigin = grid.position;
        }

        private void LateUpdate()
        {
            if (cameraTarget == null || grid == null)
                return;

            var position = cameraTarget.position - groundOrigin;
            var shift = new Vector3(
                Mathf.Round(position.x / gridSpacing) * gridSpacing,
                0f,
                Mathf.Round(position.z / gridSpacing) * gridSpacing);

            // Multiples of the grid spacing keep the visible lines stationary in world space.
            transform.position = groundOrigin + shift;
            grid.position = gridOrigin + shift;
        }
    }
}
