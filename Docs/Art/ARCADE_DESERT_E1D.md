# E1d — long-range real shadows (isolated future-main URP profile)

**Status:** one-time installer prepared, Unity Play Mode verdict pending. Current E1c observation: at Y110 the aircraft shadow appears on the plateau but vanishes on the lower ground. Near-screen-edge shadow disappearance was also seen in older maps. The **230-unit shadow distance** on the shared URP asset is a likely cause, but has not yet been proven to be the only cause.

## Install once

1. Apply the E1d patch and allow Unity Editor scripts to compile. Save/discard scene changes; exit Play Mode.
2. Run **Air Combat > Art > E1d - Install Long-Range Shadows**.
3. Open/Play existing `Assets/_Project/Scenes/ArcadeDesertE1.unity`. Use **7 / 8 / 9** to compare **Y78 / Y110 / Y150**, especially over the plateau and adjacent bare ground.
4. If good, commit the modified scene, the new `Assets/_Project/Settings/ArcadeCombat_URP.asset` **and its generated `.meta` file** along with the patch. The installer refuses to overwrite an existing profile. **Do not rerun E1/E1b/E1c builders.**

## Exact change

- Duplicates `Assets/Settings/ScorchedFrontier_URP.asset` to **`Assets/_Project/Settings/ArcadeCombat_URP.asset`**. This is the candidate for the future main game, **not yet** the global profile.
- In the new profile only: **shadow distance 230 → 600**, **cascades 2 → 4**, cascade splits **0.13 / 0.32 / 0.62**. Keeps the existing **4096** main-light shadow atlas, soft-shadow mode and all other renderer settings.
- Overrides **only** the `CombatLightingRig.pipeline` property of the existing `E1 / CombatLightingRig` **scene prefab instance**. Shared rig prefab, `WeaponLab`, `GroundAttackLab`, V19 and the legacy URP asset remain untouched. The E1 camera already has **700 far clip**, so this pass does not need to change it.
- No fake ground-shadow plane, VFX, extra directional lights, material or terrain edits.

## Play Mode verdict

Compare the real aircraft shadow on bare ground vs the plateau at 110, then at 150; do not assume the shadow must be visible if it is geometrically **outside the viewport**. Try the upper/lateral screen edges while flying toward and away from the plateau. Judge shadow stability and quality, especially when crossing cascade boundaries. A 4-cascade 600-unit setup costs more shadow rendering than the earlier 2-cascade 230-unit setup; profile before treating the setting as production-ready on target hardware.

If the shadow still clips while visible and within the new range, check URP camera shadow settings, shadow-caster bounds/culling and cascade transitions before increasing distance/resolution again. Keep this candidate separate until it passes the art and combat regression checks.
