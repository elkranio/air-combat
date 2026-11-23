using UnityEngine;

namespace AirCombat
{
    // Prefab-level UI composition. No scene links are serialized in the prefab;
    // each scene supplies its own camera, aircraft instruments and radar sensor.
    [DisallowMultipleComponent]
    public sealed class CombatHud : MonoBehaviour
    {
        [SerializeField] private FlightHud statusBar;
        [SerializeField] private RadarHud radarDisplay;

        public void ConfigureParts(FlightHud bar, RadarHud radar)
        {
            statusBar = bar;
            radarDisplay = radar;
        }

        public void Bind(Camera camera, Afterburner boost, PlayerHitPoints hitPoints,
            AircraftRadar sensor)
        {
            if (statusBar == null || radarDisplay == null)
            {
                Debug.LogError("Combat HUD prefab has missing bar or radar components.", this);
                return;
            }
            statusBar.Bind(camera, boost, hitPoints);
            // C2 uses the study camera axes for both the jet and world contacts.
            // Existing combat labs have no study component: radar stays north-up.
            radarDisplay.Bind(sensor, camera != null
                ? camera.GetComponent<HeadingLockedObliqueCameraStudy>() : null);
        }
    }
}
