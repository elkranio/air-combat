using UnityEngine;

namespace AirCombat
{
    // Unit-facing flight availability and playable XZ rectangle.
    // No dependency on a particular mission, objective or phase enum.
    public interface IAirTargetFlightContext
    {
        bool FlightActive { get; }
        Rect OperationBounds { get; }
    }
}
