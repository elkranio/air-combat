# Air Combat

Unity 6000.6.10f1 / URP / Input System. Stylized top-down arcade jet combat: responsive flight, readable threats, tactical sorties and authored missions. Gameplay before simulation realism.

**Start here: [HANDOFF.md](HANDOFF.md).** M01/M02 gameplay slices accepted for now; current development is M03's interceptor in CombatSandbox (IN02). Final balance, radio, campaign results and polish are deferred.

## Open and play

Open the existing project in Unity Hub. Never regenerate authored scenes. M01 and Sandbox are enabled in Build Settings; M02 remains editor-only. No M03 scene yet.

| Scene under `Assets/_Project/Scenes/` | Current use |
| --- | --- |
| `CombatSandbox.unity` | Active IN02 single interceptor test. Mode = Interceptor (5). Existing Kestrel, AC01 + AAM. R retries. |
| `Mission01_FirstSortie.unity` | Lynx, three Watchers, optional Overlord, six Pickets, RTB/assisted landing. Turn off mission debug shortcuts for normal play. |
| `Mission02_ConvoyInterdiction.unity` | Lynx, six mobile Pickets, two Wardens, C1/C2 optional cargo evacuation. Saved Base Assault Trial is ON; turn it OFF for the full sortie. |

| Input | Action |
| --- | --- |
| A/D | Steer |
| W / S | Afterburner / tight turn (shared fuel); W starts scripted takeoff in campaign |
| LMB / RMB | AC01 / equipped secondary (AAM or NURS; none in M01) |
| Tab / Esc | Tactical map open / close where installed |
| R / H | Retry / diagnostic legend |
| 7 / 8 / 9 | Sandbox altitude comparison (78 / 110 / 150) |

## Active documentation

- [Handoff](HANDOFF.md) — read first for each new development chat.
- [M01](Docs/MISSION_01.md) · [M02](Docs/MISSION_02.md) · [M03 working design](Docs/MISSION_03.md).
- [IN02 interceptor trial](Docs/IN_02_INTERCEPTOR_PASS.md) — current implementation, visual selection, tuning and next playtest.
- [Combat](GAMEPLAY_COMBAT.md) · [Architecture](GAMEPLAY_ARCHITECTURE.md).
- [Art pipeline](ART_PIPELINE.md) · [Model authoring](ART_AUTHORING.md) · [Lighting](LIGHTING_L1.md) · [Third-party assets](THIRD_PARTY_ASSETS.md).
- [Historical snapshots](Docs/Archive/README.md) — retired atom logs and old installer instructions, not current setup.

Raw editable art in `Resources/`; generators in `Tools/Blender/`; imported runtime art in `Assets/_Project/Art/`; reusable prefabs in `Assets/_Project/Prefabs/`.
