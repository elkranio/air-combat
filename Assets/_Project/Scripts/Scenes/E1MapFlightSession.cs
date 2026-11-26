using UnityEngine;
using UnityEngine.InputSystem;

namespace AirCombat
{
    // Shared player, camera and HUD composition. An optional scenario supplies
    // combat behavior through IFlightSessionEncounter, never a concrete lab type.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-80)]
    public sealed class E1MapFlightSession : MonoBehaviour
    {
        [SerializeField] private JetAssembly player;
        [SerializeField] private CombatCameraRig cameraRig;
        [SerializeField] private CombatHud combatHud;
        [SerializeField] private RadarAirContact[] airContacts;
        [SerializeField, Min(100f)] private float resetHalfExtent = 850f;
        [SerializeField] private bool altitudeComparison; // E1c only, disabled in other scenes.

        [Header("FLIGHT-02 / Variable maneuverability")]
        [SerializeField, Min(.1f)] private float tightTurnSpeed = 35f;
        [SerializeField, Range(.1f, 1f)] private float boostTurnMultiplier = .6f;
        [SerializeField, Min(1f)] private float tightTurnMultiplier = 2f;
        [SerializeField, Min(.1f)] private float maneuverResponse = 140f;

        private IFlightSessionEncounter encounter;
        private FlightController flight;
        private Afterburner boost;
        private ArcadeCameraFollow arcadeCamera;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private GUIStyle label;
        private bool showLegend = false;

        public void Configure(JetAssembly jet, CombatCameraRig rig, float boundary)
        {
            player = jet;
            cameraRig = rig;
            resetHalfExtent = boundary;
        }

        // Editor-only scene authoring enables the three-height visual test.
        // This never changes the shared FlightController or player prefab.
        public void EnableAltitudeComparison() => altitudeComparison = true;

        // Editor authoring links the scene to the reusable HUD and known aircraft.
        // No scene references are serialized into the CombatHUD prefab.
        public void ConfigureHud(CombatHud hud, RadarAirContact[] contacts)
        {
            combatHud = hud;
            airContacts = contacts;
        }

        private void Awake()
        {
            if (player == null || !player.IsConfigured || cameraRig == null)
            {
                Debug.LogError("E1: missing configured player or camera rig.", this);
                enabled = false;
                return;
            }
            flight = player.GetComponent<FlightController>();
            boost = player.GetComponent<Afterburner>();
            var legacyBreak = player.GetComponent<BreakTurn>();
            arcadeCamera = cameraRig.GetComponent<ArcadeCameraFollow>();
            if (flight == null || boost == null || arcadeCamera == null)
            {
                Debug.LogError("E1: flight, boost or camera missing.", this);
                enabled = false;
                return;
            }

            spawnPosition = player.transform.position;
            spawnRotation = player.transform.rotation;
            // Production flight is always WASD now. Leave the shared legacy
            // component available to the labs, but do not equip it on this map.
            if (legacyBreak != null) legacyBreak.enabled = false;
            flight.ConfigureBreakTurn(null);
            flight.ConfigureVariableManeuverability(tightTurnSpeed, boostTurnMultiplier,
                tightTurnMultiplier, maneuverResponse);
            cameraRig.BindPlayer(player.transform, boost, false);
            cameraRig.BindLockTarget(null, 1f, .5f);

            encounter = GetComponent<IFlightSessionEncounter>();
            if (combatHud != null || encounter != null)
            {
                var radar = player.GetComponent<AircraftRadar>();
                var health = player.GetComponent<PlayerHitPoints>();
                if (radar == null || health == null)
                {
                    Debug.LogError("E1: combat player prefab needs authored radar and health.", player);
                    enabled = false;
                    return;
                }
                // Parked art trials are not hostile contacts during a live encounter.
                if (encounter == null && airContacts != null)
                    foreach (var contact in airContacts) radar.Register(contact);
                health.ResetHealth();
                if (encounter != null)
                    encounter.Bind(player, cameraRig.ViewCamera, radar, health, resetHalfExtent);
                if (combatHud != null)
                    combatHud.Bind(cameraRig.ViewCamera, boost, health, radar,
                        player.GetComponent<PlayerWeaponTrigger>());
            }
        }

        private void Update()
        {
            if (MissionPause.IsTacticalMapPausedFor(gameObject.scene)) return;
            if (!Application.isFocused) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.hKey.wasPressedThisFrame) showLegend = !showLegend;
            if (altitudeComparison && keyboard != null)
            {
                // Direct A/B/C choices; no auto camera zoom, pitch or yaw changes.
                if (keyboard.digit7Key.wasPressedThisFrame) ChangeAltitude(78f);
                if (keyboard.digit8Key.wasPressedThisFrame) ChangeAltitude(110f);
                if (keyboard.digit9Key.wasPressedThisFrame) ChangeAltitude(150f);
            }
            // Standalone sandbox/labs retain their own retry input.
            // In a campaign scene the mission invokes the same reset explicitly.
            if (!MissionRetry.IsOwnedFor(gameObject.scene) &&
                keyboard != null && keyboard.rKey.wasPressedThisFrame)
                ResetFlight();
        }

        private void ChangeAltitude(float altitude)
        {
            Vector3 p = player.transform.position;
            p.y = altitude;
            player.transform.position = p;
            // Manual reset should retain whichever height is being tested.
            spawnPosition.y = altitude;
            encounter?.SetAltitude(altitude);
            SnapCamera(); // Prevent a large vertical camera sweep.
        }

        internal bool Controls(FlightController controller) => flight == controller;

        internal void ResetForMissionRetry()
        {
            // The campaign owns its objectives, weapons and retry lifecycle.
            if (isActiveAndEnabled) ResetPlayerPose();
        }

        private void ResetFlight()
        {
            ResetPlayerPose();
            encounter?.Restart();
        }

        private void ResetPlayerPose()
        {
            player.transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            SnapCamera(); // No fast camera sweep across the map.
        }

        private void SnapCamera()
        {
            // Cleanup A made ArcadeCameraFollow the sole driver of the main map.
            if (arcadeCamera != null) arcadeCamera.SnapToAircraft();
        }

        private void OnGUI()
        {
            if (!showLegend || Event.current.type != EventType.Repaint || flight == null) return;
            if (label == null)
                label = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    normal = { textColor = Color.white }
                };
            GUI.Box(new Rect(12f, 12f, 390f, (altitudeComparison ? 108f : 88f) + (encounter != null ? 22f : 0f)), GUIContent.none);
            GUI.Label(new Rect(22f, 17f, 365f, 22f),
                altitudeComparison ? "B1a / BARRACKS APPROACH" : "E1 / MAP — FIRST FLIGHT", label);
            GUI.Label(new Rect(22f, 39f, 365f, 22f),
                "A/D turn  W boost  S tight  R reset  H hide", label);
            GUI.Label(new Rect(22f, 61f, 365f, 22f),
                $"Speed {flight.CurrentSpeed:F0}   Boost {boost.Charge01:P0}   Terrain 2000 x 2000", label);
            if (altitudeComparison)
                GUI.Label(new Rect(22f, 83f, 365f, 22f),
                    $"7 / 8 / 9 : Y78 / Y110 / Y150     Current Y {player.transform.position.y:F0}", label);
            if (encounter != null)
                GUI.Label(new Rect(22f, altitudeComparison ? 105f : 83f, 365f, 22f),
                    encounter.DebugStatus, label);
        }
    }
}
