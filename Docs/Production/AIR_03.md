# AIR-03 — independent Tiger attack passes

Base `e48636f`. FLIGHT-02 accepted in playtest; W/S gives substantially better continuous control. Keep Tiger as an entry-level enemy: 5 HP, existing flight/boost/turn and AC-01 stats unchanged. No missiles or audiovisual polish in this atom.

- Both Tigers still spawn together on the same offscreen ring. At each spawn, clone the authored attack settings per pilot. Randomly assign the earlier/later tempo bands to Alpha/Bravo; no per-frame random steering or shared mutable settings.
- Earlier pilot: attack distance x1.04–1.12, phase entry reaction 0.05–0.20 s, exit min/max time x0.80–0.95. Later pilot: distance x0.88–0.98, reaction 0.35–0.60 s, exit min/max time x1.10–1.25. Both independently vary close approach timeout, exit distance, catch-up distance (±10%) and exit boost duration (±15%). Reaction is continuous line-up time before entering Attack, not delayed steering. Fire cone/range and pass distance remain unchanged.
- Soft wingman separation: within 50 units, add a distance-weighted heading bias up to 55 degrees through the existing steering command, no transform displacement or collision forces. Choose a side once per close encounter, with a deterministic fallback for nearly collinear/overlapping pairs. Boundary return overrides separation. The survivor has no wingman correction. Radius and max bias are editable in the encounter's Attack settings (0 disables separation).
- No changes to scene/prefab serialization, main player controls, radar, weapons or lab behavior. This reduces persistent overlap; brief crossing paths are still possible.

## Play Mode gate

- R several times; watch the two phase labels on H. Their attack/exit timing should diverge rather than produce prolonged parallel passes. Check both straight flight and repeated tight turns, especially after a pass and near the boundary.
- In a close encounter, both should split naturally without jitter, teleporting or endlessly turning away from the player. A temporary crossing is acceptable; persistent merged silhouettes are not.
- Kill one Tiger: the survivor continues normal approach/attack/disengage, with its own timer settings. Check death, radar contact cleanup and R. Both should still pose a threat and be killable at 5 HP.
- Verify W/S fuel/steering, both guns and legacy labs are unchanged. Next: discuss missiles; fuel-use sound and combat juice remain deferred.
