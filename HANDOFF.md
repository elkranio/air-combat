# Air Combat — current handoff

2026-11-26 | Post CLEAN-04 baseline `8134ba9` (Unity 6000.6.10f1).

## Where we are

- Mission01 is the campaign priority. The authored Lynx sortie includes launch, terrain/boundary failure, tactical map, six live Pickets, three required Watchers, optional escaping Overlord, return navigation and assisted landing. `Landed` is implemented; a full debrief/reward/progression layer is not.
- CombatSandbox is the separate regression scene. Dogfight uses two Tigers and AAM; Ground Attack uses mobile Mantis and NURS; Interception uses Sparrow targets and AAM. Mode is a single enum, saved as Dogfight at the baseline.
- Shared flight session, scripted takeoff/landing, flight safety, objective state, target effects and HUD presentation have been separated. Prefabs are authored, not rebuilt by one-shot installers.
- CLEAN-03 removed the three obsolete scenes, legacy camera/weapon/input/AI paths and 37 imported asset candidates; CLEAN-04 migrated nine deprecated Unity search calls. The second Unity reachability report counted 516 imported paths and 42 deliberately retained non-reachable candidates (see [art pipeline](ART_PIPELINE.md)). No automatic deletion policy.
- Saved Mission01 editor overrides: Debug Air Start ON, Debug No Clip ON, Debug Landing Near Airfield OFF. Disable all debug shortcuts for normal regression. They are not intended as shipping rules.

## Next work (proposed, not committed design)

1. Run a normal end-to-end sortie with debug shortcuts off: launch, three Watchers, optional Overlord outcome, return and landing. Check threat placement, visibility and pacing.
2. Fix actual playtest friction; decide the mission-end/debrief experience before adding campaign progression.
3. Expand mission content and reusable threat/objective compositions only after the first sortie is fun. Keep the sandbox for weapon and enemy tests rather than mixing it into the campaign.

## Green gate

- Unity compilation and Console; inspect both scenes for missing scripts/references/materials.
- Mission01: launch, W/S/A/D, cannon, map pause/resume, terrain crash, boundary failure, SAM warning/cancel/hit, Watcher objective count, Overlord destruction AND escape, RTB capture and landing, R throughout.
- Sandbox: all three modes, independent Tiger behavior, AAM lock/launch/rearm, Mantis patrol/NURS, Sparrow interception, HUD/radar and R. H/7/8/9 remain test tools.
- Verify no unintended scene/prefab changes; commit a green after local Play Mode validation. Restore intentional development overrides after tests or explicitly commit the new baseline.

[Mission](Docs/MISSION_01.md) | [Architecture](GAMEPLAY_ARCHITECTURE.md) | [Combat](GAMEPLAY_COMBAT.md) | [History](Docs/Archive/README.md)
