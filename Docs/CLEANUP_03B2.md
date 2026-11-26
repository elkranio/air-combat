# CLEAN-03B2 - Retire obsolete prefabs

Remove the historical PlayerJet, EnemyJet and CombatCameraRig prefabs and their metas. The two remaining scenes and their current player, Tiger and arcade camera prefabs are untouched. The shared CombatCameraRig C# class remains in use; do not delete it.

Preflight checks exact green HEAD and blobs, clean worktree, and surviving tracked text references to these GUIDs and prefab filenames. Binary and dynamically constructed references are not proven by text search. No gameplay code is changed. Legacy code that was held by these prefabs is reviewed separately.

Close Unity before applying. Reopen and test Mission01 and all CombatSandbox modes, including retry. Commit with git add -A after verification.
