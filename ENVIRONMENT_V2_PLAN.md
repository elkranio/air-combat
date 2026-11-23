# ENV V2 — Dry Frontier: visual production plan

**Status:** working art direction / incremental playtest plan. **Not** a commitment to replace the current scenes. First atom: **V2A ground-only study**, Unity test pending.

## Target, not a promise

A credible **stylized military arcade** seen through the actual low-altitude air-combat camera: clean low-poly geometry, materially believable aircraft and infrastructure, recognizable industrial scars and strong geological silhouettes, controlled atmosphere. **Not** a toy diorama, flat color desert, military simulation, hyper-detailed photogrammetry or glossy mobile cartoon.

The generated 16:9 concept is a **mood/subject reference only**, not a literal layout, an asset specification, or evidence the engine can produce that lighting. In particular, its cinematic chase view, jet proportions, fake HUD reticles and dense background should **not** override the accepted gameplay camera, aircraft or combat HUD. Approve work by **actual Unity screenshots** at the real camera distance.

### World identity / current proposed setting

**Dry frontier warzone**: a sparse, semi-arid highland border crossed by a decaying industrial transport corridor. Chalky soil, layered sandstone and grey limestone, shallow dry riverbeds, broken highways, disused refineries, damaged overpasses, concrete emplacements, a few charred trees and electrical lines. A tiny number of tall smokestacks, radar dishes and bridge ruins orient the player. The landscape should tell a clear story *without* decorating every square metre.

**Visual pillars**

1. **Geography first:** traversable valleys and readable rock walls; genuine silhouette breaks rather than uniform Perlin lumps or repeated radial fans. A plane-scale geological mass is more important than fifty small pebbles.
2. **Material hierarchy:** warm restrained dusty earth, desaturated grey/tan concrete, dark cool metal, rare oxidized-red and hazard-orange accents. The jet and incoming threats must remain distinguishable. No global orange saturation.
3. **Designed density:** empty flight corridors → distinct landmark / ruin cluster → clear breathing space. No uniform scatter or noisy high-frequency albedo.
4. **Grounded finish:** dynamic sun shadows, authored silhouettes, optional local bake of actual relief and intentional restrained VFX. Do **not** bake sunlight into base color or hide bad shapes with postprocessing.
5. **Art judged in motion:** target **16:9 1080p and 720p**, orthographic size **80**, pitches **90° / 75° / 65°**, player flying at the study altitude around **Y=48**. Keep tall ground masses below the flight plane in this study; do not carry that constraint over mechanically to every future map.

## Production map and modular kit

**Art study footprint:** ~**1200×1200 world units** first, structured as a broad flyable basin with two flanking ridges and several navigable passages. This is a **prototype scale**, not the final mission extent. Later levels may stream or tile authored terrain chunks rather than using one giant unique mesh. Establish visual scale with the aircraft; avoid camera tricks that make small props look like toys.

**Scene zones (composition, not quests):**

- **A / Western escarpment:** large stratified ridge, sparse rockfall, small radar silhouette.
- **B / Central transport corridor:** dry gully, damaged road, generous open ground, line-of-sight for arcade attack runs.
- **C / Industrial wreckage:** 2–3 grouped ruined structures, exposed pipe rack, tanks, one chimney landmark; recognizable from gameplay height.
- **D / Eastern shelf:** harder fractured geology and a broken overpass or spillway, later a plausible AA emplacement zone.
- **E / Breathing spaces:** deliberately undecorated ground between clusters. Spawn/playtest paths stay free of large obstacles.

**Reusable kits (first ~20–30 useful pieces, not 200 props):**

| Family | Initial scope | Modular contract |
| --- | --- | --- |
| Rocks | 4 large forms, 4 small boulders, 2 ridge/cliff units | 2–3 distinct silhouettes, shared stone palette, height/size variants; no obvious repeated rotations |
| Industrial ruins | 2 wall bays, 2 roof/beam pieces, 1 damaged hangar section, 1 main block, 1 pipe rack | Grid/pivot discipline; roof and walls separable; destruction-specific pieces not needed yet |
| Infrastructure | 1 road/shoulder kit, 1 collapsed bridge span, 1 concrete pad, 1 utility pole, 1 broken fence unit | Spline/terrain fit without floating pieces; roads belong to ground composition |
| Military remnants | 1 concrete blast wall, 1 revetment, 1 bunker shell, 1 derelict radar mast | Static environmental dressing, **not** gameplay damage receivers |
| Atmosphere props | 1 stack, 1 tank, 1 wrecked vehicle silhouette, 2 debris modules | Only where they support a scene landmark, not random confetti |

All pieces have predictable orientation, metric scales, pivots, colliders only if needed, and shared material families. Prefer prefab instances to unique per-instance meshes. Choose a **consistent kit grammar** (bay width, storey height, road shoulder, ruin break shapes) before producing variants. Keep source `.blend` and generator scripts when moving to Blender. Avoid importing massive high-poly sources into game scenes.

## Ordered, testable steps

**Rule:** one atom → create isolated output → inspect at game camera → record **green / revise / reject** → only then proceed. Never rewrite accepted V17, GroundAttackLab, WeaponLab or Sandbox while testing this art direction.

### V2A — Ground / macro composition (first implementation in this patch)

