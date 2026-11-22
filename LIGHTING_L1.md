# L1 — shared combat lighting and real URP shadows (current)

**Already installed. Do not run the one-time L1 installer again.** The authoritative asset is `Assets/_Project/Prefabs/Environment/CombatLightingRig.prefab`, used by both `WeaponLab.unity` and `GroundAttackLab.unity`.

## Reuse in new combat scenes

- Instance the **same prefab**. Its `CombatLightingRig` component owns the sun and runtime URP profile (`Assets/Settings/ScorchedFrontier_URP.asset`), not a separate script or scene-specific sun.
- The accepted URP profile provides a 4096 main-light shadowmap and a 230-unit shadow distance. Do not change the global URP profile as part of a camera or weapon cleanup.
- The rig supplies Day, Sunset and NearNight time-of-day presets, azimuth and elevation adjustment, and ambient multiplier. All are Inspector-editable in Play Mode. `SuggestedEmissionMultiplier` is only a hint for future emissive props; it does not modify materials.
- The rig restores the previously active pipeline and scene ambient/sun settings on disable, provided it still owns those settings. Do not instantiate multiple combat lighting authorities in the same scene.
- Renderers on aircraft, bombs, NURS and depot geometry cast **real 3D shadows** on terrain, roofs and the sloped test receiver. Their positions follow geometry and the common sun angle.

## Do not reuse

- `Scripts/Visual/AircraftGroundShadow.cs` is an older ground-projected translucent mesh for the legacy Sandbox and art experiments, not a terrain-aware physical shadow.
- No replacement projected-shadow component or additional material glow is needed for the accepted combat baseline.
- Do not rebake directional light into albedo/normal/AO maps. Environment art assets follow [ART_AUTHORING.md](ART_AUTHORING.md).

## Verify

1. WeaponLab and GroundAttackLab each have a single `CombatLightingRig` prefab instance, with no extra directional sun or Missing Script after script moves.
2. Compare Day / Sunset / NearNight; look for a single physically plausible aircraft shadow and moving bomb/rocket shadows on ground and roofs. Check the temporary slope in GroundAttackLab.
3. Exit Play Mode and change scenes. Lighting state/URP profile must not leak into Sandbox or unrelated art scenes.
4. Confirm Bombs, NURS, camera cue and aircraft flight are unchanged, and Sandbox retains its legacy decorative shadow.

Original one-time import procedure and installation notes: [Docs/Archive/LIGHTING_L1_INSTALL_HISTORY.md](Docs/Archive/LIGHTING_L1_INSTALL_HISTORY.md).
