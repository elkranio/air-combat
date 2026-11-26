# FIX-02 — resolve the active Mission01 runway

Base `e722439ed5f0e40312a511fde68ca72e82464893` (FIX-01 green).

`Airfield_B` retains an inactive `Unified pavement` with `Mission01_AirfieldPavement`. Its active `Unified pavement C6 / Continuous asphalt` uses the main mesh of `Mission01_HangarPass.asset`. The resolver excluded the inactive branch and still searched for the former mesh name, forcing LAND-01A fallback.

The active C6 mesh's decoded vertex positions confirm runway X 113..137 and Z -120..120 (rounded corners), with the apron attached. The existing runway anchor X125 and width24 remain valid. The scene's authored Lynx starts at local X125, Z-92 relative to pavement, with matching 30-degree yaw. The expected inbound threshold offset is approximately 212 world units instead of fallback 150; corridor far offset is approximately 412. The existing approach and rollout formulas are unchanged. No scene, prefab, asset, GUID, inspector or tuning changes. The obsolete inactive pavement can be removed in a later cleanup, not in this atom.

## Local regression gate

- Open Mission01 and enter Play Mode: no `fidA != fidB` and no `LAND-01A: pavement not resolved` warning.
- Enable Debug Landing Near Airfield temporarily. Verify cyan corridor begins at actual inbound runway threshold, is centered and matches the runway width; verify ground marker and capture.
- Land, check touchdown and stop on runway, then R. Restore debug flags. Unity compile and Play Mode remain the acceptance gate.
