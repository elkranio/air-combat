# B1b — stylized barracks + side-shadow study

Status: **assets authored; Unity Game-view visual verdict pending**. This is an in-place
style pass on B1a, **not** a new barracks prefab, map or reinstallation command.

## What changed

- Keep the existing `ArcadeDesertE1` Terrain, plateau and Y150 approach unchanged.
  The three `Barracks_B1a` prefab instances remain in the base sector.
- Rework **same four source OBJ meshes** via `Tools/Blender/generate_barracks_b1a.py`.
  Preserve object names, four import `.meta` GUIDs, prefab links and Unity scene transforms.
  Main walls stay ~10.4×30.5m; the roof eaves project to ±6.4m, with a 6.62m ridge,
  visually thick fascia and an entry canopy on the camera-facing +X wall. Two
  simple ~2.4×3.4m roof vents break the roof silhouette. No tiny details.
- Scene-neutral prefab `Assets/_Project/Prefabs/Environment/Base/Barracks_B1a.prefab`
  keeps its materials and collider component; its box bounds are widened to cover
  the canopy (15.6×7.1×33m, center X1.35/Y3.38). Barracks have real URP shadows.
- Four flat URP materials, recolored **in place**: walls `#91A9A2`, roof `#385568`,
  openings `#253945`, structural trim `#A78C68`. No additional textures / bakes.
- **Main map only:** `CombatLightingRig` prefab instance overrides
  `azimuth = +52°`, `elevationOffset = −15°` (Day preset sunlight is now 45° elevation),
  exposing longer diagonal shadows from the unchanged camera yaw −45°.
  `CombatLightingRig.prefab`, `WeaponLab`, `GroundAttackLab` and URP profile do not change.

## Check in Unity

Apply the patch on the B1a green commit. Open `ArcadeDesertE1.unity`, Play at Y150.
After import, run **Air Combat > Art > B1b - Apply Barracks Palette** (once).
This updates the existing four materials in Unity and preserves their GUIDs.
Then Play; commit the updated material files, model sources, scene and prefab.

Fly toward the row and pass alongside it. Judge the **roof silhouette**, entrance,
vents, wall-vs-sand contrast, shadow length/angle, and whether the trio reads
as a military camp instead of toy cottages. Also fly to the plateau to check
that the new scene-only sun angle has not damaged its readability.

Do not assert the shadow issue is permanently fixed without seeing the result:
current URP shadow distance, camera culling and cascade behavior still apply.
If shadows remain invisible, inspect receiver/caster URP flags and frame position
before raising shadow distance again. B1a can be compared with Git history.

Next after GREEN: warehouse/maintenance building as the second authored module.
Texture-space AO is optional later only if a real recess cannot be read from
geometry plus direct URP lighting; **never bake the directional sun** into albedo.