- New isolated scene `Assets/_Project/Scenes/FrontierV2Ground.unity`, **copied from V17 for its player/camera/light setup**; remove V17 environment only inside the new scene.
- Author a **1200×1200** terrain mesh with broad basin, two long ridges, 2 accent shelves and one subtle dry gully, no decorative rocks or buildings. Deliberately cap terrain high below the visual flight plane.
- Produce one **2048² albedo** from broad color zones + slope/height masks + restrained noise. URP Lit, genuine sun shadows, no baked sunlight, fog or heavy post. Mesh and image generated in `Assets/_Project/Generated/EnvironmentV2/`.
- Use actual V17 ArtLab flight, camera pitch controls and ortho size 80. Make screenshots looking north, east and across the ridge at 90/75/65. **Acceptance:** ground looks geographically intentional even *without* props; jet readable; no repeated hill bumps, contour rings, obvious tiling, busy microtexture or floating ground gaps.
- **If weak:** change shape proportions and macro color frequency *before* moving to prop work. This stage is disposable. Do not infer success from the reference image.

### V2B — Roads, readable zones and negative space

- Add one damaged main road, one branching service track, shallow cuts, industrial pad and one bridge/spillway footprint. All align to actual terrain elevations.
- Road shoulders and washed-out edges need to read from above; no white painted lines every few metres. Use deterministic ribbon meshes and material reuse rather than full-map decals.
- **Acceptance:** a screenshot reads as a *place with a route*, not an endless surface. Paths do not hover; landing/AA pads have safe gameplay clearances.

### V2C — First modular silhouette kit

- Create **one** large industrial landmark and **one** cliff cluster using only 6–10 reusable low-poly pieces. Start with greybox, evaluate scale, then extend to the 20–30-piece kit above only as needed.
- Contrast grouped ruin density with open approach corridors; separate meshes/colliders from gameplay targets. No global Object.Find scene spawner in combat scenes.
- **Acceptance:** silhouettes distinguish facility, road and rock from ortho size 80, from several camera pitches; no procedural-scatter look or toy proportions.

### V2D — Material pass and selective Blender bake

- Consolidate palette/roughness across ground, buildings, rocks and aircraft. Add large dirt/scorch and slope transitions; gentle bottom-dust gradient where it helps.
- For *specific* visible hero surfaces, use the existing `ART_AUTHORING.md` workflow: Blender low/high → tangent normal → optional **local** AO → separate albedo → FBX/PNG → Unity compressed mipmapped import. Prefer 1–2 test pieces and strict same-camera **base vs normal vs AO** comparison before generalizing. No light/directional shadow bake in albedo.
- Investigate a shared trim sheet for concrete/metal if repeated industrial modules justify it. Not mandatory for rocks or every asset.
- **Acceptance:** materially more convincing *at flight zoom*, not merely in Blender close-ups. Bake reveals meaningful forms rather than speckles.

### V2E — Lighting, shadows and atmosphere A/B

- Compare two restrained warm/cool lighting grades with identical camera/time; fix known **edge-of-screen shadow quality** issue using controlled cascade/resolution/bias tests; avoid reflexively increasing shadow distance.
- Trial distance haze/fog/atmospheric perspective, mild tone mapping and color grading; optional subtle bloom only around actual hot light/exhaust. Never wash away red threat markers or obscure ground targets.
- **Acceptance:** perceived depth increases and jet/terrain retain separation in motion at 720p and 1080p. Keep toggles for bare geometry / finished look.

### V2F — Living world / VFX restraint

- Add a few readable vertical smoke plumes, fine dust at ruined facilities, very sparse warning lights or heat shimmer. Budget overdraw; smoke should not hide AA tracers or missile warnings.
- Ground damage decals and impact scorch are later gameplay hooks; static scars can be authored now, but don't simulate destruction in art study.
- **Acceptance:** scene feels alive without persistent noise or expensive full-screen distortion.

### V2G — Integration and reusable production kit

- Promote only the **accepted** terrain, kit and lighting assets to gameplay-facing prefabs. Keep authoring source, deterministic export pipeline, LOD candidates and stable material asset GUIDs.
- Build one short combat test route with the existing AA1 and SAM, then compare actual attack readability and route decisions. Move nothing into GroundAttackLab until gameplay camera and collision tests pass.
- Profile on development GPU and at least one weaker hardware target later. A powerful RTX card accelerates bake iteration but must not define shipping performance or compensate for poor art direction.
- **Acceptance:** polished screenshot and combat test look coherent, mobile threats remain visible, camera/hit detection do not regress; old scenes still green.

## Artifact policy and constraints

- Reference: `Resources/References/` (optional image concept); sources `Resources/Blender/`; deterministic generators `Tools/Blender/`; Unity authoring menu commands `Assets/_Project/Editor/` (external helper scripts may live in `Tools/Unity/`); export/baked models `Assets/_Project/Art/Environment/`; disposable meshes/textures `Assets/_Project/Generated/EnvironmentV2/`; one isolated study scene in `Assets/_Project/Scenes/`.
- **V2A first setup:** Unity **Edit Mode**, no unsaved scenes → **Air Combat > Art V2 > V2A Create Ground Study**. This is a **one-time creator** and refuses to overwrite an existing V2A scene. It does not alter V17. Commit Unity-created assets and `.meta` only after Play Mode screenshots are evaluated.
- Prior art technical lessons remain authoritative: [ART_AUTHORING.md](ART_AUTHORING.md), [ART_PIPELINE.md](ART_PIPELINE.md). V20's preferred single-mesa normal style is a reference, *not* a mandatory all-rock material.
- Every step records **date, same-camera screenshots, changes, observed failures, verdict, next action** here or in a short companion log; green only after the user plays and approves.

## Decision log

| Atom | Status | Decision / next test |
| --- | --- | --- |
| V2A ground / macro shapes | **Built in code, Unity playtest pending** | Inspect the first barren terrain before committing to roads/props |
| V2B–V2G | Not started | Proceed only on V2A green or after a deliberate direction change |
