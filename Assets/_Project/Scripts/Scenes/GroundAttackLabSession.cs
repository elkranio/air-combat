using UnityEngine;
using UnityEngine.InputSystem;

namespace AirCombat
{
    // Scene-only composition test. WeaponLabSession is deliberately not involved.
    // Equips G3 bombs, G4b NURS, and depot hit zones without mutating prefab assets.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-80)]
    public sealed class GroundAttackLabSession : MonoBehaviour
    {
        [SerializeField] private JetAssembly playerJet;
        [SerializeField] private CombatCameraRig cameraRig;
        [SerializeField] private AntiAircraftTurret antiAircraft; // Optional scene instance (AA1).

        private FlightController flight;
        private Afterburner afterburner;
        private BreakTurn breakTurn;
        private WeaponController weapons;
        private BoundedAim aim;
        private BombRack bombs;
        private CabrageManeuver cabrage;
        private NursRocketRack nurs;
        private NursCameraCue nursCamera;
        private GroundDepotTarget[] depots;
        private PlayerHitPoints playerHealth;
        private Vector3 startPosition;
        private Quaternion startRotation;
        private bool showDiagnostics;
        private GUIStyle label;

        // Editor authoring only sets scene object references. Prefabs own no scene links.
        public void Configure(JetAssembly jet, CombatCameraRig rig)
        {
            playerJet = jet;
            cameraRig = rig;
        }

        // Called only by the one-time AA1 scene installer. The turret prefab
        // never serializes a link back to a scene-specific aircraft.
        public void ConfigureAntiAircraft(AntiAircraftTurret turret)
        {
            antiAircraft = turret;
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
            selection.EquipBombs();
            selection.EquipNurs();
            bombs = jet.GetComponent<BombRack>();
            if (bombs == null) bombs = jet.AddComponent<BombRack>();
            bombs.Configure(selection, flight, afterburner, breakTurn);
            cabrage = jet.GetComponent<CabrageManeuver>();
            if (cabrage == null) cabrage = jet.AddComponent<CabrageManeuver>();
            cabrage.Configure(selection, breakTurn, flight.Visual);
            flight.ConfigureCabrage(cabrage);
            nurs = jet.GetComponent<NursRocketRack>();
            if (nurs == null) nurs = jet.AddComponent<NursRocketRack>();
            nurs.Configure(cabrage, flight);
            // G4c keeps aircraft-safe camera lead, without an impact predictor.
            nursCamera = cameraRig.GetComponent<NursCameraCue>();
            if (nursCamera == null) nursCamera = cameraRig.gameObject.AddComponent<NursCameraCue>();
            nursCamera.Configure(cameraRig.ViewCamera, jet.transform, selection, cabrage);
            // Height reference only while bombs are equipped: no lead/prediction.
            var nadir = jet.GetComponent<BombNadirMarker>();
            if (nadir == null) nadir = jet.AddComponent<BombNadirMarker>();
            nadir.Configure(selection, cameraRig.ViewCamera);

            startPosition = jet.transform.position;
            startRotation = jet.transform.rotation;

            // GroundAttackLab-only incoming damage. Shared PlayerJet prefab,
            // WeaponLab and Sandbox receive no AA hitbox or health component.
            if (antiAircraft != null)
            {
                playerHealth = jet.GetComponent<PlayerHitPoints>();
                if (playerHealth == null) playerHealth = jet.AddComponent<PlayerHitPoints>();
                playerHealth.InstallHitbox();
                antiAircraft.BindTarget(jet.transform, playerHealth);
            }

            // R1: radar belongs to this aircraft, with scene-registered contacts.
            // The UI has no knowledge of turrets, range balance or scene prefabs.
            var radar = jet.GetComponent<AircraftRadar>();
            if (radar == null) radar = jet.AddComponent<AircraftRadar>();
            if (antiAircraft != null) radar.Register(antiAircraft);
            RadarHud.Create(transform, radar);
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
            cabrage?.ResetManeuver();
            breakTurn?.CancelBreak();
            nursCamera?.ResetCue();
            playerJet.transform.SetPositionAndRotation(startPosition, startRotation);
            cameraRig.BindPlayer(playerJet.transform, afterburner, false);
            aim?.Configure(cameraRig.ViewCamera, playerJet.transform);
            cameraRig.Lock.ResetLock();
            playerHealth?.ResetHealth();
            antiAircraft?.ResetEncounter();
        }

        private void OnGUI()
        {
            if (!showDiagnostics || !enabled || Event.current.type != EventType.Repaint) return;
            if (label == null)
                label = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };
            GUI.Box(new Rect(12, 12, 650, antiAircraft != null ? 164 : 140), GUIContent.none);
            GUI.Label(new Rect(22, 17, 620, 22), "GROUND ATTACK LAB G4b / BOMBS + CABRAGE NURS", label);
            GUI.Label(new Rect(22, 40, 620, 22), "A/D turn  W boost  Q/E break/abort  1/2/3/4/5 weapons  R reset  H HUD", label);
            GUI.Label(new Rect(22, 63, 620, 22),
                $"Speed {flight.CurrentSpeed:F0}   Fuel {afterburner.Charge01:P0}   Bombs {bombs.FiredCount}   Pending {bombs.PendingCount}   CD {bombs.CooldownLeft:F1}s", label);
            GUI.Label(new Rect(22, 86, 620, 22),
                $"NURS: {cabrage.PhaseLabel}   Alt {playerJet.transform.position.y:F1}   CD {cabrage.CooldownLeft:F1}s   Fired {nurs.FiredCount}   Impacts {nurs.ImpactCount}   PartHits {nurs.DamagedPartHits}", label);
            int destroyed = 0;
            if (depots != null) foreach (var depot in depots) destroyed += depot.DestroyedParts;
            GUI.Label(new Rect(22, 109, 620, 22),
                $"Destroyed depot parts {destroyed}/16   |   Hold course for precision; W adds scatter", label);
            if (antiAircraft != null && playerHealth != null)
                GUI.Label(new Rect(22, 132, 620, 22),
                    $"AA1: Player {playerHealth.Hp}/{playerHealth.MaxHp} HP   Turret {antiAircraft.Hp}/{antiAircraft.MaxHp} HP   Shots {antiAircraft.FiredCount}   Hits {antiAircraft.HitCount}", label);
        }
    }
}
