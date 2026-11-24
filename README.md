# Air Combat

Unity 6.6 / URP. Stylized arcade air combat: strong silhouettes, responsive flight, readable threats and impactful destruction. Gameplay comes before simulation realism.

## Play the current build

Open `Assets/_Project/Scenes/ArcadeDesertE1.unity` and press Play. The current green uses **Ground Nurs Test** on `Air01Encounter`: AC-01 on LMB, NURS on RMB, mobile missile AA on the authored patrol. Disable Ground Nurs Test before Play to restore the two-Tiger air encounter and AAM secondary. These are separate test compositions, not a finished mission system.

A/D steer, W boost, S tight turn, LMB/RMB fire, R reset, H debug. Q/E reserved for future per-slot weapon selection.

## Current documentation

- [HANDOFF.md](HANDOFF.md) — current baseline, next atoms, regression gates.
- [GAMEPLAY_COMBAT.md](GAMEPLAY_COMBAT.md) — accepted weapons, enemies and player rules.
- [GAMEPLAY_ARCHITECTURE.md](GAMEPLAY_ARCHITECTURE.md) — current scene and component ownership.
- [ART_AUTHORING.md](ART_AUTHORING.md), [ART_PIPELINE.md](ART_PIPELINE.md), [THIRD_PARTY_ASSETS.md](THIRD_PARTY_ASSETS.md) — art contracts and attribution.
- [Docs/Archive](Docs/Archive/README.md) — historical iterations and superseded instructions; not installation instructions.
- [Cleanup record](Docs/CLEANUP_01.md) — removed editor/runtime code, asset scope, verification.

The main scene is hand-authored; never regenerate it. `WeaponLab` and `GroundAttackLab` remain regression scenes. Editable source art lives outside Unity in `Resources`; imported art lives in `Assets/_Project/Art`.
