# Air Combat

Top-down arcade air combat prototype. Unity 6.6 / URP.

## Start here

- `Assets/_Project/Scenes/WeaponLab.unity` — **generated scene**, isolated 12 × 12 km cannon/weapon feel-test range, no enemies. Build with `Tools/Unity/ConfigureWeaponLab.cs` (see guide); not a build scene.
- `Assets/_Project/Scenes/Sandbox.unity` — gameplay reference: aircraft controls, banking, afterburner, enemy pursuit, target lock, missiles and respawn. The build scene remains Sandbox.
- `Assets/_Project/Scenes/FrontierStrata.unity` — active visual playground (V17): 1200 × 1200 badlands, integrated rocky relief, industrial ruins, dynamic shadows, free flight. No combat yet.
- `Assets/_Project/Scenes/FrontierBakeTruth.unity` — V18 material bake diagnostic; C switches close/gameplay camera.
- `Assets/_Project/Scenes/ScorchedFrontier.unity` — V13 visual fallback.

Play Mode: WASD to fly, Space to boost, 1/2/3 camera pitch (90/75/65 degrees). Frontier additionally has R recenter and H legend toggle.

## Documentation

- [HANDOFF.md](HANDOFF.md) — active decisions and next steps; read first when switching chats.
- [ART_PIPELINE.md](ART_PIPELINE.md) — asset layout, generated outputs and cleanup rules.
- [ART_AUTHORING.md](ART_AUTHORING.md) — **how to model, bake, import and evaluate art** (V18–V20 validated playbook).
- [Tools/Unity/WEAPON_LAB.md](Tools/Unity/WEAPON_LAB.md) — isolated autocannon lab, build and controls.
- [Tools/Unity/ARTLAB.md](Tools/Unity/ARTLAB.md) — current visual scenes, bake test, cleanup and archived studies.
- [THIRD_PARTY_ASSETS.md](THIRD_PARTY_ASSETS.md) — asset attribution.

The previous forest art experiments are retired from the active project, preserved in Git at `3aae3d12b84e5276a333060fdd1480828aa17f2c`. Never rewrite history just for visual iteration cleanup.
