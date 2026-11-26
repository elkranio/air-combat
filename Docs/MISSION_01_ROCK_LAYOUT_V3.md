# Mission 01 — rock layout v3 (draft)

> Historical planning reference. Bounds, SAMs and objective placeholders below predate the current playable mission. Use [Mission01](MISSION_01.md) and the saved Unity scene for current gameplay.

Source: [technical X/Z scheme](MISSION_01_ROCK_LAYOUT_V3.svg). World bounds X/Z -1000..2200, cruise Y78. The central triangular massif extends past the south edge. R1–R6 are small independent formations. No new models; existing CL06G prefabs and compact debris style.

The saved scene is the source of truth. The one-time v3 installer was retired by CLEANUP-03. Keep the JSON recipe and SVG for planning/reference; edit the saved scene directly.

- 263 radar/collision solids, 66 visual-only debris meshes.
- Six visual SAM placeholders, same accepted prefab. Planned radii A–F: 430, 470, 430, 440, 460, 430; names are planning metadata, not live weapon range.
- Three recon markers at Y78; optional ground target marker; route guide markers, not enforced rails. Airfield and player unchanged.
- No runtime changes. Map boundary enforcement is still required to prevent flying outside the southern edge and around the massif. SAM combat, objectives and landmarks beyond rock formations are subsequent passes.
- Offline section checks on the actual current model meshes, both X handedness conventions: main section one polygon, no holes at Y78; main/optional guide clearance from rocks >=190/144 units respectively. These are geometric calculations, not Unity flight or performance tests.
