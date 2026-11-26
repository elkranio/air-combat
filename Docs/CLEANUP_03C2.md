# CLEAN-03C2 - Retire obsolete art and lab assets

The local Unity reachability audit reported 15 unreachable prefabs, 47 prefab-only assets and 17 orphan candidates. This pass removes 37 of those 79 assets (and their metas), three obsolete Hangar-04 / MT-01 authoring tools, and empty folder metas where applicable.

Removed: old AA1/SAM1 prefabs and unused materials, Hangar-04 and MT-01-Sentinel imported art, old enemy Kestrel materials, B1d3 prepared lot, Terrain01 and GroundAttackLab assets, and WeaponLab training assets. The currently used AutocannonTracer material remains. No changes to live scenes or combat scripts.

Preserved intentionally: CL06 rock variants and layout JSON, ENV04 authoring family, MilitaryDepot ground target and authoring outputs, old Mantis authoring family, PolyPizza model sidecar, raw Resources and shared assets. Unreachable from today's scenes does not necessarily mean obsolete.

The committed C1 auditor must be the only change from the audited base. Preflight checks exact asset blobs against that base, audit GUIDs, clean worktree, and surviving tracked text references. This is not proof against dynamic loads or binary references beyond the Unity audit. Reopen Unity and rerun the audit, test Mission01 and all CombatSandbox modes, then commit with git add -A.
