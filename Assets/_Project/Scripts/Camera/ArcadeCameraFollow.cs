using UnityEngine;
using UnityEngine.InputSystem;

namespace AirCombat
{
    // Production fixed-world arcade camera. No C1/C2 comparison modes, camera
    // hotkeys or scene-owned art dependencies. CombatCameraRig wires the player.
    // World yaw stays fixed; only the smoothed focus follows jet heading/boost.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(105)] // NursCameraCue is 110; TargetLock is 200.
    public sealed class ArcadeCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform aircraft;
        [Header("Framing")]
        [SerializeField, Range(40f, 80f)] private float pitch = 50f;
        [SerializeField, Range(-180f, 180f)] private float yaw = -45f;
        [SerializeField, Min(1f)] private float orthographicSize = 80f;
        [SerializeField, Min(0f)] private float cruiseLead = 57.6f;
        [SerializeField, Min(0f)] private float boostExtraLead = 16.8f;
        [SerializeField, Min(.01f)] private float leadTransitionSeconds = .42f;
        [SerializeField, Range(.05f, .4f)] private float aircraftViewportMargin = .2f;
        [SerializeField, Min(.01f)] private float positionSmoothTime = .12f;

        private Camera viewCamera;
        private Afterburner afterburner;
        private float cameraHeight;
        private Vector3 focus;
        private Vector3 focusVelocity;
        private Vector3 lead;
        private Vector3 leadVelocity;
        private bool initialized;
        private float shakeStarted, shakeDuration, shakeAmplitude;

        // Translation only, in screen axes; never feeds back into follow smoothing.
        // A lesser hit cannot cut short an active stronger kill/received-hit impulse.
        public void AddHitShake(float amplitude, float duration)
        {
            if (amplitude <= 0f || duration <= 0f) return;
            if (Time.time < shakeStarted + shakeDuration && amplitude < shakeAmplitude) return;
            shakeStarted = Time.time;
            shakeDuration = duration;
            shakeAmplitude = amplitude;
        }

        public void ClearHitShake()
        {
            shakeDuration = 0f;
            shakeAmplitude = 0f;
        }

        // Called by CombatCameraRig, never by a global search for Player objects.
        public void Configure(Transform player, Afterburner boost)
        {
            if (player == null) throw new System.ArgumentNullException(nameof(player));
            if (aircraft != player) initialized = false;
            aircraft = player;
            afterburner = boost;
            if (Application.isPlaying && !initialized) Initialize();
        }

        private void OnEnable()
        {
            if (Application.isPlaying && aircraft != null) Initialize();
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying || aircraft == null) return;
            if (!initialized) Initialize();

            var keyboard = Keyboard.current;
            if (keyboard != null && Application.isFocused && keyboard.rKey.wasPressedThisFrame)
            {
                SnapToAircraft();
                return;
            }

            focus = Vector3.SmoothDamp(focus, aircraft.position, ref focusVelocity,
                positionSmoothTime, Mathf.Infinity, Time.deltaTime);
            lead = Vector3.SmoothDamp(lead, DesiredLead(), ref leadVelocity,
                leadTransitionSeconds, Mathf.Infinity, Time.deltaTime);
            ApplyPose();
        }

        public void SnapToAircraft()
        {
            if (!Application.isPlaying || aircraft == null) return;
            ClearHitShake();
            if (!initialized) { Initialize(); return; }
            focus = aircraft.position;
            lead = DesiredLead();
            focusVelocity = Vector3.zero;
            leadVelocity = Vector3.zero;
            ApplyPose();
        }

        private void Initialize()
        {
            viewCamera = GetComponent<Camera>();
            if (afterburner == null) afterburner = aircraft.GetComponent<Afterburner>();
            cameraHeight = Mathf.Max(20f, transform.position.y - aircraft.position.y);
            viewCamera.orthographic = true;
            viewCamera.orthographicSize = orthographicSize;
            initialized = true;
            SnapToAircraft();
        }

        private Vector3 DesiredLead()
        {
            Vector3 heading = Vector3.ProjectOnPlane(aircraft.forward, Vector3.up);
            if (heading.sqrMagnitude < .0001f) return Vector3.zero;
            float distance = cruiseLead +
                (afterburner != null && afterburner.IsActive ? boostExtraLead : 0f);
            return heading.normalized * distance;
        }

        private void ApplyPose()
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            float distance = cameraHeight / Mathf.Sin(pitch * Mathf.Deg2Rad);
            transform.SetPositionAndRotation(focus - rotation * Vector3.forward * distance, rotation);
            transform.position += lead * SafeLeadFraction(lead);
            float elapsed = Time.time - shakeStarted;
            if (shakeDuration > 0f && elapsed < shakeDuration)
            {
                float fade = 1f - Mathf.Clamp01(elapsed / shakeDuration);
                float strength = shakeAmplitude * fade * fade;
                float phase = elapsed * 2f * Mathf.PI;
                transform.position += rotation * new Vector3(
                    Mathf.Cos(phase * 23f), .75f * Mathf.Sin(phase * 19f + .8f), 0f) * strength;
            }
        }

        private float SafeLeadFraction(Vector3 extra)
        {
            // Translation is equivalent to projecting the jet at position -extra.
            // Avoid losing it from the frame during boost or a sharp Q/E break turn.
            Vector3 before = viewCamera.WorldToViewportPoint(aircraft.position);
            Vector3 after = viewCamera.WorldToViewportPoint(aircraft.position - extra);
            float fraction = 1f;
            fraction = ClampAxis(before.x, after.x - before.x, fraction);
            fraction = ClampAxis(before.y, after.y - before.y, fraction);
            return Mathf.Clamp01(fraction);
        }

        private float ClampAxis(float start, float delta, float fraction)
        {
            if (delta > .00001f)
                return Mathf.Min(fraction, Mathf.Max(0f, (1f - aircraftViewportMargin - start) / delta));
            if (delta < -.00001f)
                return Mathf.Min(fraction, Mathf.Max(0f, (start - aircraftViewportMargin) / -delta));
            return fraction;
        }
    }
}
