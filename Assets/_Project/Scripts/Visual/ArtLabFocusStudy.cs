using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AirCombat
{
    // V12-only presentation changes. The existing flight, camera and pipeline glue are authoritative.
    [DisallowMultipleComponent]
    public sealed class ArtLabFocusStudy : MonoBehaviour
    {
        [Serializable]
        public sealed class AircraftMaterials
        {
            public MeshRenderer renderer;
            public Material[] baseline;
            public Material[] accent;
        }

        [SerializeField] private Light sun;
        [SerializeField] private MeshRenderer ground;
        [SerializeField] private Material baselineGround, softGround, softTrees;
        [SerializeField] private ArtLabForestInstances forest;
        [SerializeField] private Transform visual;
        [SerializeField] private GameObject[] outlines;
        [SerializeField] private AircraftMaterials[] aircraftMaterials;
        private Vector3 baselineScale;
        private Color baselineSun, baselineAmbient;
        private int mode;

        public string ModeName => mode == 0 ? "V11 baseline" : mode == 1 ? "Soft nature" : "Aircraft focus";

        public void Configure(Light light, MeshRenderer terrain, Material mutedGround, Material mutedTrees,
            ArtLabForestInstances trees, Transform aircraft, GameObject[] outlineObjects, AircraftMaterials[] materials)
        {
            sun = light; ground = terrain; baselineGround = terrain.sharedMaterial; softGround = mutedGround;
            softTrees = mutedTrees; forest = trees; visual = aircraft; outlines = outlineObjects; aircraftMaterials = materials;
        }

        private void Awake()
        {
            if (sun == null || ground == null || baselineGround == null || softGround == null || softTrees == null ||
                forest == null || visual == null || outlines == null || aircraftMaterials == null)
            {
                Debug.LogError("Incomplete V12 focus configuration. Regenerate V12.", this);
                enabled = false; return;
            }
            baselineSun = sun.color; baselineAmbient = RenderSettings.ambientLight; baselineScale = visual.localScale;
            mode = 0;
            Apply();
        }

        private void Update()
        {
            var keys = Keyboard.current;
            if (keys == null || !Application.isFocused || !keys.bKey.wasPressedThisFrame) return;
            mode = (mode + 1) % 3;
            Apply();
        }

        private void Apply()
        {
            bool soft = mode != 0, accent = mode == 2;
            ground.sharedMaterial = soft ? softGround : baselineGround;
            forest.SetStudyMaterial(soft ? softTrees : null);
            visual.localScale = accent ? baselineScale * 1.18f : baselineScale;
            sun.color = accent ? (Color)new Color32(248, 244, 235, 255) : baselineSun;
            RenderSettings.ambientLight = accent ? (Color)new Color32(86, 101, 121, 255) : baselineAmbient;
            foreach (var outline in outlines) if (outline != null) outline.SetActive(accent);
            foreach (var entry in aircraftMaterials)
                if (entry != null && entry.renderer != null)
                    entry.renderer.sharedMaterials = accent ? entry.accent : entry.baseline;
        }
    }
}
