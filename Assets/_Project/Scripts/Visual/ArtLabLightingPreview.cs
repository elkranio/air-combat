using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace AirCombat
{
    // Only used in the static ArtLab v2/v3/v4/v5/v6 scenes.
    [DisallowMultipleComponent]
    public sealed class ArtLabLightingPreview : MonoBehaviour
    {
        [SerializeField] private Light sun;
        [SerializeField] private bool neutral;
        [SerializeField] private MeshRenderer[] groundRenderers;
        [SerializeField] private Material[] originalGround;
        [SerializeField] private Material[] texturedGround;
        [SerializeField] private bool newGround = true;
        [SerializeField] private bool antiTilingStudy;
        [SerializeField] private GameObject originalTrees;
        [SerializeField] private GameObject authoredTrees;
        [SerializeField] private bool newTrees = true;
        [SerializeField] private bool forestStudy;
        [SerializeField] private bool canopyStudy;
        private bool HasTreeComparison => originalTrees != null && authoredTrees != null;
        private bool HasGroundComparison => groundRenderers != null && groundRenderers.Length > 0;
        private bool showLegend = true;
        private GUIStyle label;

        public void Configure(Light light) { sun = light; neutral = false; Apply(); }
        public void ConfigureGroundComparison(MeshRenderer[] renderers, Material[] original, Material[] textured, bool antiTiling = false)
        {
            antiTilingStudy = antiTiling;
            groundRenderers = renderers; originalGround = original; texturedGround = textured;
            newGround = true;
            ApplyGround();
        }
        public void ConfigureTreeComparison(GameObject original, GameObject authored, bool forest = false, bool canopy = false)
        {
            forestStudy = forest; canopyStudy = canopy;
            originalTrees = original; authoredTrees = authored; newTrees = true;
            ApplyTrees();
        }
        private void OnEnable() { Apply(); ApplyGround(); ApplyTrees(); }
        private void ApplyTrees()
        {
            if (!HasTreeComparison) return;
            originalTrees.SetActive(!newTrees);
            authoredTrees.SetActive(newTrees);
        }
        private void ApplyGround()
        {
            if (!HasGroundComparison) return;
            var materials = newGround ? texturedGround : originalGround;
            if (materials == null || materials.Length != groundRenderers.Length) return;
            for (int i = 0; i < materials.Length; i++)
                if (groundRenderers[i] != null && materials[i] != null)
                    groundRenderers[i].sharedMaterial = materials[i];
        }
        private void Update()
        {
            var keys = Keyboard.current;
            if (keys == null || !Application.isFocused) return;
            if (keys.lKey.wasPressedThisFrame) { neutral = !neutral; Apply(); }
            if (HasGroundComparison && keys.gKey.wasPressedThisFrame) { newGround = !newGround; ApplyGround(); }
            if (HasTreeComparison && keys.tKey.wasPressedThisFrame) { newTrees = !newTrees; ApplyTrees(); }
            if (keys.hKey.wasPressedThisFrame) showLegend = !showLegend;
        }
        private void Apply()
        {
            if (sun == null) return;
            sun.color = neutral ? Color.white : new Color32(255, 231, 186, 255);
            sun.intensity = neutral ? 1.2f : 1.22f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = neutral ? new Color(.66f, .71f, .70f) : new Color32(104, 132, 158, 255);
        }
        private void OnGUI()
        {
            if (!showLegend || Event.current.type != EventType.Repaint) return;
            if (label == null) label = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };
            GUI.Box(new Rect(12, 12, HasTreeComparison ? 340 : HasGroundComparison ? 335 : 310, HasTreeComparison ? 100 : HasGroundComparison ? 78 : 55), GUIContent.none);
            GUI.Label(new Rect(22, 16, 290, 22), HasTreeComparison ? (canopyStudy ? "ArtLab v6 / Continuous canopy" : forestStudy ? "ArtLab v5 / Forest composition" : "ArtLab v4 / Tree pack") : antiTilingStudy ? "ArtLab v3.1 / Anti-tiling" : HasGroundComparison ? "ArtLab v3 / Ground material study" : "ArtLab v2 / Sunlit Foothills", label);
            GUI.Label(new Rect(22, 38, 290, 22), (neutral ? "L: Neutral light" : "L: Art light") + "    H: Hide legend", label);
            if (HasGroundComparison) GUI.Label(new Rect(22, 60, 310, 22), "G: Ground / " + (antiTilingStudy ? (newGround ? "Anti-tiling" : "Tiled v3") : (newGround ? "New textures" : "Original v2")), label);
            if (HasTreeComparison) GUI.Label(new Rect(22, 82, 310, 22), "T: Trees / " + (canopyStudy ? (newTrees ? "Canopy modules" : "Dense individual trees") : forestStudy ? (newTrees ? "Forest masses" : "Scattered trees") : (newTrees ? "Blender pack" : "Procedural")), label);
        }
    }
}
