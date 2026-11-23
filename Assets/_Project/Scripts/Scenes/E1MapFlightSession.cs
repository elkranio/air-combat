using UnityEngine;
using UnityEngine.InputSystem;

namespace AirCombat
{
    // E1 art-map-only flight composition. No combat systems, encounter or mission logic.
    // Keeps the original PlayerJet prefab unchanged and makes a large map flyable.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-80)]
    public sealed class E1MapFlightSession : MonoBehaviour
    {
        [SerializeField] private JetAssembly player;
        [SerializeField] private CombatCameraRig cameraRig;
        [SerializeField, Min(100f)] private float resetHalfExtent = 850f;

        private FlightController flight;
        private Afterburner boost;
        private BreakTurn breakTurn;
        private HeadingLockedObliqueCameraStudy studyCamera;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private GUIStyle label;
        private bool showLegend = true;

        public void Configure(JetAssembly jet, CombatCameraRig rig, float boundary)
        {
            player = jet;
            cameraRig = rig;
            resetHalfExtent = boundary;
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
            breakTurn = player.GetComponent<BreakTurn>();
            studyCamera = cameraRig.GetComponent<HeadingLockedObliqueCameraStudy>();
            if (flight == null || boost == null || breakTurn == null || studyCamera == null)
            {
                Debug.LogError("E1: flight, boost, break-turn or C3 camera missing.", this);
                enabled = false;
                return;
            }

            spawnPosition = player.transform.position;
            spawnRotation = player.transform.rotation;
            breakTurn.Configure(boost, flight.Visual);
            flight.ConfigureBreakTurn(breakTurn);
            cameraRig.BindPlayer(player.transform, boost, false);
            cameraRig.BindLockTarget(null, 1f, .5f);
        }

        private void Update()
        {
            if (!Application.isFocused) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.hKey.wasPressedThisFrame) showLegend = !showLegend;
            bool manuallyReset = keyboard != null && keyboard.rKey.wasPressedThisFrame;
            var position = player.transform.position;
            if (manuallyReset || Mathf.Abs(position.x) >= resetHalfExtent ||
                Mathf.Abs(position.z) >= resetHalfExtent)
                ResetFlight();
        }

        private void ResetFlight()
        {
            breakTurn.CancelBreak();
            player.transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            studyCamera.SnapToAircraft(); // No fast camera sweep across the 2 km map.
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
            GUI.Box(new Rect(12f, 12f, 390f, 88f), GUIContent.none);
            GUI.Label(new Rect(22f, 17f, 365f, 22f), "E1 / VIBRANT DESERT — FIRST FLIGHT", label);
            GUI.Label(new Rect(22f, 39f, 365f, 22f), "A/D turn  W boost  Q/E break  R reset  H hide", label);
            GUI.Label(new Rect(22f, 61f, 365f, 22f),
                $"Speed {flight.CurrentSpeed:F0}   Boost {boost.Charge01:P0}   Terrain 2000 x 2000", label);
        }
    }
}
