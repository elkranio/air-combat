# E1a — ground style test on accepted C3 camera

Status: **historical comparison; flat-color approach selected for the new vivid E1 map**. The original C3 `T` toggles still exist in the old combat study; do not use this document as the next production task. See [E1](ARCADE_DESERT_E1.md). The goal is not to finish the map, but to answer one art question fast: **how simple should the ground be so the game reads like an arcade jet game, not a muddy survival sandbox?**

## Rationale

The user explicitly compared the current image against **Jet Attack** and called out the risk of a *Kenshi-like* look: the ground carries too much low-contrast noisy detail, while roads/buildings/rocks are sharp and therefore clash with it. The proposed remedy is intentionally simple: test a flatter ground language and let silhouettes, roads, shadows and structures provide the readable detail.

## How the test works

Use the already committed scene:

- `Assets/_Project/Scenes/FrontierCameraCombatC1.unity`

No builder, no scene regeneration, no new imported textures.

`ArtLabLandscapeFlight` now supports a runtime ground-style preview hotkey:

- **T** — cycle ground style: **Original -> Flat -> Macro -> Fine**.

The camera/gameplay baseline stays unchanged:

- fixed-world **50°** camera,
- C3 forward look-ahead,
- six weapon slots,
- fighter, AA1, SAM1 and radar still present.

## What the styles mean

- **Original** — current committed ground presentation.
- **Flat** — almost no texture, only a base sand tone.
- **Macro** — large soft color patches with minimal detail; intended arcade candidate.
- **Fine** — still simplified, but with more small-scale variation than Macro.

## Approval question

Which direction best supports the intended arcade read:

1. clean ground with no blur/mud feeling,
2. no clash between the earth and crisp structures,
3. readable roads, buildings, rocks and canyon walls,
4. no accidental drift into a gritty Kenshi-like survival look?

If one style wins, use it as the ground-language baseline for **E1** (new persistent map foundation).
