using UnityEngine;

namespace AirCombat
{
    // Explicitly opt-in art preview, not the eventual mission/convoy AI.
    // The saved position and heading define the start of a tangent circle.
    [DisallowMultipleComponent]
    public sealed class WardenPatrolPreview : MonoBehaviour
    {
        [SerializeField] private WardenRotorVisual rotorVisual;
        [SerializeField, Min(0f)] private float speed = 24f;
        [SerializeField, Min(1f)] private float radius = 65f;
        [SerializeField] private bool clockwise = true;
        private Vector3 startPosition, startForward;
        private Quaternion startRotation;
        private float phase;
        private bool initialized;

        private void Awake()
        {
            startPosition = transform.position;
            startRotation = transform.rotation;
            startForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (startForward.sqrMagnitude < .5f) startForward = Vector3.forward;
            initialized = true;
        }

        public void RestartPreview()
        {
            if (!initialized) return;
            phase = 0f;
            transform.SetPositionAndRotation(startPosition, startRotation);
            if (rotorVisual != null) rotorVisual.ResetPhase();
        }

        private void Update()
        {
            if (!initialized || Time.deltaTime <= 0f) return;
            float r = Mathf.Max(1f, radius);
            float sign = clockwise ? 1f : -1f;
            phase = Mathf.Repeat(phase + Mathf.Max(0f, speed) / r * Mathf.Rad2Deg * Time.deltaTime, 360f);
            Vector3 center = startPosition + Vector3.Cross(Vector3.up, startForward) * (r * sign);
            Quaternion turn = Quaternion.AngleAxis(phase * sign, Vector3.up);
            transform.SetPositionAndRotation(center + turn * (startPosition - center),
                Quaternion.LookRotation(turn * startForward, Vector3.up));
        }
    }
}
