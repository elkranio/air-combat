# CLEAN-03B4A - Player flight cleanup

Retire the unreachable legacy player throttle and break-turn path. FlightController always executes the existing variable flight tick; keep its exact formula, authored tuning, debug fast travel and handover. Defaults match FlightSession's defaults for standalone authoring. FlightSession and CombatSandboxEncounter no longer look for a player BreakTurn. Both player combat prefabs drop only the three obsolete serialized fields. No changes to scenes or their file IDs. Tiger's CombatAircraftFlight and BreakTurn are deliberately retained, including their initialization contract. TargetLock removal is separate because camera prefab overrides exist in both live scenes.

Preflight requires accepted HEAD, exact blobs, clean worktree and no surviving text references to removed player fields/methods. Test both scenes, W/S/A/D, boost, retry and Tiger dogfight in Unity. No commit performed.
