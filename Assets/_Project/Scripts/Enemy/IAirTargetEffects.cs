using UnityEngine;

namespace AirCombat
{
    // Presentation port shared by air targets; no dependency on a mission type.
    public interface IAirTargetEffects
    {
        void AirBurst(Vector3 position, Vector3 forward);
        void GroundBurst(Vector3 position, Vector3 forward);
        void EmitSmoke(Vector3 position, bool burning);
    }
}
