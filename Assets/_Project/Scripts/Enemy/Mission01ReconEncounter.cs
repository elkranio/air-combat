using UnityEngine;

namespace AirCombat
{
    // Composition, radar and objective progress. Forward the existing serialized FX port.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-30)]
    public sealed class Mission01ReconEncounter : MonoBehaviour, IAirTargetEffects, IPrimaryObjectiveSource
    {
        [SerializeField] private Mission01LaunchSequence launch;
        [SerializeField] private FlightController player;
        [SerializeField] private WatcherTarget[] targets;
        [SerializeField] private float groundY;
        private AircraftRadar radar;
        private AirTargetCombatEffects airTargetEffects;
        private bool ready;
        public bool InFlight => ready && launch != null && launch.isActiveAndEnabled && launch.State == Mission01LaunchSequence.Phase.Flight;
        private DestroyTargetsObjective primaryObjective;
        public ObjectiveProgress PrimaryProgress => primaryObjective != null ? primaryObjective.Read() : default;
        public int Eliminated => PrimaryProgress.Destroyed;
#if UNITY_EDITOR
        public void ConfigureForAuthoring(Mission01LaunchSequence sequence, FlightController flight, WatcherTarget[] units, float floor)
        { launch = sequence; player = flight; targets = units; groundY = floor; }
#endif
        private void Start()
        {
            if (launch == null || player == null || targets == null || targets.Length != 3)
            { Debug.LogError("RE01: missing mission references.", this); enabled = false; return; }
            radar = player.GetComponent<AircraftRadar>();
            foreach (var target in targets)
            {
                if (target == null || !target.Initialize(CanRunTargets, this, player.CruiseSpeed * .23f, groundY))
                { Debug.LogError("RE01: invalid target authoring.", this); enabled = false; return; }
                radar?.Register(target);
            }
            var rig = FindAnyObjectByType<ArcadeCameraFollow>();
            airTargetEffects = new AirTargetCombatEffects(transform, player.transform,
                rig != null ? rig.GetComponent<Camera>() : null);
            primaryObjective = new DestroyTargetsObjective(targets);
            ready = true;
        }

        private bool CanRunTargets() => this != null && isActiveAndEnabled && InFlight;

        private void Update()
        {
            if (!ready || MissionPause.IsTacticalMapPausedFor(gameObject.scene)) return;
            if (MissionRetry.WasRequestedThisFrame(gameObject.scene))
            {
                // The mission has already reset the player and SAMs this frame.
                foreach (var round in FindObjectsByType<BallisticProjectile>())
                    if (round.gameObject.scene == gameObject.scene && round.IsOwnedBy(player.transform))
                    { round.enabled = false; Destroy(round.gameObject); }
                airTargetEffects?.Reset();
            }
        }

        // Keep the existing scene reference for Overlord and Watcher bindings.
        // The actual particle/audio resources and emission policy live in the service.
        public void AirBurst(Vector3 p, Vector3 forward) => airTargetEffects?.AirBurst(p, forward);
        public void GroundBurst(Vector3 p, Vector3 forward) => airTargetEffects?.GroundBurst(p, forward);
        public void EmitSmoke(Vector3 p, bool burning) => airTargetEffects?.EmitSmoke(p, burning);

        private void OnDestroy()
        {
            if (radar != null && targets != null) foreach (var t in targets) if (t != null) radar.Unregister(t);
            airTargetEffects?.Dispose();
            airTargetEffects = null;
        }
    }
}
