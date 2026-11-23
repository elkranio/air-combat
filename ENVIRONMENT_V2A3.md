# V2A3 — Correct the banded geology (working experiment)

Base: accepted V2A ground study + V2A2 experimental comparison. **Do not green-light V2A2's drawn geology.** In the screenshot, the extra gullies read as broad, nearly parallel, pixelated vertical bands. This is an actual visual failure, not a need for postprocessing.

## Diagnosis

- V2A2 was a **240x240 mesh over 1200x1200 units (5-unit spacing)**. Its slope/color mask sampled that coarse grid, revealing stepped cells at the real orthographic camera zoom.
- Three similar longitudinal channels each crossed substantial map distance. Their smooth, wide, dark edges created obvious parallel markings rather than natural drainage.
- A more aggressive sharpen/filter setting alone would make those shapes uglier, not fix them.

## V2A3 (non-destructive)

- New scene: `Assets/_Project/Scenes/FrontierV2Erosion.unity`, copied from the **green V2A** scene, **not** the rejected V2A2 variant. V17, V2A, V2A2, combat scenes and shared assets remain intact.
- New authored terrain at **600x600 cells, 2 world units/cell**, with actual normals, tangent space, collision mesh, and 2048x2048 albedo. The grid increase targets coarse mesh stair-steps; it is **not** a brute-force 4K everywhere solution.
- One broad drainage network with finite diagonal tributaries at varied widths, warped locally. Distinct large quiet areas are left clear. Mild upland noise and erosion geometry produce true sunlight shading rather than directional shadows painted into albedo.
- Material colors are **continuous** in interpolated normal slope. Much weaker dark bands. Existing sun, camera, controls and ArtLab pipeline are reused; **no postprocessing, haze, additional kit or road**.
- The editor creates mesh, material, texture and scene **once**. New assets are isolated under `Assets/_Project/Generated/EnvironmentV2/V2A3_*`.

## Run

1. Apply patch with Unity closed. Open Unity with all scenes saved, not in Play Mode.
2. Execute **Air Combat > Art V2 > V2A3 Create Erosion Study** once.
3. Open `Assets/_Project/Scenes/FrontierV2Erosion.unity` and Play.
4. **B:** compare *original V2A* vs *V2A3 branched watershed* with exactly the same camera/lighting. **1/2/3:** 90/75/65 pitch. **H:** hide diagnostics. **R:** recenter.
5. Test the default spawn and 30–60 seconds in each direction; use 1080p and 720p if available. Look for grid/block edges, long straight repeated bands, tiling, strange ridges, unconvincing scale or colors. Jet readability remains more important than decorative detail.
6. After a green Unity test, commit the generated `.unity`, `.asset`, `.mat`, `.png` and **all `.meta` files**. Do not rerun original V2A / V2A2 authoring commands.

## Decision criterion

**Green** only if the land reads as a continuous, organically eroded place at gameplay distance and remains navigable. **Revise** if resolution is okay but drainage is still stylized or material contrast is too subtle. **Reject** if it repeats V2A2's stripes. Do not advance to roads (V2B) until this geological baseline feels convincing.

Unity Play Mode not run while preparing the patch.
