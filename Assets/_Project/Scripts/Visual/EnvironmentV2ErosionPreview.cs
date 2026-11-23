using UnityEngine;
using UnityEngine.InputSystem;

namespace AirCombat
{
    // V2A3: compares a green baseline with the fixed geometry/color in one scene.
    [DisallowMultipleComponent]
    public sealed class EnvironmentV2ErosionPreview : MonoBehaviour
    {
        [SerializeField] private MeshFilter terrain;
        [SerializeField] private MeshRenderer renderer;
        [SerializeField] private MeshCollider collider;
        [SerializeField] private Mesh originalMesh;
        [SerializeField] private Material originalMaterial;
        [SerializeField] private Mesh newMesh;
        [SerializeField] private Material newMaterial;

        private bool improved = true;
        private bool show = true;
        private GUIStyle style;

        public void Configure(MeshFilter mesh, MeshRenderer surface, MeshCollider meshCollider,
            Mesh baseline, Material baselineMaterial, Mesh updated, Material updatedMaterial)
        {
            terrain = mesh;
            renderer = surface;
            collider = meshCollider;
            originalMesh = baseline;
            originalMaterial = baselineMaterial;
            newMesh = updated;
            newMaterial = updatedMaterial;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (terrain == null || renderer == null || collider == null ||
                originalMesh == null || originalMaterial == null || newMesh == null || newMaterial == null)
            {
                Debug.LogError("V2A3 comparison has incomplete mesh/material references.", this);
                enabled = false;
                return;
            }
            Apply();
        }

        private void Update()
        {
            if (!Application.isFocused || Keyboard.current == null) return;
            if (Keyboard.current.bKey.wasPressedThisFrame)
            {
                improved = !improved;
                Apply();
            }
            if (Keyboard.current.hKey.wasPressedThisFrame) show = !show;
        }

        private void Apply()
        {
            terrain.sharedMesh = improved ? newMesh : originalMesh;
            renderer.sharedMaterial = improved ? newMaterial : originalMaterial;
            collider.sharedMesh = null;
            collider.sharedMesh = terrain.sharedMesh;
        }

        private void OnGUI()
        {
            if (!show || Event.current.type != EventType.Repaint) return;
            if (style == null)
                style = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };
            float x = Mathf.Max(12f, Screen.width - 400f);
            GUI.Box(new Rect(x, 12, 388, 66), GUIContent.none);
            GUI.Label(new Rect(x + 10, 17, 366, 22), "V2A3 / watershed + 2m mesh", style);
            GUI.Label(new Rect(x + 10, 42, 366, 22),
                "B: " + (improved ? "NEW / branched relief" : "ORIGINAL / V2A") + "   H: hide", style);
        }
    }
}
