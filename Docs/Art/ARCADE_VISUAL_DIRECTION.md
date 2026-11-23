# Visual direction — Stylized Arcade Military (working, 2026-11-23)

**Intent:** vivid and readable arcade air combat, **not** photorealistic milsim and **not** a toy/cartoon. We intentionally exaggerate useful silhouettes, material/color grouping, and impactful combat feedback. The look must work in motion at the **accepted fixed-world C3 camera: yaw -45°, pitch 50°, ortho 80, forward/boost look-ahead**. Playability and comfort win over visual tricks.

## Style pillars

1. **Big simple forms, selected exaggeration.** Strong readable outlines for rocks, aircraft, weapons and buildings. Favor 3–5 dominant volumes on an object rather than tiny noisy pieces. No spikes, concentric procedural mountains or ornamental polygons for their own sake. Avoid exaggerated round toy proportions and thick cartoon outlines.
2. **Color hierarchy, restrained surface noise.** Ground is a calm, **muted mid-light** material (E1b first pass `#B8A993`), not a vivid golden field and not dark grit. Keep it nearly uniform, matte and free of photos/noise. Save stronger hue/saturation for aircraft, military structures, threats and VFX. Existing bold E1 albedos were tried and rejected, not future palette targets.
3. **Volume, readable shadows, deliberate scale.** Choose large chunky faceted stones/outcrops with slightly softened, broad planes, not little pebbles or sharp low-poly spikes. Fly slightly higher in E1, with real rock/aircraft ground shadows. Retain cooler aircraft contrast; no glossy ground, toy-plastic smoothness, huge bloom or muddy realism.
4. **Sparse but intentional level dressing.** Empty space is essential for flying and target anticipation. Rock groups create rhythm and scale; later add readable roads, settlements, industry and a real flyable canyon. Art pieces should support approach paths, target priority and danger zones, not just decorate the minimap.
5. **Combat juice is a later dedicated pass.** Eventually tune weapon sound and impacts together: punchy firing, tracer readability, debris particles, airframe breakup, falling wreckage, terrain contact explosions, smoke and trails. Keep shapes/materials and effects from the same visual language. Do **not** call the present proof-of-concept guns, hits or destruction presentation final.

## Immediate production order

**E1b active map art:** reuse the existing **2×2 km** native Unity Terrain. Test a muted uniform ground, three authored broad outcrop meshes, Y78 flight and scene-specific Kestrel paint under the shared CombatLightingRig. [E1b implementation and test](ARCADE_DESERT_E1B.md). No second map/build-once E1 regeneration.

**Aircraft art exploration:** E1b already tries a scene-only material paint on the existing Kestrel. After reviewing it in flight, refine color grouping and maybe exaggerate the silhouette; avoid editing the shared prefab until a version is genuinely approved.

**Later:** sculpt hills/lowlands, connect ground to canyon/rocks, populate roads/bases and real collisions, then a separate high-impact VFX/audio pass. Prioritize readable flight and targets over extra decoration. Review [map plan](ENVIRONMENT_MAP_PLAN.md).

## What we learned from Jet Attack reference (user screenshot 2026-11-23)

The reference's **large empty, low-detail ground areas** and **sparse clusters of volumetric rocks** are useful. We are **not** adopting the intricate, jagged rock meshes wholesale: for E1 use three simpler, angular, fairly massive meshes, no baked maps, and let light/shadow do the heavy lifting. The source game's specific palette, fighter geometry and UI are not assets to copy.
