# B1e — first airfield slice (runway, hangar, tower, static aircraft)

**Status:** source meshes and one-time installation prepared, **Unity visual verdict pending**. Extend the *existing* `Assets/_Project/Scenes/ArcadeDesertE1.unity`. Keep the approved B1d3 prepared lot, the B1b barracks, B1c HQ, plateau, Y78 flight spawn, lighting and camera unchanged. Do not rebuild the map or manually overwrite old prefabs.

## Install once

1. Apply patch, let Unity import seven `*.obj` meshes and compile the editor script. Exit Play Mode, save/discard any dirty scenes.
2. Run **Air Combat > Art > B1e - Install Airfield: Runway + Hangar + Tower**. Once only, it refuses existing outputs. It does not generate a new scene or terrain.
3. Play the **existing main map**, cruise Y78. Fly from barracks/HQ toward the new airfield to judge its scale and shadows. For a direct visual edit, select **`B1 / Airfield - runway hangar tower`** in Hierarchy; every asset underneath is independently movable.
4. On green commit the modified `.unity` scene, generated prefabs (`B1e_Hangar`, `B1e_ControlTower`, `B1e_RunwayAndApron`), new 7 URP materials, unit-surface mesh and their Unity `.meta` files. The source `Tools/Blender/generate_airfield_b1e.py` reproduces all seven OBJ sections from Python 3 (`python3 Tools/Blender/generate_airfield_b1e.py` from project root). Their `.meta` GUIDs are stable; the installer itself should eventually be retired.

## Composition

- **Do not extend the B1d3 prepared-lot surface to the runway.** The lot remains a hard-edged ground clearing under HQ/barracks. The airfield is a separate cleared aviation zone to the west; the final base-to-airfield service road comes later, after the two spatial zones have been tested in actual flight.
- **Runway:** 460 × 36 world-metre dark matte asphalt around **X−770/Z−290**, north–south. 48m×474m flat grade/shoulder, very sparse pale center dashes, end thresholds and thin edge guides; not a full photoreal commercial runway. There is no takeoff/landing mechanic. At ortho size 80, seeing the **entire** 460m runway in one Game frame is neither expected nor necessary.
- **Apron:** 120×102m light grey concrete, approximately **X−678/Z−280**; one short taxi connector to the runway, marked for two aircraft. A few separately toggleable, low-contrast surface scuffs only. No repeating road rectangles masquerading as complete road network.
- **Hangar:** separate `B1e_Hangar.prefab`, around **36m deep × 48m wide × 14m high** near **X−619/Z−280**. Faceted pitched roof with a broad portal facing the apron/runway, an actual hollow entrance (side piers + beam), two deliberately large rooftop vents. Muted wall/roof colors reused from the approved B1b barracks; portal reads as its main distinctive functional detail. Dark internal floor is a real floor, not painted fake shadow. No bake.
- **Tower:** separate `B1e_ControlTower.prefab`, around **22m high**, near **X−603/Z−200**. Open four-leg base, elevated control cabin, dark wide window band. No tiny repeating metal lattice clutter.
- **Two parked aircraft:** **visual-only Kestrel chassis prefab instances** on two marked stands on the apron. No FlightController/weapons/enemy AI, no new plane meshes or shared aircraft prefab changes. They may be removed or recolored separately. Runtime flight remains with the *actual* player jet only.
- **Terrain, camera, sun, weapons:** unchanged. New flat surfaces are visual and receive real URP shadows; TerrainCollider remains authoritative. Hangar/tower have simple box colliders, but gameplay aircraft crash handling is still separate future work.

## Review gate

At **Y78** judge (1) hangar silhouette compared with existing barracks/HQ, (2) tower readability versus aircraft and terrain, (3) plausible runway/apron scale without competing with plateau, (4) real shadows on roof/concrete, (5) clear aviation-zone footprint beside the prepared ground, and (6) whether two parked jets feel too large/small. If the hangar reads generically, change **that model**, not the entire ground/camera. No final access roads from the old base in this atom. After this green, design **custom curved service-road geometry** and intentional intersections, not perpendicular tile snapping.
