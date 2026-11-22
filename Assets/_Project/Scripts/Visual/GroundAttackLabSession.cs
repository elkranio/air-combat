using UnityEngine;
using UnityEngine.InputSystem;

namespace AirCombat
{
    // Scene-only composition test. WeaponLabSession is deliberately not involved.
    // Does not implement targets, ground ordnance, AI, loadouts or new flight rules.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-80)]
    public sealed class GroundAttackLabSession : MonoBehaviour
    {
        [SerializeField] private JetAssembly playerJet;
        [SerializeField] private CombatCameraRig cameraRig;

        private FlightController flight;
        private Afterburner afterburner;
        private BreakTurn breakTurn;
        private WeaponController weapons;
        private BoundedAim aim;
        private Vector3 startPosition;
        private Quaternion startRotation;
        private bool showDiagnostics = true;
        private GUIStyle label;

        // Editor authoring only sets scene object references. Prefabs own no scene links.
        public void Configure(JetAssembly jet, CombatCameraRig rig)
        {
            playerJet = jet;
            cameraRig = rig;
        }

        private void Awake()
        {
            if (playerJet == null || !playerJet.IsConfigured || cameraRig == null)
            {
                Debug.LogError("GroundAttackLab: missing player JetAssembly or camera rig.", this);
                enabled = false;
                return;
            }
            var jet = playerJet.gameObject;
            flight = jet.GetComponent<FlightController>();
            afterburner = jet.GetComponent<Afterburner>();
            breakTurn = jet.GetComponent<BreakTurn>();
            weapons = jet.GetComponent<WeaponController>();
            aim = jet.GetComponent<BoundedAim>();
            var selection = jet.GetComponent<WeaponSelection>();
            if (flight == null || afterburner == null || breakTurn == null ||
                weapons == null || aim == null || selection == null)
            {
                Debug.LogError("GroundAttackLab: PlayerJet prefab is missing required flight/weapon components.", this);
                enabled = false;
                return;
            }

            // All scene references come from the scene, never from the shared prefabs.
            cameraRig.BindPlayer(jet.transform, afterburner, false);
            cameraRig.BindLockTarget(null, 1f, .5f);
            aim.Configure(cameraRig.ViewCamera, jet.transform);
            weapons.ConfigureAim(aim);
            weapons.ConfigureSelection(selection);
            weapons.ConfigureMissileLock(cameraRig.Lock);
            breakTurn.Configure(afterburner, flight.Visual);
            flight.ConfigureBreakTurn(breakTurn);

            startPosition = jet.transform.position;
            startRotation = jet.transform.rotation;
        }

        private void Update()
        {
            if (playerJet == null || !Application.isFocused) return;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.hKey.wasPressedThisFrame) showDiagnostics = !showDiagnostics;
            if (keyboard.rKey.wasPressedThisFrame) ResetFlight();
            var position = playerJet.transform.position;
            if (Mathf.Abs(position.x) > 5200f || Mathf.Abs(position.z) > 5200f)
                ResetFlight();
        }

        private void ResetFlight()
        {
            breakTurn?.CancelBreak();
            playerJet.transform.SetPositionAndRotation(startPosition, startRotation);
            cameraRig.BindPlayer(playerJet.transform, afterburner, false);
            aim?.Configure(cameraRig.ViewCamera, playerJet.transform);
            cameraRig.Lock.ResetLock();
        }

        private void OnGUI()
        {
            if (!showDiagnostics || !enabled || Event.current.type != EventType.Repaint) return;
            if (label == null)
                label = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };
            GUI.Box(new Rect(12, 12, 510, 88), GUIContent.none);
            GUI.Label(new Rect(22, 17, 480, 22), "GROUND ATTACK LAB G1 / PREFAB FLIGHT ONLY", label);
            GUI.Label(new Rect(22, 40, 480, 22), "WASD fly  Space boost  Q/E break  1/2 guns  R reset  H HUD", label);
            GUI.Label(new Rect(22, 63, 480, 22),
                $"Speed {flight.CurrentSpeed:F0}   Fuel {afterburner.Charge01:P0}   Shots {weapons.FiredCount}   No targets / missiles yet", label);
        }
    }
}
