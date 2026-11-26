# Air Combat

Unity 6000.6.10f1 / URP / Input System. Stylized arcade air combat: responsive flight, readable threats and impactful destruction. Gameplay before simulation realism.

## Open and play

Open the existing project in Unity Hub. Three authored scenes already exist; **never regenerate them**. Mission01 and CombatSandbox are enabled in Build Settings. Mission02 is the active editor-only work in progress.

| Scene | Purpose |
| --- | --- |
| `Assets/_Project/Scenes/Mission01_FirstSortie.unity` | Campaign slice: takeoff, six Picket SAMs, three Watchers, optional Overlord, RTB and assisted landing. First build scene. |
| `Assets/_Project/Scenes/CombatSandbox.unity` | Isolated combat regression: Dogfight, Ground Attack, Interception, Warden and Convoy. Select `Mode` on `CombatSandboxEncounter` before Play. |
| `Assets/_Project/Scenes/Mission02_ConvoyInterdiction.unity` | Active WIP: convoy interception, Warden escorts, C1/C2 canyon cargo sites and airfield-side Carryall tests. Open directly in the editor. |

**Mission01 testing:** the saved editor baseline has Debug Air Start and Debug No Clip enabled. To play a normal sortie, disable both on `Mission01LaunchSequence`; leave Debug Landing Near Airfield disabled. These bypasses are editor-only. Press W to take off.

| Input | Action |
| --- | --- |
| A/D | Steer |
| W / S | Afterburner / tight turn (shared fuel) |
| LMB | AC-01 cannon |
| RMB | Equipped secondary (sandbox AAM/NURS; M02 NURS); no secondary in Mission01 |
| Tab / Esc | Open / close tactical map in Mission01 |
| R | Retry / reset |
| H | Diagnostic flight legend |
| 7 / 8 / 9 | Sandbox altitude comparison (78 / 110 / 150) |

**Current work / M02:** open the saved scene and Play. Keep `M02 / CA01 flight preview` near the airfield; use `Start Airborne` on `Mission02RouteTrial` for quick flybys. K triggers a preview crash, L cycles Pickup / Empty hover / Loaded hover, R resets, Tab pauses. These are temporary test controls, not mission gameplay. See [CA05](Docs/CA_05_DESTRUCTION_PREVIEW.md) and the [handoff](HANDOFF.md) for the next pass.

## Documentation

- [Handoff](HANDOFF.md) — current baseline, next work and regression gate.
- [Mission 01](Docs/MISSION_01.md) — mission rules, debug switches and authoring.
- [Mission 02 — current status](HANDOFF.md#mission02--current-work) — start here; [V06 layout](Docs/MISSION_02_LAYOUT_V06.md), [original route graybox](Docs/MISSION_02_ROUTE_TRIAL.md) (historical setup, do not rerun).
- [Convoy art](Docs/CV_01_PICKET_MOBILE.md), [march](Docs/CV_02_CONVOY_MARCH.md), [Warden escort](Docs/CV_03_WARDEN_ESCORT.md) — earlier sandbox atoms, not the complete M02 rules.
- [CG01 cargo](Docs/CG_01_SPECIAL_CARGO.md), [CA01 Carryall](Docs/CA_01_CARRYALL.md), [CA02 rotors](Docs/CA_02_FLIGHT_PREVIEW.md), [CA03 pickup](Docs/CA_03_CARGO_PICKUP.md), [CA04 effects](Docs/CA_04_PICKUP_VFX.md), [CA05 destruction](Docs/CA_05_DESTRUCTION_PREVIEW.md).
- [Combat](GAMEPLAY_COMBAT.md) — player, weapons and enemy behavior.
- [Architecture](GAMEPLAY_ARCHITECTURE.md) — runtime ownership and reusable prefabs.
- [Art pipeline](ART_PIPELINE.md), [model authoring](ART_AUTHORING.md), [lighting](LIGHTING_L1.md).
- [Third-party assets and audio provenance](THIRD_PARTY_ASSETS.md).
- [History](Docs/Archive/README.md) — superseded iterations and cleanup records, not current installation instructions.

Raw editable art stays under `Resources/` (outside Unity Assets); imported assets under `Assets/_Project/Art`; gameplay prefabs under `Assets/_Project/Prefabs`.
