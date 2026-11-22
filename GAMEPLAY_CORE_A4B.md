# A4b — role jet and combat camera prefab authoring

Baseline: green `master` **b4b899a** after A4a's Kestrel chassis export. Code-only additive prefab authoring; the existing WeaponLab and Sandbox scenes are **not edited**.

## Prefab composition

- `Aircraft/PlayerJet.prefab` — role root: `FlightController`, `Afterburner`, `WeaponController` (NOT the legacy `WeaponLabCannon`), `WeaponSelection`, `BoundedAim`, `BreakTurn`, `GuidedMissileLauncher`, `JetAssembly`. A `JetVisual` child contains the **nested** `Chassis/Kestrel.prefab` and the role-specific `AfterburnerVfx`.
- `Aircraft/EnemyJet.prefab` — role root: original green `EnemyFlightController`, `Afterburner`, `WeaponLabEvasion`, `WeaponLabEnemy` (temporarily Lab-specific for 5 HP, respawn and tint), `BreakTurn`, `JetAssembly`, collider. The nested Kestrel supplies the new visible renderers; external references to player are empty until scene composition.
- `Camera/CombatCameraRig.prefab` — `Camera`, `TopDownCameraFollow`, `CameraLookAhead`, `TargetLock`, `TargetLockHud`, and `CombatCameraRig` binder. Player and target references are deliberately empty; `BindPlayer` and `BindLockTarget` are used by the scene in A4c.
- `JetChassis` owns the 6 named sockets. `JetAssembly` points to the visual root and chassis; it does not own a pilot, damage rules or gameplay tuning. Future airframes replace the nested visual, not the role prefab.

## Operational socket support (fallback preserved)

`AutocannonEmitter`, `GaussEmitter` and `GuidedMissileLauncher` use chassis muzzle/mount positions on composed jets. `AfterburnerVfx` uses chassis nozzle sockets when provided. The currently running scene objects **have no JetAssembly**, so they retain original numeric offsets, effects and timing unchanged. GAUSS still fires along the aircraft ROOT forward axis, not the banked visual model.

`TopDownCameraFollow.Configure` allows a prefab camera whose target cannot be serialized across scenes to acquire the player and calculate correct height at runtime.

## One-time Unity Editor creation

1. Apply the patch and wait for compilation. **Keep** the `Kestrel.prefab` generated in A4a and its hardpoints. Open **WeaponLab.unity** in Edit Mode; never run the old WeaponLab scene generators.
2. Run **Air Combat > Prefabs > Author Jet Prefabs** (new command lives in `Assets/_Project/Editor/`). This copies the existing green aircraft and camera into a temporary Unity preview scene, replaces each copied JetVisual with a nested Kestrel, rewires internal references, then saves three new prefabs. The command refuses to overwrite any existing result.
3. Inspect all three assets in Prefab Mode. Confirm nested Kestrel icons, `JetAssembly` refs, player `WeaponController` settings/materials, enemy visible renderer list and collider, camera lock settings (1.0s / 0.5s) and **empty scene-target references**.
4. Reopen WeaponLab and test normal Play Mode (BURST/GAUSS/MISSILE, enemy 5 HP, Q/E, evasion, Sandbox camera). **These tests still run from the existing scene objects, not from the new prefabs.** The prefab prefabs are not a playable independent scene until A4c composition.
5. Stage all new `.prefab` / `.meta` assets, the scripts, and generated folder `.meta`. Push green after inspection. Do not save scene changes from the authoring pass.

## A4c next

Replace WeaponLab scene aircraft/camera with prefab instances **in a separate atomic patch**. Bind player/AI, camera, missile lock, HUD and encounter references through explicit scene composition. The existing session can still use its previous setup until A4c is green. A later atom can separate `WeaponLabEnemy`'s lab-only HP/respawn policy from general damage handling.
