# Air Combat

Unity 6.6 / URP arcade air-combat prototype. **Committed scenes, prefabs and their Inspector values are the current source of truth.** The root documents describe the current game; dated implementation atoms are preserved in [`Docs/Archive`](Docs/Archive/README.md).

## Where to start

1. [HANDOFF.md](HANDOFF.md) — current state, open issues, next playtest and regression checklist. **Read this first when starting a new chat.**
2. [GAMEPLAY_COMBAT.md](GAMEPLAY_COMBAT.md) — controls, six weapon slots, ground threats, real gameplay numbers and tuning.
3. [GAMEPLAY_ARCHITECTURE.md](GAMEPLAY_ARCHITECTURE.md) — ownership, scene wiring, prefab contracts and safe change boundaries.
4. [ART_PIPELINE.md](ART_PIPELINE.md) / [ART_AUTHORING.md](ART_AUTHORING.md) — art assets and model/bake workflow; [LIGHTING_L1.md](LIGHTING_L1.md) — shared URP lighting; [THIRD_PARTY_ASSETS.md](THIRD_PARTY_ASSETS.md) — licenses.
5. [Docs/Art/ENVIRONMENT_MAP_PLAN.md](Docs/Art/ENVIRONMENT_MAP_PLAN.md) — **approved hybrid map direction**. [Docs/Art/CAMERA_C1_COMBAT.md](Docs/Art/CAMERA_C1_COMBAT.md) — **accepted C3 fixed-world 50° + forward look-ahead**. [Docs/Art/ARCADE_VISUAL_DIRECTION.md](Docs/Art/ARCADE_VISUAL_DIRECTION.md) — **current stylized arcade art pillars**. [Docs/Art/ARCADE_DESERT_E1.md](Docs/Art/ARCADE_DESERT_E1.md) — **active E1 big-map first flight**. [ENVIRONMENT_ART_RESTART.md](ENVIRONMENT_ART_RESTART.md) keeps earlier art experiments.

## Playable/reference scenes

| Scene | Purpose |
| --- | --- |
| `Assets/_Project/Scenes/WeaponLab.unity` | Air combat: jet vs. fighter, BURST / GAUSS / air-to-air MISSILE |
| `Assets/_Project/Scenes/GroundAttackLab.unity` | Ground attack: depots, bombs, cabrage, direct NURS, AA1, SAM1, shared CombatHUD |
| `Assets/_Project/Scenes/Sandbox.unity` | Legacy comparison; currently the only Build Settings entry, not a template for new combat scenes |
| `Assets/_Project/Scenes/FrontierStrata.unity` | V17 environmental art reference, not the combat regression |
| `Assets/_Project/Scenes/FrontierPracticalBake.unity` | V19 reference for readable roads/industrial layout/rocks; old art lighting |
| `Assets/_Project/Scenes/FrontierCameraC1.unity` | C1 camera experiment (visually promising; 50° preferred) |
| `Assets/_Project/Scenes/FrontierCameraCombatC1.unity` | **Accepted C3 camera combat regression**; six weapons, fighter, AA1/SAM1, radar and historical E1a `T` ground test. |
| `Assets/_Project/Scenes/ArcadeDesertE1.unity` | **NEW map foundation after one-time E1 authoring**: vivid 2×2 km flat-color Unity Terrain, sparse rocks, existing PlayerJet, C3 and shared daylight/real shadows. Art/flight only. |

Controls in the two combat labs: **A/D** turn, **W** boost, **Q/E** emergency Break Turn, **R** reset, **H** diagnostics (hidden by default), **LMB** fire/commit. WeaponLab: **1–3**. GroundAttackLab: **1–6** (4 bombs, 5 cabrage rockets, 6 direct NURS). Sandbox retains its legacy W/S + Space controls.

## Development workflow

One approved atom -> `.patch` -> `git apply --check --verbose` -> `git apply` -> Unity Play Mode -> green push. Document each new atom so an interrupted chat can restart without guessing. Periodically fold **current** behavior into HANDOFF / GAMEPLAY_COMBAT / ARCHITECTURE and move the per-atom notes to `Docs/Archive` rather than deleting history.

**Do not rerun old AA1 / HUD / SAM1 installation or SAM1b upgrade commands.** They were single-use prefab authoring; their source is archived outside `Assets` so the menu entries are intentionally gone. Keep original Unity prefab and `.meta` GUIDs. The actual player air-to-air missile is distinct from the enemy SAM missile.
