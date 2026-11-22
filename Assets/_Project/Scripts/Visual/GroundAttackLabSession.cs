using UnityEngine;
using UnityEngine.InputSystem;

namespace AirCombat
{
    // Scene-only composition test. WeaponLabSession is deliberately not involved.
    // Equips the G3 bomb prototype and depot hit zones without mutating prefab assets.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-80)]
    public sealed class GroundAttackLabSession : MonoBehaviour
    {
        [SerializeField] private JetAssembly playerJet;
        [SerializeField] private CombatCameraRig cameraRig;
        // Hard asset reference: do not rely on Shader.Find for an unreferenced HUD shader.
        [SerializeField] private Material shadowSourceMaterial;

        private FlightController flight;
        private Afterburner afterburner;
        private BreakTurn breakTurn;
        private WeaponController weapons;
        private BoundedAim aim;
        private BombRack bombs;
        private Material groundShadowMaterial;
        private GroundDepotTarget[] depots;
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

            // G3 is an isolated GroundAttackLab experiment. Keep the approved
            // shared PlayerJet prefab and other labs' input/loadouts untouched.
            var depotGroup = GameObject.Find("Military Depots - G2");
            if (depotGroup == null || depotGroup.transform.childCount != 4)
            {
                Debug.LogError("G3 requires the four G2 Military Depot scene instances.", this);
                enabled = false;
                return;
            }
            depots = new GroundDepotTarget[depotGroup.transform.childCount];
            for (int i = 0; i < depots.Length; i++)
            {
                var depot = depotGroup.transform.GetChild(i).gameObject;
                var rig = depot.GetComponent<GroundDepotTarget>();
                if (rig == null) rig = depot.AddComponent<GroundDepotTarget>();
                rig.Install();
                depots[i] = rig;
            }
            // Reuse the proven vertex-color shadow mesh/shader, explicitly linked
            // by this scene. An unreferenced Shader.Find may be stripped from builds.
            if (shadowSourceMaterial != null && shadowSourceMaterial.shader != null)
            {
                groundShadowMaterial = new Material(shadowSourceMaterial)
                    { name = "G3c soft ground shadow (runtime)" };
                var aircraftShadow = jet.GetComponent<AircraftGroundShadow>();
                if (aircraftShadow == null) aircraftShadow = jet.AddComponent<AircraftGroundShadow>();
                // At altitude 48, offset ~20m and an enlarged, legible soft shadow.
                // Keep separation for height perception without losing the association.
                aircraftShadow.Configure(groundShadowMaterial, 10.5f, 15f,
                    .50f, .28f, 85f, .12f, new Vector2(-.24f, .35f), .025f);
            }
            else Debug.LogError("G3c: GroundAttackLab has no shadow material assigned.", this);

            selection.EquipBombs();
            bombs = jet.GetComponent<BombRack>();
            if (bombs == null) bombs = jet.AddComponent<BombRack>();
            bombs.Configure(selection, flight, afterburner, breakTurn, groundShadowMaterial);

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
            bombs?.CancelBurst();
            breakTurn?.CancelBreak();
            playerJet.transform.SetPositionAndRotation(startPosition, startRotation);
            cameraRig.BindPlayer(playerJet.transform, afterburner, false);
            aim?.Configure(cameraRig.ViewCamera, playerJet.transform);
            cameraRig.Lock.ResetLock();
        }

        private void OnDestroy()
        {
            if (groundShadowMaterial != null) Destroy(groundShadowMaterial);
        }

        private void OnGUI()
        {
            if (!showDiagnostics || !enabled || Event.current.type != EventType.Repaint) return;
            if (label == null)
                label = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };
            GUI.Box(new Rect(12, 12, 560, 112), GUIContent.none);
            GUI.Label(new Rect(22, 17, 530, 22), "GROUND ATTACK LAB G3 / GRAVITY BOMBS", label);
            GUI.Label(new Rect(22, 40, 530, 22), "A/D turn  W boost  Q/E break  1/2/3/4 weapons  R reset  H HUD", label);
            GUI.Label(new Rect(22, 63, 530, 22),
                $"Speed {flight.CurrentSpeed:F0}   Fuel {afterburner.Charge01:P0}   Bombs {bombs.FiredCount}   Pending {bombs.PendingCount}   CD {bombs.CooldownLeft:F1}s", label);
            int destroyed = 0;
            if (depots != null) foreach (var depot in depots) destroyed += depot.DestroyedParts;
            GUI.Label(new Rect(22, 86, 530, 22),
                $"Destroyed depot parts {destroyed}/16   |   Hold course for precision; W adds scatter", label);
        }
    }
}
