using UnityEngine;

namespace AirCombat
{
    // Weapon Lab only: add a smooth world-space offset AFTER TopDownCameraFollow.
    // The shared camera follow, its pitch presets and all other scenes stay untouched.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class WeaponLabCameraLead : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float leadFractionOfHalfView = 0.45f;
        [SerializeField, Min(1f)] private float boostLeadMultiplier = 1.3f;
        [SerializeField, Min(0.01f)] private float transitionSeconds = 0.42f;

        private Camera viewCamera;
        private Transform aircraft;
        private Afterburner afterburner;
        private Vector3 currentOffset;
        private Vector3 offsetVelocity;

        public bool LookAheadEnabled { get; private set; } = true;

        public void Configure(Camera camera, Transform target, Afterburner boost)
        {
            viewCamera = camera;
            aircraft = target;
            afterburner = boost;
            LookAheadEnabled = true;
            currentOffset = Vector3.zero;
            offsetVelocity = Vector3.zero;
        }

        public void Toggle() => LookAheadEnabled = !LookAheadEnabled;

        private void LateUpdate()
        {
            if (viewCamera == null || aircraft == null) return;

            // Use heading, not the mouse pointer. Keep the offset on the XZ flight plane
            // so changing camera pitch never changes altitude or its framing rules.
            Vector3 desiredOffset = Vector3.zero;
            if (LookAheadEnabled)
            {
                Vector3 heading = Vector3.ProjectOnPlane(aircraft.forward, Vector3.up);
                if (heading.sqrMagnitude > 0.0001f)
                {
                    float boostFactor = afterburner != null && afterburner.IsActive ? boostLeadMultiplier : 1f;
                    desiredOffset = heading.normalized * (viewCamera.orthographicSize * leadFractionOfHalfView * boostFactor);
                }
            }

            currentOffset = Vector3.SmoothDamp(currentOffset, desiredOffset, ref offsetVelocity,
                transitionSeconds, Mathf.Infinity, Time.deltaTime);
            // TopDownCameraFollow already wrote its unmodified position this frame.
            // Add exactly one smoothed offset; never accumulate camera drift.
            viewCamera.transform.position += currentOffset;
        }
    }
}
