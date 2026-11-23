using UnityEngine;
using UnityEngine.InputSystem;

namespace AirCombat
{
    // C1 only: camera-relative screen heading stays diagonal while the map turns.
    // FlightController and aircraft world heading are deliberately unchanged.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(200)]
    public sealed class HeadingLockedObliqueCameraStudy : MonoBehaviour
    {
        [SerializeField] private Transform aircraft;
        [SerializeField, Range(45f, 75f)] private float obliquePitch = 58f;
        [SerializeField, Min(0f)] private float forwardLead = 28f;
        [SerializeField, Min(0.01f)] private float positionSmoothTime = 0.12f;
        [SerializeField, Min(0.01f)] private float rotationSmoothTime = 0.14f;

        private bool headingLocked = true;
        private bool showHint = true;
        private float cameraHeight;
        private float yaw;
        private float yawVelocity;
        private float pitch;
        private float pitchVelocity;
        private Vector3 focus;
        private Vector3 focusVelocity;
        private GUIStyle style;

        public void Configure(Transform player) => aircraft = player;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (aircraft == null)
            {
                Debug.LogError("Camera C1: missing aircraft reference.", this);
                enabled = false;
                return;
            }

            cameraHeight = Mathf.Max(20f, transform.position.y - aircraft.position.y);
            GetComponent<Camera>().orthographicSize = 80f;
            SnapPose();
        }

        private void Update()
        {
            var keys = Keyboard.current;
            if (keys == null || !Application.isFocused) return;

            if (keys.cKey.wasPressedThisFrame) headingLocked = !headingLocked;
            if (keys.digit1Key.wasPressedThisFrame) obliquePitch = 50f;
            if (keys.digit2Key.wasPressedThisFrame) obliquePitch = 58f;
            if (keys.digit3Key.wasPressedThisFrame) obliquePitch = 65f;
            if (keys.hKey.wasPressedThisFrame) showHint = !showHint;
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying || aircraft == null) return;

            // Existing art-study R reset teleports the player; follow immediately.
            var keys = Keyboard.current;
            if (keys != null && Application.isFocused && keys.rKey.wasPressedThisFrame)
            {
                SnapPose();
                return;
            }

            float targetYaw = headingLocked ? aircraft.eulerAngles.y - 45f : 0f;
            float targetPitch = headingLocked ? obliquePitch : 65f;
            Vector3 targetFocus = aircraft.position +
                                  (headingLocked ? aircraft.forward * forwardLead : Vector3.zero);

            yaw = Mathf.SmoothDampAngle(yaw, targetYaw, ref yawVelocity, rotationSmoothTime);
            pitch = Mathf.SmoothDamp(pitch, targetPitch, ref pitchVelocity, rotationSmoothTime);
            focus = Vector3.SmoothDamp(focus, targetFocus, ref focusVelocity, positionSmoothTime);
            ApplyPose();
        }

        private void SnapPose()
        {
            yaw = headingLocked ? aircraft.eulerAngles.y - 45f : 0f;
            pitch = headingLocked ? obliquePitch : 65f;
            focus = aircraft.position +
                    (headingLocked ? aircraft.forward * forwardLead : Vector3.zero);
            focusVelocity = Vector3.zero;
            yawVelocity = 0f;
            pitchVelocity = 0f;
            ApplyPose();
        }

        private void ApplyPose()
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            // Maintain the original camera's height relative to the flight plane,
            // even when tilt changes. Focus sits ahead of the aircraft in oblique mode.
            float distance = cameraHeight / Mathf.Sin(pitch * Mathf.Deg2Rad);
            transform.SetPositionAndRotation(focus - rotation * Vector3.forward * distance, rotation);
        }

        private void OnGUI()
        {
            if (!showHint || Event.current.type != EventType.Repaint) return;
            if (style == null)
                style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    normal = { textColor = Color.white }
                };
            float y = Mathf.Max(12f, Screen.height - 75f);
            GUI.Box(new Rect(12, y, 415, 63), GUIContent.none);
            GUI.Label(new Rect(22, y + 5, 395, 22),
                headingLocked ? $"C1 / HEADING LOCKED 3/4   pitch {obliquePitch:F0}" : "C1 / ORIGINAL NORTH-UP 65", style);
            GUI.Label(new Rect(22, y + 28, 395, 22),
                "C: toggle camera   1/2/3: 50/58/65   H: hide", style);
        }
    }
}
