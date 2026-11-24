# B1d3 — one prepared lot, hard irregular perimeter, easy A/B
> **Historical atom — already installed (2026-11-24).** The hard-edged prepared lot is present in the scene and separately toggleable. Do not rerun its installer. Current state: [BASE_DESIGN.md](BASE_DESIGN.md).

---


**Status:** authored replacement for the installed B1d/B1d2 ground; **Unity visual verdict pending**. The roads, wear, barracks, HQ, plaza, plateau, camera and default flight Y78 are untouched. This is a follow-up to [B1d roads](ROAD_B1D.md), not a new road installer.

## Install once

1. Apply the B1d3 patch on the current green `master` with B1d/B1d2 already installed. Exit Play Mode and save/discard unsaved scenes.
2. Let Unity compile `InstallPreparedLotB1D3.cs`.
3. Run **Air Combat > Art > B1d3 - Install Sharp-Edge Prepared Lot** once.
4. Play `Assets/_Project/Scenes/ArcadeDesertE1.unity` at **Y78**. Compare the full base from the actual C3 flight view.
5. Select **`B1 / Prepared lot - DISABLE to compare`** in the scene Hierarchy and toggle its active checkbox: **on = prepared soil**, **off = natural desert with roads and buildings unchanged**.

After running the menu, commit the modified **`ArcadeDesertE1.unity`**, **`E1_Terrain.asset`**, generated **`B1d3_PreparedLot.asset`**, **`B1d3_PreparedLot.mat`** and **`B1d3_PreparedLot.prefab`**, together with their Unity `.meta` files. The one-time installer refuses to run again or overwrite any of those assets.

## What changed

- Previous B1d/B1d2: multiple overlapping rounded-rectangle terrain stamps, ~2m feather; because it lives in the Terrain alphamap, switching it off independently was awkward.
- B1d3: **one contiguous ~183 × 226 m prepared lot** under the whole developed sector, with a deliberately non-rectangular ~29-point perimeter. Hard material boundary, zero alpha gradient and no separate framed patches under each building. It contains the access road, barracks service lane, HQ and plaza, with room for future structures. A few irregular edge breaks make it look like graded terrain, not an architectural platform.
- Matte warm earth **`#A38B72`**, flat-color URP Lit. No paint noise, dark decal halo, baked AO, painted shadow or raised rim. The lot is a thin (2.8cm) visual surface over the currently flat terrain, does not cast shadow, and receives real URP shadows. The original **TerrainCollider** remains authoritative; the lot has no collider.
- The old **prepared** TerrainLayer and texture assets are preserved with their GUIDs, but **the old prepared alpha coverage in the B1 region is cleared**. Terrain heights, base sand layer and paint outside that local patch are not changed. This makes Hierarchy toggle genuinely remove the soil, rather than revealing a second painted area underneath.
- Existing asphalt ribbons, shoulder meshes, wear patches and all gameplay remain untouched. Their materials and prefab references are not regenerated.

## What to judge

Does the base look like it occupies an intentional **worked dirt clearing** rather than floating on colored sand? Does the sharp, imperfect outline help, or does it look like a giant board-game base? Check the whole compound from Y78, turning and flying over both the prepared ground and the bare sand beyond it.

If it reads as a platform/diarama, **disable the lot root and keep the roads**. No destructive undo required. This is an explicit A/B test before adding structure weathering, new buildings or more terrain detail.

**Limitations:** the B1d3 lot is authored for the current flat sector; if Terrain is sculpted later, regenerate/re-author the mesh to follow the new heights. Don't rerun the installer on a moved base or a reworked TerrainData.
