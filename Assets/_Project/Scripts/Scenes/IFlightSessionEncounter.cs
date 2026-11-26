using UnityEngine;

namespace AirCombat
{
    // Optional combat scenario attached to a shared flight session.
    // Mission controllers do not implement this sandbox lifecycle.
    public interface IFlightSessionEncounter
    {
        void Bind(JetAssembly aircraft, Camera camera, AircraftRadar sensor,
            PlayerHitPoints health, float halfExtent);
        void Restart();
        void SetAltitude(float altitude);
        string DebugStatus { get; }
    }
}
