# Air Combat

Top-down arcade air combat prototype. Unity 6.6 / URP.

## Start here

- `Assets/_Project/Scenes/WeaponLab.unity` — **current combat prototype**: 12 × 12 km range, one flying 5-HP opponent (respawns), BURST autocannon and fixed-forward Gauss. **Open the existing scene; do not rerun the old V0 generator** (`ConfigureWeaponLab.cs`), which recreates passive dummies and may overwrite the current setup. Not a build scene.
- `Assets/_Project/Scenes/Sandbox.unity` — gameplay reference: aircraft controls, banking, afterburner, enemy pursuit, target lock, missiles and respawn. The build scene remains Sandbox.
- `Assets/_Project/Scenes/FrontierStrata.unity` — active visual playground (V17): 1200 × 1200 badlands, integrated rocky relief, industrial ruins, dynamic shadows, free flight. No combat yet.
- `Assets/_Project/Scenes/FrontierBakeTruth.unity` — V18 material bake diagnostic; C switches close/gameplay camera.
- `Assets/_Project/Scenes/ScorchedFrontier.unity` — V13 visual fallback.

**Weapon Lab (current):** WASD fly, Space boost, **Q/E break turn** (full boost tank), LMB fire, **1 BURST / 2 GAUSS / 3 MISSILE**, B enemy evasion A/B, R reset, H HUD. Camera pitch fixed at 65 degrees with look-ahead always on. BURST has a persistent +/-12-degree aim wedge; Gauss has a short fixed-forward sight. Both sights are pale gray-white. Missile mode has no minimum lock range or safety circle; acquire for 1.0 s, tolerate 0.5 s off-target and click LMB to launch the homing rocket. See the [current Weapon Lab guide](Tools/Unity/WEAPON_LAB.md) for approved tuning and pending ideas.

**Sandbox/visual playgrounds:** WASD fly, Space boost; Sandbox retains the shared 1/2/3 camera pitch presets (90/75/65 degrees). Frontier has its separate R/H controls. The only build scene is Sandbox.

## Documentation

- [HANDOFF.md](HANDOFF.md) — active decisions and next steps; read first when switching chats.
- [GAMEPLAY_ARCHITECTURE.md](GAMEPLAY_ARCHITECTURE.md) — active dependency map, reusable gameplay boundaries and migration sequence.
- [GAMEPLAY_CORE_A3D.md](GAMEPLAY_CORE_A3D.md) — common weapon controller, projectile damage/air-ground contract and guided missile launcher migration.
- [GAMEPLAY_CORE_A3C.md](GAMEPLAY_CORE_A3C.md) — extracted BURST/GAUSS emitters, migration notes and regression checks.
- [GAMEPLAY_CORE_A3B.md](GAMEPLAY_CORE_A3B.md) — BURST/GAUSS cadence extraction, temporary legacy adapter and test checklist.
- [ART_PIPELINE.md](ART_PIPELINE.md) — asset layout, generated outputs and cleanup rules.
- [ART_AUTHORING.md](ART_AUTHORING.md) — **how to model, bake, import and evaluate art** (V18–V20 validated playbook).
- [Tools/Unity/WEAPON_LAB.md](Tools/Unity/WEAPON_LAB.md) — isolated autocannon lab, build and controls.
- [Tools/Unity/ARTLAB.md](Tools/Unity/ARTLAB.md) — current visual scenes, bake test, cleanup and archived studies.
- [THIRD_PARTY_ASSETS.md](THIRD_PARTY_ASSETS.md) — asset attribution.

The previous forest art experiments are retired from the active project, preserved in Git at `3aae3d12b84e5276a333060fdd1480828aa17f2c`. Never rewrite history just for visual iteration cleanup.
