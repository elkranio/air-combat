using UnityEngine;

namespace AirCombat
{
    [DisallowMultipleComponent]
    public sealed class TopDownCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Afterburner afterburner;
        [SerializeField, Min(0f)] private float boostShakeAmplitude = 0.55f;
        [SerializeField, Min(0f)] private float boostShakeFrequency = 9f;
        [SerializeField, Min(0f)] private float smoothTime = 0.12f;

        private Vector3 offset;
        private Vector3 velocity;
        private Vector3 unshakenPosition;
        private float shakeWeight;

        private void Awake()
        {
            unshakenPosition = transform.position;
            if (target != null)
                offset = transform.position - target.position;
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            var desiredPosition = target.position + offset;
            unshakenPosition = smoothTime <= 0f
                ? desiredPosition
                : Vector3.SmoothDamp(unshakenPosition, desiredPosition, ref velocity, smoothTime);
            shakeWeight = Mathf.MoveTowards(shakeWeight,
                afterburner != null && afterburner.IsActive ? 1f : 0f, Time.deltaTime * 8f);
            float phase = Time.time * boostShakeFrequency * 2f * Mathf.PI;
            var shake = new Vector3(Mathf.Sin(phase), 0f, Mathf.Sin(phase * 0.73f + 1f));
            transform.position = unshakenPosition + shake * (boostShakeAmplitude * shakeWeight);
        }
    }
}
