using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AirCombat
{
    // ArtLab-only scene glue. Flight and camera controllers remain authoritative.
    // E1a uses this scene wrapper for quick ground-language comparisons in the
    // accepted C3 camera, without regenerating scenes or importing new art.
    // T cycles original / flat / macro / fine ground presentation at runtime.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public sealed class ArtLabLandscapeFlight : MonoBehaviour
    {
        [SerializeField] private UniversalRenderPipelineAsset studyPipeline;
        [SerializeField] private Transform player;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private FlightController flight;
        [SerializeField] private Afterburner boost;
        [SerializeField] private float halfMap = 600f;
        [SerializeField] private bool resetOutsideBounds = true;
        [SerializeField] private string studyTitle = "ArtLab v10 / Landscape flight";
        [SerializeField] private string cameraHelp;
        [SerializeField] private string controlsHelp;
        private RenderPipelineAsset previousPipeline;
        private bool ownsOverride;

        private Vector3 startPosition;
        private Quaternion startRotation;
        private bool showLegend = true;
        private GUIStyle label;

        private readonly List<GroundStyleBinding> groundBindings = new List<GroundStyleBinding>(8);
        private Texture2D macroGroundTexture;
        private Texture2D fineGroundTexture;
        private GroundStyleMode groundStyle = GroundStyleMode.Original;

        private enum GroundStyleMode { Original, Flat, Macro, Fine }

        private sealed class GroundStyleBinding
        {
            public Renderer Renderer;
            public Material[] Original;
            public Material[] Flat;
            public Material[] Macro;
            public Material[] Fine;
        }

        public void Configure(UniversalRenderPipelineAsset pipeline, Transform aircraft, Camera camera,
            FlightController controller, Afterburner afterburner, float halfExtent, string title = "ArtLab v10 / Landscape flight")
        {
            studyPipeline = pipeline;
            player = aircraft;
            viewCamera = camera;
            flight = controller;
            boost = afterburner;
            halfMap = halfExtent;
            studyTitle = title;
        }

        public void SetStudyLegend(string title, string cameraLine)
        {
            studyTitle = title;
            cameraHelp = string.IsNullOrEmpty(cameraLine)
                ? "T: ground original/flat/macro/fine"
                : cameraLine + "   T: ground";
        }

        public void SetControlsLegend(string controls) => controlsHelp = controls;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (studyPipeline == null || player == null || viewCamera == null)
            { Debug.LogError("Incomplete visual flight study configuration.", this); enabled = false; return; }
            startPosition = player.position;
            startRotation = player.rotation;
            previousPipeline = QualitySettings.renderPipeline;
            QualitySettings.renderPipeline = studyPipeline;
            SetupGroundStyleTest();
            ApplyGroundStyle();
            ownsOverride = true;
        }

        private void OnDisable()
        {
            if (!ownsOverride) return;
            if (QualitySettings.renderPipeline == studyPipeline) QualitySettings.renderPipeline = previousPipeline;
            DisposeGroundStyleTest();
            ownsOverride = false;
        }

        private void Update()
        {
            var keys = Keyboard.current;
            bool reset = false;
            if (keys != null && Application.isFocused)
            {
                if (keys.hKey.wasPressedThisFrame) showLegend = !showLegend;
                reset = keys.rKey.wasPressedThisFrame;
                if (keys.tKey.wasPressedThisFrame) CycleGroundStyle();
            }
            // Leave enough terrain outside the reachable area for the moving viewport.
            float margin = Mathf.Max(180f, viewCamera.orthographicSize * viewCamera.aspect + 40f);
            float limit = Mathf.Max(40f, halfMap - margin);
            var p = player.position;
            if (reset || (resetOutsideBounds && (Mathf.Abs(p.x) > limit || Mathf.Abs(p.z) > limit)))
                player.SetPositionAndRotation(startPosition, startRotation);
        }

        private void SetupGroundStyleTest()
        {
            DisposeGroundStyleTest();
            macroGroundTexture = BuildGroundTexture(128, 2.1f, 0.15f, new Color(.73f, .60f, .45f), new Color(.64f, .52f, .39f));
            fineGroundTexture = BuildGroundTexture(128, 9.5f, 0.07f, new Color(.70f, .58f, .45f), new Color(.66f, .55f, .42f));

            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var renderer in renderers)
            {
                string name = renderer.name.ToLowerInvariant();
                if (!name.Contains("terrain") && !name.Contains("ground")) continue;
                if (renderer.sharedMaterials == null || renderer.sharedMaterials.Length == 0) continue;

                var binding = new GroundStyleBinding
                {
                    Renderer = renderer,
                    Original = renderer.sharedMaterials,
                    Flat = CloneMaterials(renderer.sharedMaterials, GroundStyleMode.Flat),
                    Macro = CloneMaterials(renderer.sharedMaterials, GroundStyleMode.Macro),
                    Fine = CloneMaterials(renderer.sharedMaterials, GroundStyleMode.Fine)
                };
                groundBindings.Add(binding);
            }
        }

        private void DisposeGroundStyleTest()
        {
            foreach (var binding in groundBindings)
            {
                if (binding.Renderer != null && binding.Original != null)
                    binding.Renderer.sharedMaterials = binding.Original;
                DestroyMaterials(binding.Flat);
                DestroyMaterials(binding.Macro);
                DestroyMaterials(binding.Fine);
            }
            groundBindings.Clear();
            SafeDestroy(macroGroundTexture);
            SafeDestroy(fineGroundTexture);
            macroGroundTexture = null;
            fineGroundTexture = null;
            groundStyle = GroundStyleMode.Original;
        }

        private void CycleGroundStyle()
        {
            groundStyle = (GroundStyleMode)(((int)groundStyle + 1) % 4);
            ApplyGroundStyle();
        }

        private void ApplyGroundStyle()
        {
            foreach (var binding in groundBindings)
            {
                if (binding.Renderer == null) continue;
                binding.Renderer.sharedMaterials = GroundMaterials(binding, groundStyle);
            }
        }

        private Material[] GroundMaterials(GroundStyleBinding binding, GroundStyleMode mode)
        {
            switch (mode)
            {
                case GroundStyleMode.Flat: return binding.Flat;
                case GroundStyleMode.Macro: return binding.Macro;
                case GroundStyleMode.Fine: return binding.Fine;
                default: return binding.Original;
            }
        }

        private Material[] CloneMaterials(Material[] source, GroundStyleMode mode)
        {
            var result = new Material[source.Length];
            for (int i = 0; i < source.Length; i++)
                result[i] = BuildGroundVariantMaterial(source[i], mode);
            return result;
        }

        private Material BuildGroundVariantMaterial(Material source, GroundStyleMode mode)
        {
            var material = new Material(source) { name = source.name + " (" + mode + ")" };
            material.enableInstancing = true;
            Texture2D texture = Texture2D.whiteTexture;
            Color color = new Color(.72f, .59f, .45f);
            Vector2 scale = Vector2.one;

            switch (mode)
            {
                case GroundStyleMode.Flat:
                    color = new Color(.73f, .60f, .46f);
                    break;
                case GroundStyleMode.Macro:
                    texture = macroGroundTexture != null ? macroGroundTexture : Texture2D.whiteTexture;
                    color = Color.white;
                    scale = new Vector2(2.2f, 2.2f);
                    break;
                case GroundStyleMode.Fine:
                    texture = fineGroundTexture != null ? fineGroundTexture : Texture2D.whiteTexture;
                    color = Color.white;
                    scale = new Vector2(6f, 6f);
                    break;
            }

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
                material.SetTextureScale("_BaseMap", scale);
            }
            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
                material.SetTextureScale("_MainTex", scale);
            }
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_BumpMap")) material.SetTexture("_BumpMap", null);
            if (material.HasProperty("_BumpScale")) material.SetFloat("_BumpScale", 0f);
            if (material.HasProperty("_DetailAlbedoMap")) material.SetTexture("_DetailAlbedoMap", null);
            if (material.HasProperty("_DetailNormalMap")) material.SetTexture("_DetailNormalMap", null);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_OcclusionStrength")) material.SetFloat("_OcclusionStrength", 0f);
            return material;
        }

        private static void DestroyMaterials(Material[] materials)
        {
            if (materials == null) return;
            for (int i = 0; i < materials.Length; i++) SafeDestroy(materials[i]);
        }

        private static void SafeDestroy(Object asset)
        {
            if (asset == null) return;
            if (Application.isPlaying) Object.Destroy(asset);
            else Object.DestroyImmediate(asset);
        }

        private static Texture2D BuildGroundTexture(int size, float frequency, float contrast, Color light, Color dark)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                name = "GroundStylePreview"
            };

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size;
                float v = y / (float)size;
                float a = Mathf.PerlinNoise(11.3f + u * frequency, 3.7f + v * frequency);
                float b = Mathf.PerlinNoise(7.1f + u * frequency * .45f, 19.4f + v * frequency * .45f);
                float tone = Mathf.Clamp01(Mathf.Lerp(a, b, .35f) + (b - .5f) * contrast);
                texture.SetPixel(x, y, Color.Lerp(dark, light, tone));
            }

            texture.Apply(true, false);
            return texture;
        }

        private void OnGUI()
        {
            if (!showLegend || Event.current.type != EventType.Repaint) return;
            if (label == null) label = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };
            GUI.Box(new Rect(12, 12, 415, 106), GUIContent.none);
            GUI.Label(new Rect(22, 16, 395, 22), string.IsNullOrEmpty(studyTitle) ? "ArtLab v10 / Landscape flight" : studyTitle, label);
            GUI.Label(new Rect(22, 38, 395, 22),
                string.IsNullOrEmpty(controlsHelp) ? "WASD: flight   Space: boost   R: recenter" : controlsHelp, label);
            GUI.Label(new Rect(22, 60, 395, 22),
                string.IsNullOrEmpty(cameraHelp) ? "1/2/3: camera 90/75/65 deg   H: hide   T: ground" : cameraHelp, label);
            GUI.Label(new Rect(22, 82, 395, 22), $"Speed {flight.CurrentSpeed:F0}   Boost {boost.Charge01:P0}   {halfMap * 2f:F0} x {halfMap * 2f:F0}", label);
            GUI.Box(new Rect(12, Screen.height - 63, 360, 51), GUIContent.none);
            GUI.Label(new Rect(22, Screen.height - 52, 338, 22), $"E1a / GROUND STYLE   {groundStyle}", label);
            GUI.Label(new Rect(22, Screen.height - 30, 338, 22), "T: original -> flat -> macro -> fine", label);
        }
    }
}
