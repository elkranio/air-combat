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
            // Only oblique cameras need camera-relative radar presentation.
            // Legacy WeaponLab/GroundAttackLab stay north-up, unchanged.
            bool oblique = camera != null &&
                camera.GetComponent<ArcadeCameraFollow>() != null;
            radarDisplay.Bind(sensor, oblique ? camera : null);
        }
    }
}
