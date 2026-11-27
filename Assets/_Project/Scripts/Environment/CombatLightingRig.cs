using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AirCombat
{
    // One lighting/shadow authority per combat scene. Geometry casts and receives
    // URP shadow maps; aircraft and bombs need no projected-shadow components.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    [DefaultExecutionOrder(-900)]
    public sealed class CombatLightingRig : MonoBehaviour
    {
        public enum LightingTime { Day, Sunset, NearNight }

        [SerializeField] private UniversalRenderPipelineAsset pipeline;
        [SerializeField] private LightingTime timeOfDay = LightingTime.Day;
        [SerializeField, Range(-180f, 180f)] private float azimuth = 52f;
        [SerializeField, Range(-10f, 10f)] private float elevationOffset = -10f;
        [SerializeField, Range(.1f, 2f)] private float ambientMultiplier = 1f;

        private Light sun;
        private RenderPipelineAsset previousPipeline;
        private Light previousSun;
        private AmbientMode previousAmbientMode;
        private Color previousAmbientLight;
        private bool ownsPipeline;
        private bool ownsEnvironment;
        private LightingTime lastTime;
        private float lastAzimuth, lastElevationOffset, lastAmbientMultiplier;
        private bool applied;

        public LightingTime TimeOfDay => timeOfDay;
        // Future emissive fixtures may read this to increase their radiance at dusk.
        // URP Lit emission maps are NOT modified globally by this value.
        public float SuggestedEmissionMultiplier => timeOfDay == LightingTime.Day ? 1f
            : timeOfDay == LightingTime.Sunset ? 1.5f : 2.5f;

#if UNITY_EDITOR
        public void ConfigureAuthoring(UniversalRenderPipelineAsset asset)
        {
            pipeline = asset;
            sun = GetComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .75f;
            sun.shadowBias = .035f;
            sun.shadowNormalBias = .18f;
            sun.shadowNearPlane = .2f;
            ApplyLighting(false); // Prefab appearance only; do not touch an open scene.
        }
#endif

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (pipeline == null)
            {
                Debug.LogError("CombatLightingRig: missing shared URP pipeline.", this);
                enabled = false;
                return;
            }
            previousPipeline = QualitySettings.renderPipeline;
            previousSun = RenderSettings.sun;
            previousAmbientMode = RenderSettings.ambientMode;
            previousAmbientLight = RenderSettings.ambientLight;
            ownsEnvironment = true;
            QualitySettings.renderPipeline = pipeline;
            ownsPipeline = true;
            sun = GetComponent<Light>();
            applied = false;
            ApplyLighting();
        }

        private void Update()
        {
            // Inspector changes and future time-of-day transitions take effect live.
            if (Application.isPlaying && (!applied || lastTime != timeOfDay ||
                !Mathf.Approximately(lastAzimuth, azimuth) ||
                !Mathf.Approximately(lastElevationOffset, elevationOffset) ||
                !Mathf.Approximately(lastAmbientMultiplier, ambientMultiplier)))
                ApplyLighting();
        }

        private void ApplyLighting(bool applyEnvironment = true)
        {
            if (sun == null) sun = GetComponent<Light>();
            float elevation;
            Color ambient;
            switch (timeOfDay)
            {
                default:
                case LightingTime.Day:
                    elevation = 60f;
                    sun.intensity = 1.38f;
                    sun.color = new Color(1f, .937f, .859f);
                    ambient = new Color(.55f, .52f, .47f);
                    break;
                case LightingTime.Sunset:
                    elevation = 14f;
                    sun.intensity = .76f;
                    sun.color = new Color(1f, .67f, .43f);
                    ambient = new Color(.33f, .34f, .39f);
                    break;
                case LightingTime.NearNight:
                    // Sun is still slightly above the horizon. Avoid a below-ground
                    // directional light projecting inverted-looking shadows.
                    elevation = 5f;
                    sun.intensity = .16f;
                    sun.color = new Color(.72f, .79f, 1f);
                    ambient = new Color(.16f, .18f, .25f);
                    break;
            }
            transform.rotation = Quaternion.Euler(elevation + elevationOffset, azimuth, 0f);
            if (applyEnvironment)
            {
                RenderSettings.sun = sun;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = ambient * ambientMultiplier;
            }
            lastTime = timeOfDay;
            lastAzimuth = azimuth;
            lastElevationOffset = elevationOffset;
            lastAmbientMultiplier = ambientMultiplier;
            applied = true;
        }

        private void OnDisable()
        {
            // Do not leave editor/next-scene ambient state behind after a lab test.
            // Only restore values while this rig still owns the scene sun.
            if (ownsEnvironment && RenderSettings.sun == sun)
            {
                RenderSettings.sun = previousSun;
                RenderSettings.ambientMode = previousAmbientMode;
                RenderSettings.ambientLight = previousAmbientLight;
            }
            ownsEnvironment = false;
            if (ownsPipeline && QualitySettings.renderPipeline == pipeline)
                QualitySettings.renderPipeline = previousPipeline;
            ownsPipeline = false;
        }
    }
}
