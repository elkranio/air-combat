using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AirCombat
{
    // ArtLab only: the pipeline override is temporary and never edits the shared asset.
    [DisallowMultipleComponent]
    public sealed class ArtLabDirectionalLightingPreview : MonoBehaviour
    {
        [SerializeField] private UniversalRenderPipelineAsset studyPipeline;
        [SerializeField] private Light sun;
        [SerializeField] private GameObject[] paintedShadows;
        [SerializeField] private MeshRenderer[] groundRenderers;
        [SerializeField] private Material[] oldGround, newGround;
        [SerializeField, Range(0, 2)] private int mode = 2;
        [SerializeField] private bool textured = true;
        [SerializeField] private bool terrainStudy;
        private RenderPipelineAsset previousPipeline;
        private bool ownsOverride;
        private bool showLegend = true;
        private GUIStyle label;

        public void Configure(UniversalRenderPipelineAsset pipeline, Light light, GameObject[] blobs,
            MeshRenderer[] ground, Material[] original, Material[] current)
        {
            studyPipeline = pipeline; sun = light; paintedShadows = blobs;
            groundRenderers = ground; oldGround = original; newGround = current;
            mode = 2; textured = true;
            Apply(); ApplyGround();
        }

        public void ConfigureTerrainStudy()
        {
            terrainStudy = true;
            mode = 2;
            Apply();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (studyPipeline == null || sun == null)
            {
                Debug.LogError("ArtLab lighting study is not configured.", this);
                enabled = false;
                return;
            }
            previousPipeline = QualitySettings.renderPipeline;
            QualitySettings.renderPipeline = studyPipeline;
            ownsOverride = true;
            Apply(); ApplyGround();
        }

        private void OnDisable()
        {
            if (!ownsOverride) return;
            // Do not clobber a pipeline override installed by another system.
            if (QualitySettings.renderPipeline == studyPipeline)
                QualitySettings.renderPipeline = previousPipeline;
            ownsOverride = false;
        }

        private void Update()
        {
            var keys = Keyboard.current;
            if (keys == null || !Application.isFocused) return;
            if (keys.digit1Key.wasPressedThisFrame) SetMode(0);
            else if (keys.digit2Key.wasPressedThisFrame) SetMode(1);
            else if (keys.digit3Key.wasPressedThisFrame) SetMode(2);
            else if (keys.lKey.wasPressedThisFrame) SetMode((mode + 1) % 3);
            if (keys.gKey.wasPressedThisFrame) { textured = !textured; ApplyGround(); }
            if (keys.hKey.wasPressedThisFrame) showLegend = !showLegend;
        }

        private void SetMode(int next) { mode = terrainStudy && next == 0 ? 1 : next; Apply(); }

        private void Apply()
        {
            if (sun == null) return;
            bool real = mode != 0, contrast = mode == 2;
            sun.transform.rotation = Quaternion.Euler(contrast ? 38f : 48f, -38f, 0);
            sun.color = contrast ? new Color32(255, 225, 184, 255) : new Color32(255, 231, 186, 255);
            sun.intensity = contrast ? 1.38f : 1.22f;
            sun.shadows = real ? LightShadows.Soft : LightShadows.None;
            sun.shadowStrength = contrast ? (terrainStudy ? .75f : .88f) : .78f;
            sun.shadowBias = .035f;
            sun.shadowNormalBias = .18f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = contrast ? new Color32(72, 93, 121, 255) : new Color32(104, 132, 158, 255);
            RenderSettings.sun = sun;
            if (paintedShadows != null)
                foreach (var blob in paintedShadows) if (blob != null) blob.SetActive(!real);
        }

        private void ApplyGround()
        {
            var mats = textured ? newGround : oldGround;
            if (groundRenderers == null || mats == null || mats.Length != groundRenderers.Length) return;
            for (int i = 0; i < mats.Length; i++)
                if (groundRenderers[i] != null && mats[i] != null) groundRenderers[i].sharedMaterial = mats[i];
        }

        private void OnGUI()
        {
            if (!showLegend || Event.current.type != EventType.Repaint) return;
            if (label == null) label = new GUIStyle(GUI.skin.label) {fontSize = 14, normal = {textColor = Color.white}};
            GUI.Box(new Rect(12, 12, 385, 100), GUIContent.none);
            GUI.Label(new Rect(22, 16, 360, 22), terrainStudy ? "ArtLab v9 / Terrain relief" : "ArtLab v8 / Directional lighting", label);
            GUI.Label(new Rect(22, 38, 360, 22), terrainStudy ? "2: Real shadows    3: Contrast sun" : "1: v5    2: Real shadows    3: Contrast sun", label);
            GUI.Label(new Rect(22, 60, 360, 22), "L: " + (mode == 0 ? "Original v5" : mode == 1 ? "Real shadows only" : "Contrast sun / 38 deg"), label);
            GUI.Label(new Rect(22, 82, 360, 22), "G: " + (textured ? "Anti-tiling" : "Tiled ground") + "    H: Hide legend", label);
        }
    }
}
