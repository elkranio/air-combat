using UnityEngine;

namespace AirCombat
{
    // Prefab-owned combat composition. The encounter supplies the target and
    // schedules the two pilot ticks around aircraft movement, as before.
    [DisallowMultipleComponent]
    public sealed class ArcadeFighterUnit : MonoBehaviour
    {
        [SerializeField] private ArcadeFighterPilot.Settings attack = new ArcadeFighterPilot.Settings();

        private CombatAircraftFlight flight;
        private Afterburner boost;
        private AutocannonWeapon gun;
        private ArcadeFighterPilot pilot;
        public PlayerHitPoints Health { get; private set; }
        public RadarAirContact Contact { get; private set; }
        public float CurrentSpeed => flight != null ? flight.CurrentSpeed : 0f;
        public string DebugStatus => pilot == null ? "UNBOUND" :
            $"{pilot.State}/{pilot.FlightMode} AC {pilot.HitCount}/{pilot.FiredCount} B {pilot.BurstsThisPass}/{pilot.BurstLimit}";

        // Prepare before the encounter registers the unit with its services.
        public bool Prepare()
        {
            flight = GetComponent<CombatAircraftFlight>();
            Health = GetComponent<PlayerHitPoints>();
            Contact = GetComponent<RadarAirContact>();
            boost = GetComponent<Afterburner>();
            gun = GetComponentInChildren<AutocannonWeapon>();
            if (flight == null || Health == null || Contact == null || boost == null ||
                gun == null || !gun.isActiveAndEnabled || gun.Preset == null)
                return false;

            flight.EnableVariableManeuverability();
            Health.InstallHitbox();
            return true;
        }

        // Call after Prepare and service registration. Each unit owns an
        // independent variant; do not mutate the serialized prefab settings.
        public void BindPilot(Transform target, FlightController targetFlight,
            float boundary, bool later, float fallbackSeparationSide)
        {
            pilot = new ArcadeFighterPilot(flight, boost, gun, target, targetFlight,
                boundary, (attack ?? new ArcadeFighterPilot.Settings()).CreateVariant(later),
                fallbackSeparationSide);
        }

        public void TickFlight(float now, Transform wingman) => pilot?.TickFlight(now, wingman);
        public void TickFire(float now) => pilot?.TickFire(now);

        public void Stop(float now)
        {
            pilot?.Stop(now);
            pilot = null;
        }
    }
}
