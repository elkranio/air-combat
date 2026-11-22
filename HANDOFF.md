# Air Combat — current handoff (post-C1)

## F1 — constant cruise / W boost (green 2026-11-22)

PlayerJet prefab: `FlightController.useFixedCruiseWBoost=true`, `cruiseSpeed=70` (the former W-held speed). A/D steer, W-held Afterburner to 110, Q/E Break Turn; S/Space unused. Normal flight remains at 70 (including with no keyboard input); fuel, turn rate, bank and boost acceleration unchanged. The setting is **false by default** in the shared FlightController, so Sandbox and visual playgrounds retain original W/S throttle and Space boost. Both WeaponLab and GroundAttackLab use PlayerJet. F1 flight feel tested green: cruise 70, W boost to 110. The user also briefly tried 140 boost speed but explicitly kept 110 for now. No change to fuel, turn rates or Sandbox input.

## Current green baseline

Latest verified green is **`6ff85069` on 2026-11-22**: G2 MilitaryDepot imported/baked/placed, followed by F1 cruise/W-boost control changes. `WeaponLab.unity` remains the authoritative prefab-composed air-combat regression scene. G3 is authored against this green but is **not yet Unity-tested**.

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

## G1/G2 — GroundAttackLab and Military Depot (green)

G1 adds an independently composed `GroundAttackLab` scene from `PlayerJet` and `CombatCameraRig`, without `WeaponLabSession`. Scene authoring is under **Air Combat > Scenes > Create GroundAttackLab G1**, with a flat 12 km test plane, a directional light, and runtime `GroundAttackLabSession` binder. No enemy/ground target/ordnance; BURST/GAUSS shoot into empty space and MISSILE has no target. Authoring leaves WeaponLab and Sandbox untouched. [G1 notes](GAMEPLAY_CORE_G1.md).

**G2 (tested green):** modeled MilitaryDepot in `Tools/Blender/military_depot_geometry.py`. `generate_military_depot.py` projects roof normal + local AO using existing V18 Cycles selected-to-active helper; importer `Assets/_Project/Editor/ImportMilitaryDepot.cs` creates a target prefab and 4 scene instances. No damage/ordnance yet. Source and verification: [GAMEPLAY_CORE_G2.md](GAMEPLAY_CORE_G2.md). **G3 pending local Unity verification:** four-release gravity bomb burst + predicted impact corridor + destructible depot parts; after green consider G4 NURS. Do not invent radar or loadouts yet. A chassis ScriptableObject waits for a second airframe.

## G3 — Gravity bombs (pending local Unity test)

G3 adds `BombRack` (four physical bombs, 0.25s separation, 3s cooldown after last shot), `BombTrajectory` (gravity, horizontal velocity inheritance, drag, prediction), a per-bomb explosion, and `BombAimIndicator` (four predicted impacts and boost-widened release corridor). **4** selects bombs only in GroundAttackLab. A/D and W work during the burst; a successful Q/E sacrifices remaining bombs and imposes cooldown; slot switching is locked until the last bomb leaves. Active W/afterburner introduces random release velocity error. No weapon, AI, camera or shared prefab art rework; Sandbox still uses its old throttle controls.

Each G2 depot gets runtime `GroundDepotTarget` with independent bunker (3HP), two tanks (1HP each), crates (2HP). The combined-by-material G2 FBX is visually trimmed by cloning meshes and removing triangles in destroyed zones; this is an **approximate G3 test**, not a finished fracture effect. G3 setup happens in the GroundAttackLab session at Play Mode; never rerun G2's generator. See [G3 test plan](GAMEPLAY_CORE_G3.md). After green: adjust feel, attack VFX and later G4 NURS.

## Workflow

User tests each small atom in Unity, pushes green, then author the next atom against fresh `master`. Provide `.patch` with `git apply --check --verbose` and `git apply` commands. No monolithic scene regenerators and no rewriting Git history. For historical decisions see `Docs/Archive/`, especially the old handoff and Gameplay Core A3/A4 logs.
