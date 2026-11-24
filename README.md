# Air Combat

Unity 6.6 / URP. Stylized arcade air combat: strong silhouettes, responsive flight, readable threats and impactful destruction. Gameplay comes before simulation realism.

## Play the current build

Open `Assets/_Project/Scenes/ArcadeDesertE1.unity` and press Play. The current green uses **Ground Nurs Test** on `Air01Encounter`: AC-01 on LMB, NURS on RMB, mobile missile AA on the authored patrol. Disable Ground Nurs Test before Play to restore the two-Tiger air encounter and AAM secondary. These are separate test compositions, not a finished mission system.

**CM-03:** the main scene starts in Interception Test (takes priority over Ground Nurs Test). Ten Sparrow targets fly individually offset straight routes. AC-01 on LMB, AAM on RMB, R restarts, no mission objective. Turn Interception Test off to restore the original ground/air selector. See [interception playground](Docs/CM_02.md).

A/D steer, W boost, S tight turn, LMB/RMB fire, R reset, H debug. Q/E reserved for future per-slot weapon selection.

To add another mobile AA, drag `Assets/_Project/Prefabs/Ground/AA02_Mantis_Combat.prefab` into the main scene and assign a scene route in its `GroundPatrolMotor` Inspector. See [authoring instructions](GAMEPLAY_ARCHITECTURE.md#add-mobile-aa-to-the-main-map).

Player health, radar and weapon module settings live on `Assets/_Project/Prefabs/Aircraft/PlayerKestrel_Combat.prefab`. The original PlayerJet is retained for the legacy labs; the main map already uses the combat prefab.

Tiger behavior settings live on `Assets/_Project/Prefabs/Aircraft/EF01_Tiger_Fighter.prefab` (`ArcadeFighterUnit`), not on the scene encounter. The current air test still spawns two Tigers automatically.

## Current documentation

- [HANDOFF.md](HANDOFF.md) — current baseline, next atoms, regression gates.
- [GAMEPLAY_COMBAT.md](GAMEPLAY_COMBAT.md) — accepted weapons, enemies and player rules.
- [GAMEPLAY_ARCHITECTURE.md](GAMEPLAY_ARCHITECTURE.md) — current scene and component ownership.
- [ART_AUTHORING.md](ART_AUTHORING.md), [ART_PIPELINE.md](ART_PIPELINE.md), [THIRD_PARTY_ASSETS.md](THIRD_PARTY_ASSETS.md) — art contracts and attribution.
- [Docs/Archive](Docs/Archive/README.md) — historical iterations and superseded instructions; not installation instructions.
- [Cleanup record](Docs/CLEANUP_01.md) — removed editor/runtime code, asset scope, verification.

The main scene is hand-authored; never regenerate it. `WeaponLab` and `GroundAttackLab` remain regression scenes. Editable source art lives outside Unity in `Resources`; imported art lives in `Assets/_Project/Art`.
