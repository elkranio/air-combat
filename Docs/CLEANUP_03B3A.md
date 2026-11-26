# CLEAN-03B3A - Retire legacy camera and cabrage

CombatCameraRig now requires the existing enabled ArcadeCameraFollow at bind time. Its public signature, TargetLock binding and camera timing are retained. The old fallback cameras, NURS camera cue and cabrage maneuver (with metas) are removed. The variable flight path is untouched. The fallback flight path still supports the old BreakTurn but no longer has cabrage/abort integration. WeaponController changes only its execution-order comment. No scene, prefab, serialized field or weapon tuning changes.

Preflight checks the exact accepted HEAD and blobs, a clean worktree, unique replacements and remaining tracked text GUID/type references after proposed edits. Text scans cannot prove dynamic/binary dependencies. Close Unity before applying; reopen and verify both scenes, all sandbox modes, W/S/A/D, camera and R. No commit is performed by the script.
