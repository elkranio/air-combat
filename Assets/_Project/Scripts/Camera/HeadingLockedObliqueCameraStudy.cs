using UnityEngine;
using UnityEngine.InputSystem;

namespace AirCombat
{
    // C1 art camera and C2/C3 combat comparison. The preferred C3 is fixed-yaw
    // 50-degree 3/4 with smooth aircraft-heading look-ahead and a small boost bonus.
    // FlightController and aircraft world heading are deliberately unchanged.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(105)] // Before NursCameraCue (110) and TargetLock (200).
    public sealed class HeadingLockedObliqueCameraStudy : MonoBehaviour
    {
        [SerializeField] private Transform aircraft;
        [SerializeField, Range(45f, 75f)] private float obliquePitch = 50f;
        [SerializeField, Min(0f)] private float forwardLead = 28f;
        [Header("C3 / fixed-world combat look-ahead")]
        [SerializeField, Min(0f)] private float cruiseLead = 48f;
        [SerializeField, Min(0f)] private float boostExtraLead = 14f;
        [SerializeField, Min(0.01f)] private float leadTransitionSeconds = 0.42f;
        [SerializeField, Range(.05f, .4f)] private float aircraftViewportMargin = .2f;
        [SerializeField, Min(0.01f)] private float positionSmoothTime = 0.12f;
        [SerializeField, Min(0.01f)] private float rotationSmoothTime = 0.14f;

        [SerializeField] private bool combatHotkeys;
        // Art C1 retains its original mode; combat C1b starts with C2 fixed yaw.
        private bool headingLocked = true;
        private bool showHint = true;
        private float cameraHeight;
        private float yaw;
        private float yawVelocity;
        private float pitch;
        private float pitchVelocity;
        private Vector3 focus;
        private Vector3 focusVelocity;
        private Vector3 currentLead;
        private Vector3 leadVelocity;
        private Camera viewCamera;
        private Afterburner afterburner;
        private GUIStyle style;

        public bool HeadingLocked => headingLocked;
        public void Configure(Transform player) => aircraft = player;
        public void SetPreferredPitch(float degrees) => obliquePitch = Mathf.Clamp(degrees, 45f, 75f);
        public void SetCombatHotkeys(bool enabled) => combatHotkeys = enabled;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (aircraft == null)
            {
                Debug.LogError("Camera C1: missing aircraft reference.", this);
                enabled = false;
                return;
            }

            // Don't regenerate the already committed C1b scene: combatHotkeys is
            // its existing serialized flag. Art-only C1 still starts heading-locked.
            headingLocked = !combatHotkeys;
            viewCamera = GetComponent<Camera>();
            afterburner = aircraft.GetComponent<Afterburner>();
            cameraHeight = Mathf.Max(20f, transform.position.y - aircraft.position.y);
            viewCamera.orthographicSize = 80f;
            SnapPose();
        }

        private void Update()
        {
            var keys = Keyboard.current;
            if (keys == null || !Application.isFocused) return;

            // Combat: C compares fixed C3 (+ heading lead) against the old
            // rotating C1b; original art-only C1 keeps its existing behavior.
            if (keys.cKey.wasPressedThisFrame) headingLocked = !headingLocked;
            // C1 art-only scene uses 1/2/3. C1b combat uses 1-6 for
            // weapons, so switch pitch presets to J/K/L there.
            if (combatHotkeys ? keys.jKey.wasPressedThisFrame : keys.digit1Key.wasPressedThisFrame)
                obliquePitch = 50f;
            if (combatHotkeys ? keys.kKey.wasPressedThisFrame : keys.digit2Key.wasPressedThisFrame)
                obliquePitch = 58f;
            if (combatHotkeys ? keys.lKey.wasPressedThisFrame : keys.digit3Key.wasPressedThisFrame)
                obliquePitch = 65f;
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

            float targetYaw = CameraYaw();
            float targetPitch = CameraPitch();
            Vector3 targetFocus = CameraFocus();

            yaw = Mathf.SmoothDampAngle(yaw, targetYaw, ref yawVelocity, rotationSmoothTime);
            pitch = Mathf.SmoothDamp(pitch, targetPitch, ref pitchVelocity, rotationSmoothTime);
            focus = Vector3.SmoothDamp(focus, targetFocus, ref focusVelocity, positionSmoothTime);
            if (combatHotkeys)
                currentLead = Vector3.SmoothDamp(currentLead, DesiredCombatLead(),
                    ref leadVelocity, leadTransitionSeconds, Mathf.Infinity, Time.deltaTime);
            ApplyPose();
        }

