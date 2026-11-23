using UnityEngine;
using UnityEngine.InputSystem;

namespace AirCombat
{
    // V2A2-only same-camera comparison: the mesh AND its albedo change together.
    // This component is not referenced by gameplay scenes or shared prefabs.
    [DisallowMultipleComponent]
    public sealed class EnvironmentV2GeologyComparison : MonoBehaviour
    {
        [SerializeField] private MeshFilter terrain;
        [SerializeField] private MeshRenderer surface;
        [SerializeField] private MeshCollider collider;
        [SerializeField] private Mesh[] geometry;
        [SerializeField] private Material[] materials;

        private static readonly string[] Names = { "V2A / original", "V2A2 / moderate", "V2A2 / defined" };
        private int selected = 2;
        private bool show = true;
        private GUIStyle label;

        public void Configure(MeshFilter meshFilter, MeshRenderer renderer, MeshCollider meshCollider,
            Mesh[] meshes, Material[] variants)
        {
            terrain = meshFilter;
            surface = renderer;
            collider = meshCollider;
            geometry = meshes;
            materials = variants;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (!Valid())
            {
                Debug.LogError("V2A2: Incomplete geology comparison assets.", this);
                enabled = false;
                return;
            }
            Apply();
        }

        private bool Valid()
        {
            if (terrain == null || surface == null || collider == null ||
                geometry == null || materials == null || geometry.Length != 3 || materials.Length != 3)
                return false;
            for (int i = 0; i < 3; i++)
                if (geometry[i] == null || materials[i] == null) return false;
            return true;
        }

        private void Update()
        {
            if (!Application.isFocused) return;
            var keys = Keyboard.current;
            if (keys == null) return;
            if (keys.bKey.wasPressedThisFrame)
            {
                selected = (selected + 1) % 3;
                Apply();
            }
            if (keys.hKey.wasPressedThisFrame) show = !show;
        }

        private void Apply()
        {
            if (!Valid()) return;
            terrain.sharedMesh = geometry[selected];
            surface.sharedMaterial = materials[selected];
            // Update only when toggling, not every frame. Flight remains above terrain.
            collider.sharedMesh = null;
            collider.sharedMesh = geometry[selected];
        }

        private void OnGUI()
        {
            if (!show || Event.current.type != EventType.Repaint) return;
            if (label == null)
                label = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };
            float x = Mathf.Max(12, Screen.width - 360);
            GUI.Box(new Rect(x, 12, 348, 76), GUIContent.none);
            GUI.Label(new Rect(x + 10, 18, 327, 24), "GEOLOGY TEST / same camera + light", label);
            GUI.Label(new Rect(x + 10, 45, 327, 24), "B: " + Names[selected] + "   |   H: hide", label);
        }
    }
}
