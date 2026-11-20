using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace AirCombat
{
    // ArtLab only. Never attached to the playable Sandbox.
    [DisallowMultipleComponent]
    public sealed class ArtLabPalettePreview : MonoBehaviour
    {
        [Serializable]
        public sealed class Study
        {
            public string label;
            public GameObject environment;
            public Color sunColor = Color.white;
            public float sunIntensity = 1.2f;
            public Color ambientColor = Color.gray;
            public Color backgroundColor = Color.gray;
        }

        [SerializeField] private Study[] studies;
        [SerializeField] private Light sun;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private int selected;
        [SerializeField] private bool neutralLighting;
        private bool showLegend = true;
        private GUIStyle heading;
        private GUIStyle hint;

        public void Configure(Study[] values, Light light, Camera camera)
        {
            studies = values;
            sun = light;
            viewCamera = camera;
            selected = 0;
            neutralLighting = false;
            Apply(0);
        }

        private void OnEnable() => Apply(selected);

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !Application.isFocused) return;
            if (keyboard.digit1Key.wasPressedThisFrame) Apply(0);
            if (keyboard.digit2Key.wasPressedThisFrame) Apply(1);
            if (keyboard.digit3Key.wasPressedThisFrame) Apply(2);
            if (keyboard.lKey.wasPressedThisFrame)
            {
                neutralLighting = !neutralLighting;
                Apply(selected);
            }
            if (keyboard.hKey.wasPressedThisFrame) showLegend = !showLegend;
        }

        private void Apply(int index)
        {
            if (studies == null || studies.Length == 0 || index < 0 || index >= studies.Length) return;
            selected = index;
            for (int i = 0; i < studies.Length; i++)
                if (studies[i].environment != null) studies[i].environment.SetActive(i == selected);
            var active = studies[selected];
            if (sun != null)
            {
                sun.color = neutralLighting ? Color.white : active.sunColor;
                sun.intensity = neutralLighting ? 1.2f : active.sunIntensity;
            }
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = neutralLighting ? new Color(0.66f, 0.71f, 0.70f) : active.ambientColor;
            if (viewCamera != null) viewCamera.backgroundColor = active.backgroundColor;
        }

        private void OnGUI()
        {
            if (!showLegend || Event.current.type != EventType.Repaint || studies == null || studies.Length == 0) return;
            if (heading == null)
            {
                heading = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold };
                heading.normal.textColor = Color.white;
                hint = new GUIStyle(GUI.skin.label) { fontSize = 13 };
                hint.normal.textColor = new Color(0.87f, 0.91f, 0.90f);
            }
            float w = Mathf.Min(365f, Screen.width - 24f);
            if (w <= 0f) return;
            var saved = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.82f);
            GUI.Box(new Rect(12, 12, w, 80), GUIContent.none);
            GUI.color = saved;
            GUI.Label(new Rect(22, 17, w - 20, 26), studies[selected].label, heading);
            GUI.Label(new Rect(22, 43, w - 20, 20), "1 Emerald   2 Desert   3 Northern", hint);
            GUI.Label(new Rect(22, 63, w - 20, 20), "L: " + (neutralLighting ? "Neutral light" : "Art light") + "    H: Hide legend", hint);
        }
    }
}