        private void SnapPose()
        {
            yaw = CameraYaw();
            pitch = CameraPitch();
            focus = CameraFocus();
            currentLead = combatHotkeys ? DesiredCombatLead() : Vector3.zero;
            leadVelocity = Vector3.zero;
            focusVelocity = Vector3.zero;
            yawVelocity = 0f;
            pitchVelocity = 0f;
            ApplyPose();
        }

        private float CameraYaw()
        {
            // C2: match the original upper-right aircraft presentation at spawn,
            // but do not change this yaw as the jet turns. No surprise rotation.
            if (combatHotkeys && !headingLocked) return -45f;
            return headingLocked ? aircraft.eulerAngles.y - 45f : 0f;
        }

        private float CameraPitch() => (headingLocked || combatHotkeys) ? obliquePitch : 65f;

        // Combat mode smooths position and lead independently: turning the jet
        // shifts the view towards its heading without ever rotating the world.
        // The art-only C1 scene preserves its original unmodified focus logic.
        private Vector3 CameraFocus() => combatHotkeys ? aircraft.position : aircraft.position +
            (headingLocked ? aircraft.forward * forwardLead : Vector3.zero);

        private Vector3 DesiredCombatLead()
        {
            Vector3 heading = Vector3.ProjectOnPlane(aircraft.forward, Vector3.up);
            if (heading.sqrMagnitude < .0001f) return Vector3.zero;
            float distance = headingLocked ? forwardLead :
                cruiseLead + (afterburner != null && afterburner.IsActive ? boostExtraLead : 0f);
            return heading.normalized * distance;
        }

        private void ApplyPose()
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            // Maintain camera height at any pitch. The follow point is smoothed
            // separately from the forward lead so turning never jerks the camera.
            float distance = cameraHeight / Mathf.Sin(pitch * Mathf.Deg2Rad);
            transform.SetPositionAndRotation(focus - rotation * Vector3.forward * distance, rotation);
            if (combatHotkeys)
                transform.position += currentLead * AircraftSafeFraction(currentLead);
            // NursCameraCue (order 110) adds the cabrage-specific lead afterward;
            // its own clamp keeps the aircraft inside the same viewport margin.
        }

        private float AircraftSafeFraction(Vector3 additionalLead)
        {
            // Orthographic translation +lead moves the jet's image by -lead.
            // Keep at least a 20% margin even during boost or a sharp Q/E turn.
            Vector3 before = viewCamera.WorldToViewportPoint(aircraft.position);
            Vector3 after = viewCamera.WorldToViewportPoint(aircraft.position - additionalLead);
            float allowed = 1f;
            allowed = ClampAxis(before.x, after.x - before.x, allowed);
            allowed = ClampAxis(before.y, after.y - before.y, allowed);
            return Mathf.Clamp01(allowed);
        }

        private float ClampAxis(float start, float delta, float allowed)
        {
            if (delta > .00001f)
                return Mathf.Min(allowed, Mathf.Max(0f, (1f - aircraftViewportMargin - start) / delta));
            if (delta < -.00001f)
                return Mathf.Min(allowed, Mathf.Max(0f, (start - aircraftViewportMargin) / -delta));
            return allowed;
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
            string mode = combatHotkeys
                ? headingLocked ? $"C1b / ROTATING WORLD   pitch {obliquePitch:F0}"
                    : $"C3 / FIXED + LEAD   pitch {obliquePitch:F0}"
                : headingLocked ? $"C1 / HEADING LOCKED 3/4   pitch {obliquePitch:F0}"
                    : "C1 / ORIGINAL NORTH-UP 65";
            GUI.Label(new Rect(22, y + 5, 395, 22), mode, style);
            GUI.Label(new Rect(22, y + 28, 395, 22),
                combatHotkeys ? "C: fixed/rotating   J/K/L: 50/58/65   1-6: guns"
                    : "C: toggle camera   1/2/3: 50/58/65   H: hide", style);
        }
    }
}
