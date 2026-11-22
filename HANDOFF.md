# Air Combat — current handoff (post-C1)

## Current green baseline

The latest verified gameplay state is A4c, pushed at **`e60a5c3` on 2026-11-22**. Cleanup C1 is an asset/documentation-only migration. Its renamed canonical `Assets/_Project/Scenes/WeaponLab.unity` contains the same prefab-composed scene previously named `WeaponLabPrefabs.unity`. This requires a fresh local Unity compile and Play Mode smoke test before committing C1 as green.

**Build Settings:** `Sandbox.unity` remains the only build scene; don't alter this incidentally. `FrontierStrata.unity` is the visual map baseline. Other Frontier/bake test scenes are deliberately retained for art, not gameplay. `WeaponLab.unity` is the authoritative air-combat regression scene.

## What was shipped

- Flight: keyboard-driven movement and bank; afterburner; Q/E real Break Turn; enemy pursuit/evasion.
- Combat: 4-shot BURST, fixed GAUSS, physical guided missile with cursor lock, hit/damage contract, 5-HP respawning fighter, kill/shot/hit diagnostic counters.
- Reusable prefabs: `PlayerJet`, `EnemyJet`, `CombatCameraRig`, nested `Kestrel` chassis and six named hardpoints.
- Prefab-composed WeaponLab: scene binds the player, enemy and camera via `WeaponLabSession`; new GroundAttackLab must bind its own scene relationships.
- Separate Sandbox comparison: original flight/camera/missile path remains intact.

Approved gun/camera/maneuver numbers and the live dependency map are in [GAMEPLAY_ARCHITECTURE.md](GAMEPLAY_ARCHITECTURE.md).

## C1 cleanup contract

- The original hand-built WeaponLab was removed after preserving its last green version in Git (`e60a5c3`). New prefab-composed `WeaponLabPrefabs.unity` was moved to `WeaponLab.unity` **together with its `.meta`**, preserving the converted scene's GUID.
- Removed the `WeaponLabCannon` compatibility script, passive dummy target and their `.meta` files only after checking remaining scene/prefab references.
- Removed obsolete WeaponLab editor generators and one-shot prefab/scene authoring commands; the **prefabs are now the source of truth**.
- Archived prior migration documents and handoff into `Docs/Archive/`. All deleted assets also remain recoverable through Git history.
- No gameplay logic, prefab content, active visual scene, art asset, rendering pipeline or Sandbox build setting changed.

## Local verification before marking green

1. Apply C1 patch; run the cleanup shell script in Git Bash **with Unity closed**. The script checks baseline HEAD, working tree and obsolete GUID references. It refuses unsafe deletions.
2. Open Unity and load `Assets/_Project/Scenes/WeaponLab.unity`: verify scene references, both prefab role roots, nested Kestrel, camera, renderers, weapon materials and no Missing Scripts.
3. Play: BURST cadence and alternating flashes; GAUSS single click and cooldown; MISSILE 1.0-second lock / 0.5-second grace / reacquire; enemy AI evade and 5-HP respawn; both Break Turns, boost recharge, R/H controls and camera look-ahead.
4. Open Sandbox: check flight, camera pitch 1/2/3, missiles. Check `ProjectSettings/EditorBuildSettings.asset` still lists only Sandbox.
5. `git status --short` and `git diff --cached --stat`; commit and push only if clean in Unity.

**Do not run retired `ConfigureWeaponLab*` generators**; they previously rebuilt old passive-target setups. If a scene breaks, Git can recover `e60a5c3` without reconstructing from tools.

## G1 — GroundAttackLab (pending local Unity test)

G1 adds an independently composed `GroundAttackLab` scene from `PlayerJet` and `CombatCameraRig`, without `WeaponLabSession`. Scene authoring is under **Air Combat > Scenes > Create GroundAttackLab G1**, with a flat 12 km test plane, a directional light, and runtime `GroundAttackLabSession` binder. No enemy/ground target/ordnance; BURST/GAUSS shoot into empty space and MISSILE has no target. Authoring leaves WeaponLab and Sandbox untouched. [G1 notes](GAMEPLAY_CORE_G1.md).

**G2 (pending Blender/Unity verification):** modeled MilitaryDepot in `Tools/Blender/military_depot_geometry.py`. `generate_military_depot.py` projects roof normal + local AO using existing V18 Cycles selected-to-active helper; importer `Assets/_Project/Editor/ImportMilitaryDepot.cs` creates a target prefab and 4 scene instances. No damage/ordnance yet. Source and verification: [GAMEPLAY_CORE_G2.md](GAMEPLAY_CORE_G2.md). **After G2 green:** G3 first gravity bomb, impact prediction and ground damage; then G4 NURS. Do not invent radar or loadouts yet. A chassis ScriptableObject waits for a second airframe.

## Workflow

User tests each small atom in Unity, pushes green, then author the next atom against fresh `master`. Provide `.patch` with `git apply --check --verbose` and `git apply` commands. No monolithic scene regenerators and no rewriting Git history. For historical decisions see `Docs/Archive/`, especially the old handoff and Gameplay Core A3/A4 logs.
