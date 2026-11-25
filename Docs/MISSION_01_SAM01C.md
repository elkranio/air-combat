# SAM-01C — single raised launcher visual trial

Replace Picket's four decorative boxes and tall sensor block with a single raised low-poly launcher, a recessed red mouth and a compact turntable silhouette. Retain the existing truck chassis, supports, palette, scale, eight OBJ group names and Unity asset GUIDs. `Launcher` and `Accent` contain only the moving upper art; the fixed base stays in the existing `Metal` and `Equipment` groups. No prefab re-authoring is required for this visual trial. The generated OBJs are included; the Python generator remains reproducible.

Open Mission01_FirstSortie after Unity imports the meshes. Inspect a placed Picket in Scene view and at gameplay zoom. Check the silhouette, color contrast, shadow and nearby field dressing. Existing SAMs use the same visual prefab. Do not reinstall the tactical map or rerun the field-site authoring menu.

This atom changes art only: the existing temporary vertical launch socket and missile flight remain untouched, so the launch may not visually align with the new tube yet. A later atom will bind a visual pivot and synchronize the fictional launch animation. No changes to detection range, warning, reload, projectile behavior, or mission layout. Unity import and visual acceptance are pending.
