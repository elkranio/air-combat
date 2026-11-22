# L1 — Unified combat lighting and physical URP shadows

Source: green master `9af37985`. This atom is an infrastructure migration, not a weapon/flight change.

## Install once in Unity (Edit Mode)

1. Apply patch and let scripts compile. Save any scene edits; exit Play Mode.
2. Menu: **Air Combat > Lighting > Install Unified Lighting L1**.
3. The one-shot importer creates `Assets/_Project/Prefabs/Environment/CombatLightingRig.prefab`, replaces each scene's old directional light with an **instance of that same prefab** in `WeaponLab.unity` and `GroundAttackLab.unity`, and creates a disposable sloped receiver at `(170,0,420)` in GroundAttackLab. Nothing modifies the shared aircraft, bomb or depot prefab.
4. The script refuses to overwrite existing LightingRig/slope output and checks for unexpected directional light roots. Save/commit the generated prefab, slope mesh, updated scene files and all generated `.meta` files after testing. **Do not re-run the old GroundAttackLab/WeaponLab creation generators.**

## One lighting authority for future levels

- `CombatLightingRig` owns `QualitySettings.renderPipeline = ScorchedFrontier_URP.asset` while its scene is running. **Both scenes use the same pre-existing 4096 main-light shadowmap and 230-unit shadow distance** already accepted in WeaponLab; these are URP asset settings, not per-bomb numbers.
- The **prefab** is the single source of truth for the directional sun: shadow casting, soft shadows, bias, azimuth/time-of-day control, and ambient response. Add the prefab to any new scene; don't create a scene-specific shadow renderer or URP override.
- Inspector (Play Mode): change `Time Of Day` between **Day**, **Sunset**, and **Near Night** to inspect sun elevation/tint/intensity and ambient lighting. `Azimuth`, `Elevation Offset` and `Ambient Multiplier` are numeric knobs; no automatic clock. Day approximates WeaponLab's existing light (38° elevation, -38° azimuth, 1.38 intensity). Sunset 14°, Near Night 5°; sun remains above horizon to avoid inverted light.
- `SuggestedEmissionMultiplier` is an API hint for later emissive signs/lamps: **it does not yet increase actual material emission** or spawn point lights. A near-night state is an illumination preview, not a finished night art pass.
- No procedural `AircraftGroundShadow` in GroundAttackLab. The approved `Kestrel` renderers already cast real URP shadows; the bomb capsule now does the same. Any terrain, slope and building with shadow-receiving URP Lit material receives shadows according to actual 3D height.
- `AircraftGroundShadow` is intentionally kept for the legacy Sandbox (and any earlier visual experiments) until that separate scene is migrated. No global deletion.

## Visual verification

- **WeaponLab**: same Kestrel silhouette shadow as baseline, no extra sun/double illumination, 1/2/3 weapons, camera, enemy, Q/E still green.
- **GroundAttackLab**: at altitude 48 the jet casts one shape-accurate shadow, with separation determined by sun elevation (not a manually moved disk). Bomb shells cast real shadows that move on ground and bunker roofs as they descend; check four sequential drops without marker. At the offset **test slope**, shadows must bend over the inclined mesh rather than sticking to y=0; test crossing near its raised end.
- Change sun `Time Of Day` and `Azimuth` while playing; **both** aircraft and bomb shadows must react to the same changes. Test bright Day and Sunset first; Near Night may need actual emissive fixture visuals in a later art atom.
- If the small bomb shadow is too weak at ortho size 80, inspect URP shadow cascade/filtering or an optional **shadow-only proxy mesh** attached to the bomb: do not restore manual ground spots, fake raycast positioning or geometry-free decals.
- **Sandbox** remains unchanged and is not part of this migration; Build Settings still include only Sandbox.

No NURS, explosions rebalance, terrain collision redesign or global sunlight animation in L1. The test slope is temporary scene geometry that can be removed in a later cleanup atom.
