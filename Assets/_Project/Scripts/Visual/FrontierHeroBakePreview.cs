using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AirCombat
{
    // Showcase only. Material copies live for one play session; no asset or global pipeline mutations.
    [DisallowMultipleComponent]
    public sealed class FrontierHeroBakePreview : MonoBehaviour
    {
        [SerializeField] private Material[] bakedMaterials;
        [SerializeField] private MeshRenderer[] showcaseRenderers;
        private readonly Dictionary<Material, Material> copies = new Dictionary<Material, Material>();
        private readonly Dictionary<MeshRenderer, Material[]> originals = new Dictionary<MeshRenderer, Material[]>();
        private int mode = 3;
        private bool show = true;
        private GUIStyle style;

        public void Configure(Material[] materials, MeshRenderer[] renderers)
        { bakedMaterials = materials; showcaseRenderers = renderers; }

        private void OnEnable()
        {
            if (!Application.isPlaying || bakedMaterials == null || showcaseRenderers == null) return;
            foreach (var material in bakedMaterials)
                if (material != null && !copies.ContainsKey(material))
                    copies.Add(material, new Material(material) { name = material.name + " (Showcase runtime)" });
            foreach (var renderer in showcaseRenderers)
            {
                if (renderer == null) continue;
                var source = renderer.sharedMaterials;
                var local = (Material[])source.Clone();
                for (int i = 0; i < local.Length; i++)
                    if (local[i] != null && copies.TryGetValue(local[i], out var replacement)) local[i] = replacement;
                originals[renderer] = source;
                renderer.sharedMaterials = local;
            }
            mode = 3; ApplyMode();
        }

        private void Update()
        {
            var keys = Keyboard.current;
            if (keys == null || !Application.isFocused) return;
            if (keys.bKey.wasPressedThisFrame) { mode = (mode + 1) % 4; ApplyMode(); }
            if (keys.hKey.wasPressedThisFrame) show = !show;
        }

        private void ApplyMode()
        {
            bool ao = mode == 1 || mode == 3, normal = mode == 2 || mode == 3;
            foreach (var pair in copies)
            {
                var original = pair.Key; var m = pair.Value;
                bool useAO = ao && original.GetTexture("_OcclusionMap") != null;
                bool useNormal = normal && original.GetTexture("_BumpMap") != null;
                m.SetFloat("_OcclusionStrength", useAO ? original.GetFloat("_OcclusionStrength") : 0f);
                m.SetFloat("_BumpScale", useNormal ? original.GetFloat("_BumpScale") : 0f);
                if (useAO) m.EnableKeyword("_OCCLUSIONMAP"); else m.DisableKeyword("_OCCLUSIONMAP");
                if (useNormal) m.EnableKeyword("_NORMALMAP"); else m.DisableKeyword("_NORMALMAP");
            }
        }

        private void OnDisable()
        {
            foreach (var pair in originals) if (pair.Key != null) pair.Key.sharedMaterials = pair.Value;
            originals.Clear();
            foreach (var material in copies.Values) if (material != null) Destroy(material);
            copies.Clear();
        }

        private void OnGUI()
        {
            if (!show || Event.current.type != EventType.Repaint) return;
            if (style == null) style = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };
            string[] names = { "Base color only", "Local AO", "High-poly normals", "AO + normals" };
            float x = Mathf.Max(12, Screen.width - 300);
            GUI.Box(new Rect(x, 12, 288, 57), GUIContent.none);
            GUI.Label(new Rect(x + 10, 16, 270, 22), "V14A / B: material comparison", style);
            GUI.Label(new Rect(x + 10, 38, 270, 22), names[mode] + " | real shadows always on", style);
        }
    }
}
