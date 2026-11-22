using UnityEngine;
using UnityEngine.InputSystem;

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

        [SerializeField, Range(60f, 90f)] private float pitch = 75f;

        private bool pitchHotkeysEnabled = true;
        public void SetPitchHotkeysEnabled(bool enabled) { pitchHotkeysEnabled = enabled; }

        private float height;
        private Vector3 velocity;
        private Vector3 unshakenFocus;
        private float shakeWeight;

        private void Awake()
        {
            if (target == null) return;
            height = Mathf.Max(0.1f, transform.position.y - target.position.y);
            unshakenFocus = target.position;
            ApplyPose(Vector3.zero);
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            // Scene can disable pitch keys while retaining its authored angle.
            var keyboard = Keyboard.current;
            if (keyboard != null && Application.isFocused && pitchHotkeysEnabled)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) pitch = 90f;
                if (keyboard.digit2Key.wasPressedThisFrame) pitch = 75f;
                if (keyboard.digit3Key.wasPressedThisFrame) pitch = 65f;
            }

            // Smooth the point of interest, not the tilt-dependent camera offset.
            unshakenFocus = smoothTime <= 0f
                ? target.position
                : Vector3.SmoothDamp(unshakenFocus, target.position, ref velocity, smoothTime);
            shakeWeight = Mathf.MoveTowards(shakeWeight,
                afterburner != null && afterburner.IsActive ? 1f : 0f, Time.deltaTime * 8f);
            float phase = Time.time * boostShakeFrequency * 2f * Mathf.PI;
            var shake = new Vector3(Mathf.Sin(phase), 0f, Mathf.Sin(phase * 0.73f + 1f));
            ApplyPose(shake * (boostShakeAmplitude * shakeWeight));
        }

        private void ApplyPose(Vector3 shake)
        {
            float angle = Mathf.Clamp(pitch, 60f, 90f);
            var rotation = Quaternion.Euler(angle, 0f, 0f);
            // Preserve vertical camera height and aim at the same flight-plane point.
            float distance = height / Mathf.Sin(angle * Mathf.Deg2Rad);
            var position = unshakenFocus - (rotation * Vector3.forward) * distance;
            transform.SetPositionAndRotation(position + shake, rotation);
        }
    }
}
