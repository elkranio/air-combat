# Map design — persistent arcade battlefield

**Scene:** `Assets/_Project/Scenes/ArcadeDesertE1.unity`. One editable 2×2 km Unity Terrain; not a one-shot generator and not a photoreal landscape. Current ground is a flat, muted mid-light fill and the world contains one low, wide plateau.

## Scale and navigation

- **Player:** default flight at **Y78** after B1c installation; 7/8/9 remain temporary comparisons Y78/Y110/Y150. At Y78 the dynamic aircraft shadow is more useful while the plateau and barracks retain acceptable proportions. Weapon ballistics remain to be tested in a future integrated mission. Flight controller moves a Transform, not a fully physical aircraft.
- **Geology:** plateau approximately 430×275 m, ~30 m high. Treat it as *landmass*, not a big rock prop. Multiple different elevations can produce flyover danger or canyon walls later; real crashes require an explicit flight-collision system, not merely mesh colliders.
- **Camera:** fixed-world yaw -45°, pitch 50°, orthographic size 80, smoothed forward look-ahead. Environment remains still when jet turns; player readability takes precedence over cinematic rotation.
- **Combat:** terrain should support open passes, clear approach/departure directions, roads and defended bases. Keep substantial negative space. Ground targets can be stylized larger than reality, but their relationship to buildings, aircraft, plateau and HUD must be consistent.
- **Shadows:** E1 map currently uses candidate 600-unit shadow distance with four cascades and 4096 atlas via scene-only profile. Check Y150 plane/plateau and screen edges. Do not turn the current result into a claim of final performance or quality without Play Mode measurement.

## Expansion

First build a compact military-base sector inside the existing terrain ([B1 plan](BASE_DESIGN.md)); then compose connecting road, small airfield, multiple threat sites and progressively larger terrain obstacles. Keep each accepted region, materials and modular structures instead of generating fresh whole-map studies. Broader hills, canyon and crash handling are future deliberate atoms, **not prerequisites** for the first building.
