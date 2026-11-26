using UnityEngine;

namespace AirCombat
{
    // Scene-level airborne radar source. Flight AI, HUD and the chassis never
    // reference one another; the mission registers this contact on its sensor.
    [DisallowMultipleComponent]
    public sealed class RadarAirContact : MonoBehaviour, IRadarAirContact
    {
        [SerializeField] private Transform headingSource;

        public Vector3 RadarPosition => transform.position;
        public Vector3 RadarForward => headingSource != null ? headingSource.forward : transform.forward;
        public float RadarThreatRadius => 0f;
        public RadarContactType RadarKind => RadarContactType.Aircraft;
        public bool IsRadarVisible => isActiveAndEnabled;
    }
}
