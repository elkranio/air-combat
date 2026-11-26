# Air Combat

Unity 6000.6.10f1 / URP / Input System. Stylized arcade air combat: responsive flight, readable threats and impactful destruction. Gameplay before simulation realism.

## Open and play

Open the existing project in Unity Hub. Both authored scenes are already installed and enabled in Build Settings; **never regenerate either scene**.

| Scene | Purpose |
| --- | --- |
| `Assets/_Project/Scenes/Mission01_FirstSortie.unity` | Campaign slice: takeoff, six Picket SAMs, three Watchers, optional Overlord, RTB and assisted landing. First build scene. |
| `Assets/_Project/Scenes/CombatSandbox.unity` | Isolated combat regression: Dogfight, Ground Attack, Interception, Warden (current gun test). Select `Mode` on `CombatSandboxEncounter` before Play. |

**Mission testing:** the saved editor baseline has Debug Air Start and Debug No Clip enabled. To play a normal sortie, disable both on `Mission01LaunchSequence`; leave Debug Landing Near Airfield disabled. These bypasses are editor-only. Press W to take off.

| Input | Action |
| --- | --- |
| A/D | Steer |
| W / S | Afterburner / tight turn (shared fuel) |
| LMB | AC-01 cannon |
| RMB | Equipped secondary in sandbox (AAM or NURS); no secondary in Mission01 |
| Tab / Esc | Open / close tactical map in Mission01 |
| R | Retry / reset |
| H | Diagnostic flight legend |
| 7 / 8 / 9 | Sandbox altitude comparison (78 / 110 / 150) |

## Documentation

- [Handoff](HANDOFF.md) — current baseline, next work and regression gate.
- [Mission 01](Docs/MISSION_01.md) — mission rules, debug switches and authoring.
- [Combat](GAMEPLAY_COMBAT.md) — player, weapons and enemy behavior.
- [Architecture](GAMEPLAY_ARCHITECTURE.md) — runtime ownership and reusable prefabs.
- [Art pipeline](ART_PIPELINE.md), [model authoring](ART_AUTHORING.md), [lighting](LIGHTING_L1.md).
- [Third-party assets and audio provenance](THIRD_PARTY_ASSETS.md).
- [History](Docs/Archive/README.md) — superseded iterations and cleanup records, not current installation instructions.

Raw editable art stays under `Resources/` (outside Unity Assets); imported assets under `Assets/_Project/Art`; gameplay prefabs under `Assets/_Project/Prefabs`.
