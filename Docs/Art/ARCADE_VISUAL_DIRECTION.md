# Visual direction — Stylized Arcade Military (working, 2026-11-23)

**Intent:** vivid and readable arcade air combat, **not** photorealistic milsim and **not** a toy/cartoon. We intentionally exaggerate useful silhouettes, material/color grouping, and impactful combat feedback. The look must work in motion at the **accepted fixed-world C3 camera: yaw -45°, pitch 50°, ortho 80, forward/boost look-ahead**. Playability and comfort win over visual tricks.

## Style pillars

1. **Big simple forms, selected exaggeration.** Strong readable outlines for rocks, aircraft, weapons and buildings. Favor 3–5 dominant volumes on an object rather than tiny noisy pieces. No spikes, concentric procedural mountains or ornamental polygons for their own sake. Avoid exaggerated round toy proportions and thick cartoon outlines.
2. **Bold color, restrained surface noise.** Start from a deliberately *saturated* warm desert palette: ground `#DDA44D`, sand/light accents `#F0C66F`, rock body `#C76D42`, darker rock tone `#AC5540`. Keep the ground nearly uniform at first; color and shadow, not photographic detail, establish form. These are initial material albedos, **not** final screenshot colors: the URP light alters their apparent brightness.
3. **Visual contrast through temperature and scale.** Warm earth/rocks, comparatively cool current jet. The aircraft should remain legible against the ground without outlines. Shadows should be directional and physically connected to geometry. No glossy ground, toy-plastic smoothness, over-blown bloom or gray-brown pseudo-realism.
4. **Sparse but intentional level dressing.** Empty space is essential for flying and target anticipation. Rock groups create rhythm and scale; later add readable roads, settlements, industry and a real flyable canyon. Art pieces should support approach paths, target priority and danger zones, not just decorate the minimap.
5. **Combat juice is a later dedicated pass.** Eventually tune weapon sound and impacts together: punchy firing, tracer readability, debris particles, airframe breakup, falling wreckage, terrain contact explosions, smoke and trails. Keep shapes/materials and effects from the same visual language. Do **not** call the present proof-of-concept guns, hits or destruction presentation final.

## Immediate production order

**E1 first-flight:** build one persistent **2×2 km** Unity Terrain with completely flat color, existing PlayerJet, rare compact rock groups and the existing `CombatLightingRig`. Fly it immediately. Evaluate color, rock shapes, shadows, scale and empty-space rhythm in motion. [E1 details](ARCADE_DESERT_E1.md).

**E2 aircraft art exploration (after E1 visual review):** first change the **paint** on the existing aircraft mesh, then compare modest silhouette exaggeration against it, then (only if worthwhile) a more daring stylized version. Same camera, environment, lighting and flight controls for all comparisons. No full aircraft replacement before a clear before/after test.

**Later:** hills/lowlands sculpt, better ground/rock transitions, level objects, collisions and canyon, then VFX/audio quality. Saturation can be reduced *after viewing* the bold initial palette; do not preemptively mute it. Review the wider map work in [ENVIRONMENT_MAP_PLAN.md](ENVIRONMENT_MAP_PLAN.md).

## What we learned from Jet Attack reference (user screenshot 2026-11-23)

The reference's **large empty, low-detail ground areas** and **sparse clusters of volumetric rocks** are useful. We are **not** adopting the intricate, jagged rock meshes wholesale: for E1 use three simpler, angular, fairly massive meshes, no baked maps, and let light/shadow do the heavy lifting. The source game's specific palette, fighter geometry and UI are not assets to copy.
