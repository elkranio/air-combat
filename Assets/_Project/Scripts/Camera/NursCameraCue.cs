using UnityEngine;

namespace AirCombat
{
    // GroundAttackLab-only, aircraft-safe NURS camera lead. Runs after ordinary
    // follow/look-ahead and never pushes the aircraft off the Game viewport.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(110)]
    public sealed class NursCameraCue : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float selectedExtraLead = 24f;
        [SerializeField, Min(0f)] private float committedExtraLead = 70f;
        [SerializeField, Range(.05f, .4f)] private float aircraftViewportMargin = .2f;
        [SerializeField, Min(0.01f)] private float transitionSeconds = .65f;

        private Camera viewCamera;
        private Transform aircraft;
        private WeaponSelection selection;
        private CabrageManeuver cabrage;
        private Vector3 currentOffset;
        private Vector3 offsetVelocity;

        public void Configure(Camera camera, Transform player, WeaponSelection weapons,
            CabrageManeuver maneuver)
        {
            viewCamera = camera;
            aircraft = player;
            selection = weapons;
            cabrage = maneuver;
            currentOffset = offsetVelocity = Vector3.zero;
        }

        private void LateUpdate()
        {
            if (viewCamera == null || aircraft == null || selection == null) return;
            Vector3 desired = Vector3.zero;
            if (selection.Selected == WeaponMode.Nurs)
            {
                Vector3 forward = Vector3.ProjectOnPlane(aircraft.forward, Vector3.up).normalized;
                if (forward.sqrMagnitude > .001f)
                    desired = forward * (cabrage != null && cabrage.IsCommitted
                        ? committedExtraLead : selectedExtraLead);
            }

            // The 600m impact area cannot fit in the existing 80m ortho half-view
            // alongside the jet. Favor usable flight framing over target centering.
            Vector3 smoothed = Vector3.SmoothDamp(currentOffset, desired, ref offsetVelocity,
                transitionSeconds, Mathf.Infinity, Time.deltaTime);
            float allowedFraction = AircraftSafeFraction(smoothed);
            currentOffset = smoothed * allowedFraction;
            if (allowedFraction < 1f) offsetVelocity *= allowedFraction;
            viewCamera.transform.position += currentOffset;
        }

        private float AircraftSafeFraction(Vector3 extraCameraOffset)
        {
            // The camera is orthographic; translating it by +offset is exactly
            // equivalent to projecting the aircraft at worldPosition - offset.
            // Clamp the *smoothed* offset, not just the target, so a sudden Q/E
            // turn cannot carry the aircraft offscreen during the transition.
            Vector3 before = viewCamera.WorldToViewportPoint(aircraft.position);
            Vector3 after = viewCamera.WorldToViewportPoint(aircraft.position - extraCameraOffset);
            float fraction = 1f;
            fraction = ClampAxis(before.x, after.x - before.x, fraction);
            fraction = ClampAxis(before.y, after.y - before.y, fraction);
            return Mathf.Clamp01(fraction);
        }

        private float ClampAxis(float start, float delta, float limit)
        {
            if (delta > .00001f)
                return Mathf.Min(limit, Mathf.Max(0f, (1f - aircraftViewportMargin - start) / delta));
            if (delta < -.00001f)
                return Mathf.Min(limit, Mathf.Max(0f, (start - aircraftViewportMargin) / -delta));
            return limit;
        }

        public void ResetCue()
        {
            currentOffset = offsetVelocity = Vector3.zero;
        }

        private void OnDisable() { ResetCue(); }
    }
}
