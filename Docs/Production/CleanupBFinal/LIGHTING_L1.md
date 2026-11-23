# Lighting — shared URP sun and shadow profiles

The authoritative sunlight prefab is `Assets/_Project/Prefabs/Environment/CombatLightingRig.prefab`. Instantiate **one** per gameplay scene. `CombatLightingRig` chooses day/sunset/near-night, sun azimuth/elevation, intensity and ambient; it restores global settings on disable. No second directional sun or painted shadows.

## Profiles

- **Main authored map** (`ArcadeDesertE1`): isolated candidate `Assets/_Project/Settings/ArcadeCombat_URP.asset`, **shadow distance 600**, **4 cascades**, main-light shadow atlas **4096**. It is a scene-instance override, not the global rig prefab. Intended to address aircraft shadow loss over lower ground and shadows disappearing near viewport edges. **Final in-game quality and cost have not been conclusively verified**.
- **WeaponLab / GroundAttackLab**: established `Assets/Settings/ScorchedFrontier_URP.asset` with **shadow distance 230**, **2 cascades**, atlas **4096**. Keep unchanged until a deliberate cross-scene migration.
- The actual sun casts directional soft shadows on aircraft, plateau, structures, terrain, projectiles. The old projected ground-shadow component and its Sandbox scene are retired. Do not rebake the sun into mesh albedo or replace real shadows with cartoon blobs.

## Checks

Fly at Y78/110/150 over bare ground and the ~30m plateau. Ensure the shadow remains visible *if its location is in the camera viewport*, including near screen edges; absence due to view geometry is not fixed by render distance. Compare quality near cascade transitions and from a moving jet at ortho 80, yaw -45°, pitch 50°. Larger shadow range/cascade count can increase shadow rendering time even with simple geometry; profile before deployment. Also regression-test the two combat labs with their original lighting.
