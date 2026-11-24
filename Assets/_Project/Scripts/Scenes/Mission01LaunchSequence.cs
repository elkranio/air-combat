using UnityEngine;
using UnityEngine.InputSystem;

namespace AirCombat
{
    // Mission-specific launch state. Normal flight remains owned by FlightController.
    [DisallowMultipleComponent]
    public sealed class Mission01LaunchSequence : MonoBehaviour
    {
        public enum Phase { Intro, Ready, Takeoff, Flight }

        [SerializeField] private FlightController flight;
        [SerializeField] private PlayerWeaponTrigger weaponTrigger;
        [SerializeField] private bool skipIntro = true;

        public Phase State { get; private set; } = Phase.Intro;

#if UNITY_EDITOR
        public void ConfigureForAuthoring(FlightController controller, PlayerWeaponTrigger trigger)
        {
            flight = controller;
            weaponTrigger = trigger;
        }
#endif

        private void Awake()
        {
            if (flight == null || weaponTrigger == null)
            {
                Debug.LogError("Mission01 launch: missing player references.", this);
                enabled = false;
                return;
            }
            flight.enabled = false;
            weaponTrigger.enabled = false;
            State = Phase.Intro;
        }

        private void Start()
        {
            if (skipIntro) CompleteIntro();
        }

        // A future intro/cutscene can call this when it finishes.
        public void CompleteIntro()
        {
            if (State == Phase.Intro) State = Phase.Ready;
        }

        private void Update()
        {
            if (!Application.isFocused) return;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (State == Phase.Takeoff && keyboard.rKey.wasPressedThisFrame)
            {
                State = Phase.Ready;
                return;
            }
            if (State == Phase.Ready && keyboard.wKey.wasPressedThisFrame)
            {
                // Atom E intentionally stops here. Atom F implements motion and handover.
                State = Phase.Takeoff;
                Debug.Log("Mission01: takeoff requested.", this);
            }
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || State == Phase.Flight) return;
            string message = State == Phase.Intro ? "MISSION INTRO" :
                State == Phase.Ready ? "PRESS W TO TAKE OFF" : "TAKEOFF REQUESTED";
            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * .025f, 16f, 32f)),
                fontStyle = FontStyle.Bold
            };
            var rect = new Rect(0f, Screen.height * .84f, Screen.width, 48f);
            Color previous = GUI.color;
            GUI.color = Color.white;
            style.normal.textColor = new Color(.05f, .07f, .09f, .9f);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), message, style);
            style.normal.textColor = Color.white;
            GUI.Label(rect, message, style);
            GUI.color = previous;
        }
    }
}
