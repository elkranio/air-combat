using UnityEngine;
using UnityEngine.InputSystem;

namespace AirCombat
{
    // V20 only. Keep all other material slots and the roof fixed during the comparison.
    [DisallowMultipleComponent]
    public sealed class FrontierMesaPolishPreview : MonoBehaviour
    {
        [SerializeField] private MeshRenderer terrain;
        [SerializeField] private int rockSlot;
        [SerializeField] private Material[] modes;
        private Material[] original;
        private int mode = 3;
        private bool show = true;
        private GUIStyle style;
        private static readonly string[] Names = { "Base only", "V19 normal 1.00", "V19 normal 0.40", "V20 beds 0.45", "V20 beds 0.80" };
        public void Configure(MeshRenderer renderer, int slot, Material[] variants)
        { terrain = renderer; rockSlot = slot; modes = variants; }
        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (terrain == null || modes == null || modes.Length != Names.Length ||
                System.Array.Exists(modes, m => m == null) || rockSlot < 0 || rockSlot >= terrain.sharedMaterials.Length)
            { Debug.LogError("Incomplete V20 mesa comparison.", this); enabled = false; return; }
            original = terrain.sharedMaterials; mode = 3; Apply();
        }
        private void Update()
        {
            var keys = Keyboard.current;
            if (keys == null || !Application.isFocused) return;
            if (keys.bKey.wasPressedThisFrame) { mode = (mode + 1) % Names.Length; Apply(); }
            if (keys.hKey.wasPressedThisFrame) show = !show;
        }
        private void Apply()
        {
            if (original == null) return;
            var slots = (Material[])original.Clone(); slots[rockSlot] = modes[mode]; terrain.sharedMaterials = slots;
        }
        private void OnDisable()
        { if (terrain != null && original != null) terrain.sharedMaterials = original; original = null; }
        private void OnGUI()
        {
            if (!show || Event.current.type != EventType.Repaint) return;
            if (style == null) style = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };
            float x = Mathf.Max(12, Screen.width - 350);
            GUI.Box(new Rect(x, 12, 338, 79), GUIContent.none);
            GUI.Label(new Rect(x + 10, 16, 318, 22), "V20 / BrokenMesa only (no AO)", style);
            GUI.Label(new Rect(x + 10, 38, 318, 22), "B: " + Names[mode], style);
            GUI.Label(new Rect(x + 10, 60, 318, 22), "Same terrain, color and dynamic light", style);
        }
    }
}
