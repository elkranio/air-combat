# Lighting — shared URP rig

The authored sunlight prefab is `Assets/_Project/Prefabs/Environment/CombatLightingRig.prefab`. One scene sun; `CombatLightingRig` manages light/ambient settings and restores global settings on disable. Do not add another directional sun or bake its shadows into albedo.

`Assets/_Project/Settings/ArcadeCombat_URP.asset` was introduced for the larger shadow range test (600 units, four cascades, 4096 atlas). Treat the current assigned render pipeline assets and scene overrides as authoritative; the historical lab profiles are not active scene instructions. Quality/cost is not a concluded performance benchmark.

Fly at the actual camera settings over ground, plateau, rocks and airfield; check shadow clipping in the viewport, cascade transitions, terrain self-shadowing and frame time. Sandbox altitude comparison 7/8/9 is useful for diagnosis. Mission01 flat faceted ground was configured to receive rather than cast shadows to avoid striping. Verify behavior in both working scenes before changing global rendering settings.
