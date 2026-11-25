# CLEANUP-02 — Mission 01 authoring consolidation

Base `fbe980fcbefb2d4df9291131c93173e90517d551`. Retire the twelve completed Mission 01 editor authoring scripts and their .meta files to `Tools/Archive/CLEANUP-02/Editor/`. The saved mission, prefabs, all runtime components and all imported art remain untouched. Historical installers are recovery sources only; do not copy them back into Assets without checking compatibility.

## Active editor tools

- AssetDependencyAudit — read-only candidate report.
- GroundPatrolRouteEditor — reusable patrol authoring.
- Terrain01Playground — terrain experiments and rebake.
- SAM01PicketPreview — reusable art palette trial; choose Imperial Olive (A) for the current preferred Dominion look. The base commit still contains palette C materials; this cleanup does not recolor them. Apply A using the existing menu and save the material assets.

Open `Assets/_Project/Scenes/Mission01_FirstSortie.unity` directly for campaign work. Do not regenerate it.

## Resource inventory and follow-up

The raw top-level `Resources/Audio/military_sfx_pack_v0.2` contains approximately 44.8 MB including source attribution. Five imported autocannon WAV assets in Assets are separate. Retain the raw pack pending a sound-source decision; no audio deletion is part of this patch. Retain Blender sources, generators, archived trial scripts and attribution. The top-level Resources directory is outside Unity's imported Assets tree. Existing generated/disabled scene assets and lab dependencies are not pruned without a local audit.

Run **Air Combat > Tools > Audit unreferenced art assets** for `Temp/AirCombat-asset-audit.txt`. This is a candidate list, not deletion proof. Check all scenes, prefab and dynamic references before removing any candidate.

## Verification

Rename-only editor changes preserve original contents and metadata. Confirm the old Missions authoring commands are gone after import, existing mission opens directly, takeoff/WASD/R work, and terrain, patrol and palette tools remain available. Unity compilation and Play Mode must be verified locally. No gameplay or scene changes in this atom.
