using UnityEngine;
using UnityEngine.InputSystem;

namespace AirCombat
{
    // V19 only: switch the roof and exactly one terrain material slot. No keyword
    // mutation at runtime and no changes to the old hero kit / remaining terrain.
    [DisallowMultipleComponent]
    public sealed class FrontierPracticalBakePreview : MonoBehaviour
    {
        [SerializeField] private MeshRenderer roof;
        [SerializeField] private MeshRenderer terrain;
        [SerializeField] private int rockSlot;
        [SerializeField] private Material[] roofModes;
        [SerializeField] private Material[] rockModes;
        private Material[] oldRoof, oldTerrain;
        private int mode = 3;
        private bool show = true;
        private GUIStyle style;
        private static readonly string[] Names = { "Base only", "AO only", "Normal only", "AO + normal" };

        public void Configure(MeshRenderer roofRenderer, MeshRenderer terrainRenderer, int slot,
            Material[] roofVariants, Material[] rockVariants)
        {
            roof = roofRenderer; terrain = terrainRenderer; rockSlot = slot;
            roofModes = roofVariants; rockModes = rockVariants;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (roof == null || terrain == null || roofModes == null || rockModes == null ||
                roofModes.Length != 4 || rockModes.Length != 4 ||
                System.Array.Exists(roofModes, m => m == null) || System.Array.Exists(rockModes, m => m == null) ||
                rockSlot < 0 || rockSlot >= terrain.sharedMaterials.Length || roof.sharedMaterials.Length != 1)
            {
                Debug.LogError("Incomplete V19 material comparison configuration.", this); enabled = false; return;
            }
            oldRoof = roof.sharedMaterials; oldTerrain = terrain.sharedMaterials;
            mode = 3; Apply();
        }

        private void Update()
        {
            var keys = Keyboard.current;
            if (keys == null || !Application.isFocused) return;
            if (keys.bKey.wasPressedThisFrame) { mode = (mode + 1) % 4; Apply(); }
            if (keys.hKey.wasPressedThisFrame) show = !show;
        }

        private void Apply()
        {
            if (oldTerrain == null || oldRoof == null) return;
            var slots = (Material[])oldTerrain.Clone();
            slots[rockSlot] = rockModes[mode]; terrain.sharedMaterials = slots;
            roof.sharedMaterial = roofModes[mode];
        }

        private void OnDisable()
        {
            if (roof != null && oldRoof != null) roof.sharedMaterials = oldRoof;
            if (terrain != null && oldTerrain != null) terrain.sharedMaterials = oldTerrain;
            oldRoof = null; oldTerrain = null;
        }

        private void OnGUI()
        {
            if (!show || Event.current.type != EventType.Repaint) return;
            if (style == null) style = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };
            float x = Mathf.Max(12, Screen.width - 350);
            GUI.Box(new Rect(x, 12, 338, 79), GUIContent.none);
            GUI.Label(new Rect(x + 10, 16, 318, 22), "V19 / Roof + BrokenMesa only", style);
            GUI.Label(new Rect(x + 10, 38, 318, 22), "B: " + Names[mode] + " | real shadows on", style);
            GUI.Label(new Rect(x + 10, 60, 318, 22), "Fly north from refinery to the mesa", style);
        }
    }
}
