using UnityEngine;
using UnityEngine.InputSystem;

namespace AirCombat
{
    // GroundAttackLab-only prototype: commit to a real climb and descent before
    // G4b adds rockets. Player flight remains horizontally steered outside the maneuver.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-5)] // Input before FlightController; height before camera LateUpdate.
    public sealed class CabrageManeuver : MonoBehaviour
    {
        public enum ManeuverPhase { Idle, Climb, Recovery, EmergencyReturn }

        [Header("Maneuver")]
        [SerializeField, Min(.1f)] private float climbSeconds = 2.5f;
        [SerializeField, Min(.1f)] private float recoverySeconds = 2.5f;
        [SerializeField, Min(.1f)] private float emergencyReturnSeconds = 2.3f;
        [SerializeField, Min(1f)] private float climbHeight = 22f;
        [SerializeField, Range(0f, 65f)] private float noseUpDegrees = 33f;
        [SerializeField, Range(0f, 30f)] private float noseDownDegrees = 8f;
        [Header("Cooldown begins after return to nominal altitude")]
        [SerializeField, Min(0f)] private float normalCooldown = 12f;
        [SerializeField, Min(0f)] private float emergencyCooldown = 20f;

        private WeaponSelection selection;
        private BreakTurn breakTurn;
        private Transform visual;
        private float nominalAltitude;
        private float phaseTime;
        private float returnStartAltitude;
        private float returnDuration;
        private float readyAt;
        private bool configured;
        private ManeuverPhase phase = ManeuverPhase.Idle;

        public ManeuverPhase Phase => phase;
        public bool IsCommitted => phase != ManeuverPhase.Idle;
        public float CooldownLeft => Mathf.Max(0f, readyAt - Time.time);
        public float NominalAltitude => nominalAltitude;
        public string PhaseLabel => phase.ToString().ToUpperInvariant();
        // Reserved for G4b: rockets will be emitted across the climb, not on button press.
        public float ClimbProgress01 => phase == ManeuverPhase.Climb
            ? Mathf.Clamp01(phaseTime / Mathf.Max(.1f, climbSeconds)) : 0f;

        public void Configure(WeaponSelection weaponSelection, BreakTurn escape, Transform aircraftVisual)
        {
            if (configured) return;
            selection = weaponSelection;
            breakTurn = escape;
            visual = aircraftVisual;
            if (selection == null || breakTurn == null || visual == null)
            {
                Debug.LogError("G4a: Cabrage requires weapon selection, BreakTurn and aircraft visual.", this);
                enabled = false;
                return;
            }
            nominalAltitude = transform.position.y;
            configured = true;
        }

        private void Update()
        {
            if (!configured || phase != ManeuverPhase.Idle || Time.time < readyAt ||
                selection.Selected != WeaponMode.Nurs || selection.SelectionLocked ||
                breakTurn.IsBreaking || !Application.isFocused) return;

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                phase = ManeuverPhase.Climb;
                phaseTime = 0f;
                selection.SetSelectionLocked(true);
            }
        }

        // Called by FlightController before sampling the break command. A refused
        // break (insufficient fuel) leaves the climb/recovery completely unchanged.
        public bool TryEmergencyBreak(int side)
        {
            if (!configured || !IsCommitted || phase == ManeuverPhase.EmergencyReturn ||
                !breakTurn.TryBegin(side)) return false;

            returnStartAltitude = transform.position.y;
            float heightFraction = Mathf.Clamp01(
                (returnStartAltitude - nominalAltitude) / Mathf.Max(1f, climbHeight));
            returnDuration = Mathf.Lerp(1.1f, emergencyReturnSeconds, heightFraction);
            phase = ManeuverPhase.EmergencyReturn;
            phaseTime = 0f;
            return true;
        }

        private void LateUpdate()
        {
            if (!configured || phase == ManeuverPhase.Idle) return;

            phaseTime += Mathf.Max(0f, Time.deltaTime);
            float height = nominalAltitude;
            float pitch = 0f;
            switch (phase)
            {
                case ManeuverPhase.Climb:
                {
                    float t = Mathf.Clamp01(phaseTime / Mathf.Max(.1f, climbSeconds));
                    height += climbHeight * Mathf.SmoothStep(0f, 1f, t);
                    pitch = noseUpDegrees * Mathf.Sin(Mathf.PI * t);
                    if (t >= 1f)
                    {
                        phase = ManeuverPhase.Recovery;
                        phaseTime = 0f;
                    }
                    break;
                }
                case ManeuverPhase.Recovery:
                {
                    float t = Mathf.Clamp01(phaseTime / Mathf.Max(.1f, recoverySeconds));
                    height += climbHeight * (1f - Mathf.SmoothStep(0f, 1f, t));
                    pitch = -noseDownDegrees * Mathf.Sin(Mathf.PI * t);
                    if (t >= 1f) Complete(false);
                    break;
                }
                case ManeuverPhase.EmergencyReturn:
                {
                    float t = Mathf.Clamp01(phaseTime / Mathf.Max(.1f, returnDuration));
                    height = Mathf.Lerp(returnStartAltitude, nominalAltitude,
                        Mathf.SmoothStep(0f, 1f, t));
                    // A 90-degree Break Turn runs simultaneously with descent. Do not
                    // release controls if the turn lasts longer than the descent.
                    if (t >= 1f && !breakTurn.IsBreaking) Complete(true);
                    break;
                }
            }

            var position = transform.position;
            position.y = height;
            transform.position = position;

            // AircraftMotor owns banking and writes the normal visual rotation in
            // Update. Add pitch once in LateUpdate; never tilt the gameplay root.
            // During emergency BreakTurn owns the model bank instead.
            if (visual != null && !breakTurn.IsBreaking)
                visual.localRotation = Quaternion.AngleAxis(-pitch, Vector3.right) * visual.localRotation;
        }

        private void Complete(bool emergency)
        {
            phase = ManeuverPhase.Idle;
            phaseTime = 0f;
            readyAt = Time.time + (emergency ? emergencyCooldown : normalCooldown);
            selection.SetSelectionLocked(false);
        }

        // R resets height, cooldown, selection lock and the active maneuver.
        public void ResetManeuver()
        {
            phase = ManeuverPhase.Idle;
            phaseTime = 0f;
            readyAt = 0f;
            if (selection != null) selection.SetSelectionLocked(false);
        }

        private void OnDisable() { ResetManeuver(); }
    }
}
