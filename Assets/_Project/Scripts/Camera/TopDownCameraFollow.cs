using UnityEngine;

namespace AirCombat
{
    [DisallowMultipleComponent]
    public sealed class TopDownCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField, Min(0f)] private float smoothTime = 0.12f;

        private Vector3 offset;
        private Vector3 velocity;

        private void Awake()
        {
            if (target != null)
                offset = transform.position - target.position;
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            var desiredPosition = target.position + offset;
            transform.position = smoothTime <= 0f
                ? desiredPosition
                : Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, smoothTime);
        }
    }
}
