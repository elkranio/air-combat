using UnityEngine;

namespace AirCombat
{
    // GroundAttackLab-only tactical pan. Runs after ordinary follow/look-ahead;
    // the global camera prefab and its fixed orthographic size remain untouched.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(110)]
    public sealed class NursCameraCue : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float previewTargetOffset = 45f;
        [SerializeField, Min(0.01f)] private float transitionSeconds = .65f;

        private Camera viewCamera;
        private Transform aircraft;
        private WeaponSelection selection;
        private CabrageManeuver cabrage;
        private NursImpactAreaMarker area;
        private Vector3 currentOffset;
        private Vector3 offsetVelocity;

        public void Configure(Camera camera, Transform player, WeaponSelection weapons,
            CabrageManeuver maneuver, NursImpactAreaMarker marker)
        {
            viewCamera = camera;
            aircraft = player;
            selection = weapons;
            cabrage = maneuver;
            area = marker;
            currentOffset = offsetVelocity = Vector3.zero;
        }

        private void LateUpdate()
        {
            if (viewCamera == null || aircraft == null || selection == null) return;
            Vector3 desired = Vector3.zero;
            if (selection.Selected == WeaponMode.Nurs && area != null && area.HasArea)
            {
                Vector3 forward = Vector3.ProjectOnPlane(aircraft.forward, Vector3.up).normalized;
                if (forward.sqrMagnitude > .001f)
                {
                    // CameraLookAhead already shifted the camera earlier this frame.
                    // Find its current horizontal focus on the aircraft-altitude plane
                    // rather than adding a hard-coded second copy of its lead.
                    Vector3 camPosition = viewCamera.transform.position;
                    Vector3 camForward = viewCamera.transform.forward;
                    float alongRay = Mathf.Abs(camForward.y) > .001f
                        ? (aircraft.position.y - camPosition.y) / camForward.y : 0f;
                    Vector3 focus = camPosition + camForward * alongRay;
                    float remaining = Vector3.Dot(area.AreaCenter - focus, forward);
                    float margin = cabrage != null && cabrage.IsCommitted ? 0f : previewTargetOffset;
                    desired = forward * Mathf.Max(0f, remaining - margin);
                }
            }
            currentOffset = Vector3.SmoothDamp(currentOffset, desired, ref offsetVelocity,
                transitionSeconds, Mathf.Infinity, Time.deltaTime);
            viewCamera.transform.position += currentOffset;
        }

        public void ResetCue()
        {
            currentOffset = offsetVelocity = Vector3.zero;
        }

        private void OnDisable() { ResetCue(); }
    }
}
