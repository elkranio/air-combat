# Air Combat — current handoff

Updated 2026-11-23. **Cleanup A/B GREEN.** Current art slice is **B1d road + prepared ground** between the accepted B1c HQ and three B1b barracks. Keep accepted scene/prefab GUIDs and do not resurrect retired art studies.

## Active production

- **Main:** `Assets/_Project/Scenes/ArcadeDesertE1.unity`, one persistent **2000×2000** editable Unity Terrain with muted nearly uniform ground (`#B8A993`), an approved approximately **430×275×30 m** plateau, and scene-specific dark blue-grey Kestrel paint. **Default cruising altitude is Y78** (after B1c install); `7/8/9` remain temporary Y78/Y110/Y150 comparisons. `R` resets to the selected trial altitude; `A/D` steer, `W` boosts, `Q/E` break-turn, `H` hides overlay.
- **Camera:** dedicated production `Assets/_Project/Prefabs/Camera/ArcadeCameraRig.prefab`: fixed-world yaw **-45°**, pitch **50°**, ortho **80**, heading look-ahead **48**, boost extra **14**, ~**0.42s** lead damping, safe viewport margin **20%**. No old C1 rotation mode on main map.
- **Lighting:** `CombatLightingRig.prefab` is the only sun; the map uses its own `Assets/_Project/Settings/ArcadeCombat_URP.asset` candidate (**600** shadow distance, **4** cascades, **4096** shadow atlas). Earlier aircraft-shadow loss on ground/screen edges motivated this change. **Do not claim shadow-range visual acceptance without testing**; shadow geometry can still leave camera view. WeaponLab/GroundAttackLab use the unchanged existing rig/profile.
- **World scale:** landforms, not pebble props, make the geography. One low plateau is an accepted module; stacked/higher formations may eventually become flyover hazards or collision walls. No extra plateaus, roads or canyon pass before the first base-style test.
- **B1d Game-view feedback:** three road prefabs and separate sparse wear marks connect the barracks and HQ acceptably for this iteration. The prepared-ground coat had an overly grey, 10–13m feathered edge and looked like a smeared shadow. B1d2 below changes **only the painted ground**, keeping roads/HQ/barracks/plateau at their current authored transforms. After ground approval: a restrained building-variation pass, then next building. No AO or sun bake.

- **Current B1d2 microatom:** revise only the installed `B1d_PreparedGround` TerrainLayer fill. Old grey soil with 10–13 m soft feather looked like a blurred shadow; switch to warm brown `#A38B72` with near-solid coverage and ~2 m irregular edge. Run **Air Combat > Art > B1d2 - Refine Prepared Ground** once; commit updated `E1_Terrain.asset` and `BaseRoadB1d/PreparedGround.png` without changing existing GUIDs. No roads/buildings/plateau/camera changes. [Road design and upgrade](Docs/Production/ROAD_B1D.md).

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
