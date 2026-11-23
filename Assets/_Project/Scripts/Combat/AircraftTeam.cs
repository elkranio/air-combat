using UnityEngine;

namespace AirCombat
{
    public enum AircraftSide { Unassigned, Player, Hostile }

    [DisallowMultipleComponent]
    public sealed class AircraftTeam : MonoBehaviour
    {
        [SerializeField] private AircraftSide side;
        public AircraftSide Side => side;
        public void SetSide(AircraftSide value) { side = value; }
    }
}
