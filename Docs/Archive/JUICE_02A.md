# JUICE-02a — critical aircraft damage trail

Base `58aaf4b` / JUICE-01b accepted. Production-only playtest; no damage, weapon, AI, camera or flight changes. Falling wreck and terrain impact are separate follow-up atoms.

- `DamageSurfaceAnchor` selects a fixed model-local point. Optional authored `DamageSockets` group (direct children are candidate positions) takes precedence. Otherwise select an area-weighted random triangle from active readable rigid hull meshes captured on registration, and sample inside that triangle. The point follows the actual mesh transform and bank. No bounding-box fallback; missing geometry logs once and suppresses the trail. No per-frame mesh sampling, collider changes, or Unity gameplay RNG consumption.
- Kestrel FBX and Tiger fuselage/main wings/engines/tail OBJ imports now have Read/Write enabled. Their GUIDs and all other settings remain untouched. This costs a CPU-side mesh copy; keep it limited to eligible low-poly hull meshes. Future models can supply sockets instead of readable meshes.
- Both player and enemies use percentage HP: at <=40% persistent charcoal smoke, at <=20% denser smoke plus short orange fire. Tiger: 2/1 HP; player (20 HP): 8/4. The point stays fixed until reset/recovery. World-space particles are emitted along the movement segment; no inherited aircraft velocity, no smoke shadows, dynamic lights or physics.
- Two shared bounded particle systems (768 smoke / 256 fire) reuse JUICE-01b's procedural puff mask; impact puffs are unchanged. Parameters live in runtime `CombatHitFeedback` on the encounter, with persistent defaults in its script. R clears particles and anchors; killed/untracked targets stop emission and their existing particles fade. Player defeat also stops the trail. Legacy labs do not bind these effects.

## Play Mode gate

1. Open ArcadeDesertE1, Play. Damage Tiger to 2 HP: a continuous dark trail should start on an actual hull surface; at 1 HP see a denser trail and fire. Kill it: emission stops, existing particles fade. Impact puff is still distinct.
2. Let enemy damage player to 8/4 HP; check the same states through A/D, W, S and sharp bank. Check that source moves with mesh and does not jump between hits; multiple R resets should vary anchors. Check low FPS / boost for gaps and 7/8/9 height changes for long streaks.
3. R during smoke, R after defeat, map reset: no residual smoke or duplicated particle systems. Confirm both labs and all materials, prefab links and Console remain clean after model reimport.
4. Evaluate readability and frame rate before JUICE-02b (burning falling wreck), JUICE-02c (terrain explosion/debris/sound/shake), JUICE-03 (wingtip trails), then missiles.

## JUICE-02a-b — softer persistent trail

Base `983784e`, procedural anchors accepted in Play Mode. Tune presentation only: smoke rate 36 -> 24/s, lifetime 1.2 -> 0.72s, size 3.1 -> 2.6, growth 0.8 -> 1.45 over lifetime. Lighter charcoal, lower alpha (0.38–0.58 critical / 0.46–0.66 severe), modest world-space drift, and a separate softer 128x128 mask for persistent smoke. Severe smoke retains a 1.25 rate and 1.12 size multiplier; fire is smaller, shorter and less opaque. Impact puffs retain their original mask and parameters. No changes to anchor selection, HP thresholds, reset, rendering quality assets or gameplay.

Check both health thresholds at cruise, boost and tight turn; verify the trail remains readable without becoming a black rope or obscuring nearby aircraft. Compare at the same Game View resolution and MSAA settings. Then JUICE-02b (falling wreck).

## JUICE-02a-c — continuous layered smoke

Base `1b5b773`: shorter trail accepted as direction, but individual puffs still read as beads. Core smoke now emits every 0.78 world units and the larger turbulence layer every 1.5 units, both sampled along the previous-to-current anchor segment with fractional distance carried between frames. Severe spacing is 0.88x. Lifetime stays around 0.72s. Overlapping low-alpha core particles form the continuous trail; a second, softer irregular 128x128 mask, drift, noise and slow random rotation give the outer layer texture. Three shared bounded trail systems (768 core / 384 cloud / 256 fire); no new physics, lights, shadows or per-aircraft particle objects. Fire and impact puffs retain their previous look. Existing anchor, HP, teleport guard, reset, and encounter rules are unchanged.

Playtest both health stages, straight cruise, W boost and S tight turns at the same resolution/MSAA. Look for a coherent smoke trail without visible bead gaps or opaque rope; verify R clears both layers and fire. Check low FPS and multiple damaged aircraft; no new Console errors. Next JUICE-02b falling wreck.
