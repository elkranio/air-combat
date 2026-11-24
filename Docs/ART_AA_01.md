# ART-AA-01 — Mantis rocket carrier

Base: accepted green `9060cbb`. This is a first-pass original stylized game asset, not a real weapon replica. Review at the current gameplay camera before polishing. The compact tracked carrier has a faceted front cab, dark glass, small sensor, six open vertical cells and restrained olive/steel materials. A future guided AA should use a distinctly larger, more sophisticated silhouette; its design is deferred.

## Assets and authoring

- `Assets/_Project/Art/Ground/AA02_Mantis_Rocket/`: 11 single-object flat-shaded OBJ meshes (body and launcher by material), with stable importer metas. Reuses the six existing matte Mantis materials. No textures, third-party models or new collision gameplay.
- `Assets/_Project/Prefabs/Ground/AA02_Mantis_Rocket.prefab`: visual hierarchy with a fixed `Launcher/LaunchSocket` at the center opening of the front row. Six cells are visual only; one rocket per existing cycle. No ammunition simulation or launch sequencing is added.
- `AA02_Mantis_Combat.prefab`: switches the nested visual and references its socket. The combat prefab GUID and its root/motor/launcher component IDs remain unchanged. Existing scene instances and route overrides need no migration. The old twin-gun visual assets remain available until art approval.
- `Tools/Blender/build_aa02_mantis_rocket.py`: deterministic source; run `python Tools/Blender/build_aa02_mantis_rocket.py` from repo root to rebuild OBJs, or `blender -b --python Tools/Blender/build_aa02_mantis_rocket.py` to additionally save the editable scene under `Resources/Blender`. Blender was unavailable in the patch environment, so the native scene was not generated or tested.

`MantisGun` now reads the authored socket rather than generating a cylinder at runtime. Rocket phases, material appearance, rate, range, damage, correction and patrol logic are unchanged. Only the launch origin moves to the new visual opening. No additional effects or sounds in this atom.

## Regression gate

Open `ArcadeDesertE1`; the new visual should be present even outside Play Mode. Inspect prefab for missing meshes, materials, and `LaunchSocket`. Verify one rocket exits the front-row center cell vertically; existing limited correction, radar, patrol, multiple instances and R reset still work. Compare silhouette from several headings and at normal camera zoom. Do not delete old art until acceptance. Next: guided visual if desired, or JUICE-AA-01, then ground damage.
