using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace AirCombat
{
    // Only used in the static ArtLab v2 scene.
    [DisallowMultipleComponent]
    public sealed class ArtLabLightingPreview : MonoBehaviour
    {
        [SerializeField] private Light sun;
        [SerializeField] private bool neutral;
        private bool showLegend = true;
        private GUIStyle label;

        public void Configure(Light light) { sun = light; neutral = false; Apply(); }
        private void OnEnable() => Apply();
        private void Update()
        {
            var keys = Keyboard.current;
            if (keys == null || !Application.isFocused) return;
            if (keys.lKey.wasPressedThisFrame) { neutral = !neutral; Apply(); }
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
            GUI.Box(new Rect(12, 12, 310, 55), GUIContent.none);
            GUI.Label(new Rect(22, 16, 290, 22), "ArtLab v2 / Sunlit Foothills", label);
            GUI.Label(new Rect(22, 38, 290, 22), (neutral ? "L: Neutral light" : "L: Art light") + "    H: Hide legend", label);
        }
    }
}
