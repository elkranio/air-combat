# Air Combat — current handoff

Updated 2026-11-23. **Cleanup A GREEN**: `ArcadeCameraRig.prefab` and `ArcadeCameraFollow` are committed and used by the main map. **Cleanup B** retires art experiments and one-time installers with dependency guards; follow [Cleanup B verification](Docs/Production/CLEANUP_B.md) before calling this cleanup green.

## Active production

- **Main:** `Assets/_Project/Scenes/ArcadeDesertE1.unity`, one persistent **2000×2000** editable Unity Terrain with muted nearly uniform ground (`#B8A993`), an approved approximately **430×275×30 m** plateau, and scene-specific dark blue-grey Kestrel paint. The first stable cruising altitude is **Y150**; `7/8/9` remain temporary Y78/Y110/Y150 comparisons. `R` resets, `A/D` steer, `W` boosts, `Q/E` break-turn, `H` hides overlay.
- **Camera:** dedicated production `Assets/_Project/Prefabs/Camera/ArcadeCameraRig.prefab`: fixed-world yaw **-45°**, pitch **50°**, ortho **80**, heading look-ahead **48**, boost extra **14**, ~**0.42s** lead damping, safe viewport margin **20%**. No old C1 rotation mode on main map.
- **Lighting:** `CombatLightingRig.prefab` is the only sun; the map uses its own `Assets/_Project/Settings/ArcadeCombat_URP.asset` candidate (**600** shadow distance, **4** cascades, **4096** shadow atlas). Earlier aircraft-shadow loss on ground/screen edges motivated this change. **Do not claim shadow-range visual acceptance without testing**; shadow geometry can still leave camera view. WeaponLab/GroundAttackLab use the unchanged existing rig/profile.
- **World scale:** landforms, not pebble props, make the geography. One low plateau is an accepted module; stacked/higher formations may eventually become flyover hazards or collision walls. No extra plateaus, roads or canyon pass before the first base-style test.
- **Current next task B1a:** [Barracks row](Docs/Production/BARRACKS_B1A.md) on the existing map. First prepare one reusable 32×12×6m barracks model and prefab, then place **three in a row** near **(-485,-270)**, keep plateau at **(110,135)**, and move Y150 flight spawn to **(-362,-393)** facing the barracks. One-time installer is provided by patch; Unity Game view/flight **not yet approved**. No road/warehouse/vehicles/airstrip until first asset passes form, scale, color, shadow review. [Overall base plan](Docs/Production/BASE_DESIGN.md).

## Combat proof retained (not integrated into main map yet)

- `WeaponLab`: player vs fighter, BURST (1), GAUSS (2), guided A2A missile (3).
- `GroundAttackLab`: bombs (4, four drops), committed cabrage NURS (5, six rockets), direct NURS (6, six shots). AA1 cannon, vertical-launch SAM1, player HP/fuel and RadarHud. Player A/D, W boost (cruise 70, boost 140, fuel 2.5s), Q/E Break Turn, R reset. For gameplay details and serialized values see [GAMEPLAY_COMBAT.md](GAMEPLAY_COMBAT.md).
- Air and ground combat work is **mechanical proof-of-concept**. Eventually tune weapon responsiveness, impacts, sound, tracers, breakup, debris, smoke, explosions and ground contact VFX for the stylized arcade look. Do **not** balance or rewrite combat during B1 art production.
- Known design debt: direct NURS currently feels too long-range; cabrage remains difficult but intentionally distinct. `GroundDepotTarget` destruction still uses a provisional runtime mesh cut.

## Architecture and safety

- Use existing prefabs: `PlayerJet`, `EnemyJet`, `Kestrel`, `ArcadeCameraRig`, legacy `CombatCameraRig` (still used in the labs), AA1, SAM1 launcher/missile, `MilitaryDepot`, `CombatHUD`, `CombatLightingRig`.
- `ArcadeDesertE1` currently runs `E1MapFlightSession` **without combat**. `WeaponLabSession` and `GroundAttackLabSession` retain their self-contained setups. The eventual combined mission needs deliberate scene wiring rather than moving scene objects blindly.
- Keep Unity `.meta` GUIDs stable. Use original authored/Blender sources for live assets. Runtime and prefab references take precedence over obsolete docs or C# default values.
- After B cleanup, validate **main map** flight and shadow/plateau, **WeaponLab** air weapons, **GroundAttackLab** all six weapons/AA/SAM/HUD. Deleting old scenes is not a substitute for Play Mode.

## Working rules

Build one approved atom at a time; send a `.patch` with check/apply commands; test in Unity; green push. Update this handoff and the active plan when an artistic or technical decision is accepted. Keep old experiments in Git history, not as production source or dozens of competing Markdown plans.
