# Visual direction — Stylized Arcade Military (working, 2026-11-23)

**Intent:** vivid and readable arcade air combat, **not** photorealistic milsim and **not** a toy/cartoon. We intentionally exaggerate useful silhouettes, material/color grouping, and impactful combat feedback. The look must work in motion at the **accepted fixed-world C3 camera: yaw -45°, pitch 50°, ortho 80, forward/boost look-ahead**. Playability and comfort win over visual tricks.

## Style pillars

1. **Big simple forms, selected exaggeration.** Strong readable outlines for rocks, aircraft, weapons and buildings. Favor 3–5 dominant volumes on an object rather than tiny noisy pieces. No spikes, concentric procedural mountains or ornamental polygons for their own sake. Avoid exaggerated round toy proportions and thick cartoon outlines.
2. **Color hierarchy, restrained surface noise.** Ground is a calm, **muted mid-light** material (E1b first pass `#B8A993`), not a vivid golden field and not dark grit. Keep it nearly uniform, matte and free of photos/noise. Save stronger hue/saturation for aircraft, military structures, threats and VFX. Existing bold E1 albedos were tried and rejected, not future palette targets.
3. **Large-scale terrain, grounded shadows, aircraft contrast.** E1b's 18 rock chunks looked like toys despite being larger than pebbles. Replace the scatter with **one very broad, low, faceted plateau** (hundreds of metres across) and compare Y78/110/150 flight. Dark cool aircraft materials must stand apart from muted sand without bright pale wings. No fake plane-shadow blob; evaluate real URP shadows. No glossy ground or concentric cliff rings.
4. **Sparse but intentional level dressing.** Empty space is essential for flying and target anticipation. Rock groups create rhythm and scale; later add readable roads, settlements, industry and a real flyable canyon. Art pieces should support approach paths, target priority and danger zones, not just decorate the minimap.
5. **Combat juice is a later dedicated pass.** Eventually tune weapon sound and impacts together: punchy firing, tracer readability, debris particles, airframe breakup, falling wreckage, terrain contact explosions, smoke and trails. Keep shapes/materials and effects from the same visual language. Do **not** call the present proof-of-concept guns, hits or destruction presentation final.

## Immediate production order

**E1c active scale reset:** retain the approved muted flat ground and persistent E1 map; one authored **430×275m** plateau, isolated dark Kestrel paint, initial Y150 and `7/8/9` altitude comparison. [E1c implementation](ARCADE_DESERT_E1C.md). No E1 builder reruns or new maps.

**Aircraft art exploration:** judge new darker paint in E1c first. Afterwards test a **slightly elongated nose/fuselage** in isolation (candidate 15–20% more length), preserving current shared aircraft prefab and gameplay handling until visually accepted.

**Later:** sculpt hills/lowlands, connect ground to canyon/rocks, populate roads/bases and real collisions, then a separate high-impact VFX/audio pass. Prioritize readable flight and targets over extra decoration. Review [map plan](ENVIRONMENT_MAP_PLAN.md).

## What we learned from Jet Attack reference (user screenshot 2026-11-23)

The reference's **large empty, low-detail ground areas** and **sparse clusters of volumetric rocks** are useful. We are **not** adopting the intricate, jagged rock meshes wholesale: for E1 use three simpler, angular, fairly massive meshes, no baked maps, and let light/shadow do the heavy lifting. The source game's specific palette, fighter geometry and UI are not assets to copy.
