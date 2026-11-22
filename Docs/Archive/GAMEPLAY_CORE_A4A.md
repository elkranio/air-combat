# A4a — Kestrel visual chassis and hardpoints (authoring only)

Baseline: `master` **53edaba**, A3d tested green. This atom creates a **visual chassis authoring path**, not PlayerJet/EnemyJet prefabs yet.

## Separation

- `PlayerJet` / `EnemyJet` (next atom): reusable ROLE/CONTROL roots. Their code owns input/AI, flight, boost, weapons, damage and mission-specific setup.
- `JetChassis` (this atom): reusable VISUAL prefab; mesh, animations and named mounts. No steering, physics, gun logic or damage inside the chassis.
- A pilot/AI should not be part of a chassis. One Kestrel chassis will eventually work with either root; enemy colors can be a visual/material variant, not a duplicated flight system.

`Assets/_Project/Scripts/Aircraft/JetChassis.cs` supplies six authored sockets: `Autocannon_L`, `Autocannon_R`, `Gauss`, `Missile`, `Engine_L`, `Engine_R` under `Hardpoints`. Future bombs and NURS are deliberately not invented before testing ground attack. The muzzle and engine socket coordinates are copied from the approved WeaponLab setup, respecting the existing `JetVisual` offset relative to the aircraft root. They are DATA on the visual prefab, not new runtime offsets.

## One-time asset creation in Unity

1. Apply A4a and allow Unity to compile; open the **existing** `Assets/_Project/Scenes/WeaponLab.unity` in **Edit Mode**.
2. Run **Air Combat > Prefabs > Author Kestrel Chassis** (this is a new one-time prefab-authoring command, NOT any old WeaponLab scene generator).
3. Confirm `Assets/_Project/Prefabs/Chassis/Kestrel.prefab` exists. Open its prefab stage and inspect six hardpoints. The asset contains mesh/art but **no AfterburnerVfx**, since that component requires the role's Afterburner.
4. Run WeaponLab Play Mode normally. Verify BURST/GAUSS/MISSILE/Break Turn and both aircraft colors still behave exactly as before. The existing scene is not converted to use this new asset yet.
5. Stage and commit `Kestrel.prefab`, its `.meta` and generated folder `.meta` files alongside the code once verified. The authoring command refuses to overwrite the asset on repeat runs.

**Important:** applying this patch alone does not create the .prefab, because only the Unity Editor can safely serialize the current scene mesh/subtree and asset references. The editor command produces the real Unity prefab without touching the test scene.

## Not in A4a

- No scene changes, no role prefabs yet, no change to hardcoded operational muzzle/nozzle offsets in the existing gameplay components, no damage/flight refactor, no ScriptableObject tuning, no radar/ground-attack code.
- A4b: author PlayerJet, EnemyJet and CombatCameraRig from existing green setup; bind the chassis hardpoints into emitters/VFX while retaining fallback until both test scenes pass. Scene-specific references are connected by composition, not stored as cross-scene references inside prefabs.
