# Mission 01 — rock layout v3 (draft)

Source: [technical X/Z scheme](MISSION_01_ROCK_LAYOUT_V3.svg). World bounds X/Z -1000..2200, cruise Y78. The central triangular massif extends past the south edge. R1–R6 are small independent formations. No new models; existing CL06G prefabs and compact debris style.

In Mission01_FirstSortie, run **Air Combat > Missions > Apply Mission 01 Rock Layout v3**, review and save the scene. Undo supported; re-running replaces the generated v3 layout. The previous CL06 main wall, orientation trials, and their attached debris are removed. Do not re-run legacy v2/CL06 placement menus after this pass.

- 263 radar/collision solids, 66 visual-only debris meshes.
- Six visual SAM placeholders, same accepted prefab. Planned radii A–F: 430, 470, 430, 440, 460, 430; names are planning metadata, not live weapon range.
- Three recon markers at Y78; optional ground target marker; route guide markers, not enforced rails. Airfield and player unchanged.
- No runtime changes. Map boundary enforcement is still required to prevent flying outside the southern edge and around the massif. SAM combat, objectives and landmarks beyond rock formations are subsequent passes.
- Offline section checks on the actual current model meshes, both X handedness conventions: main section one polygon, no holes at Y78; main/optional guide clearance from rocks >=190/144 units respectively. These are geometric calculations, not Unity flight or performance tests.
