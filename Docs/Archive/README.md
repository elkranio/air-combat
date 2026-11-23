# Archive — historical implementation atoms

This folder intentionally holds **superseded implementation notes**, one-time installer recipes and prior architecture/handoff snapshots. They are useful for reconstructing decisions when a chat ends abruptly, but their numbers and setup commands may be obsolete. **Do not treat them as current gameplay instructions.**

Live entry points are [`../../README.md`](../../README.md), [`../../HANDOFF.md`](../../HANDOFF.md), [`../../GAMEPLAY_COMBAT.md`](../../GAMEPLAY_COMBAT.md) and [`../../GAMEPLAY_ARCHITECTURE.md`](../../GAMEPLAY_ARCHITECTURE.md).

- `GAMEPLAY_CORE_G1/G2/G3/G4A/G4B/G4C/G5.md`: progression from scene/depot setup through bombs, cabrage and forward direct NURS. Current summary is `GAMEPLAY_COMBAT.md` at repository root.
- `GAMEPLAY_AA1.md`, `GAMEPLAY_SAM_M1*.md`, `GAMEPLAY_RADAR_R1.md`, `GAMEPLAY_HUD_H1.md`: individual AA/SAM/radar/HUD development and diagnosis, including superseded tuning/installer instructions.
- `*_PRE_DOCS_CLEANUP_20261123.md`: exact historical root README, HANDOFF and architecture before this pass.
- `EditorInstallers/*.cs.txt` + `.meta.txt`: historical one-time Unity Editor authors for accepted AA1, SAM and H1 prefabs. They are deliberately **outside `Assets/`** so they no longer compile or expose stale installation commands in the Unity menu. If ever needed, restore intentionally with their original `.meta` GUIDs and review against the then-current scene/prefabs.
- Earlier archived `PRE_C1`, `PRE_C2`, weapon-lab, lighting and design transition notes remain in place.

**Rule for later cleanups:** keep one compact live specification per area and one fresh handoff; archive each old `GAMEPLAY_*.md` once its actual behavior has been reconciled into live docs. Keep code changes in Git and playable prefabs committed. Old archive Markdown links may refer to their original root paths.
