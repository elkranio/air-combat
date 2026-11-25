# MISSION-01H1e — Authored cliff mesh comparison (WIP)

Base `a395efc`. Local density H1c did not improve the silhouette at game zoom. Keep the current central Terrain ridge unchanged as comparison. Test one separate, approximately 330 x 186 x 154 unit rock segment, center X=-180 Z=-70, on flat ground along the departure direction. It has a short talus foot, two steep wall tiers separated by a ledge, and a distinct lighter irregular crown. Three matte material groups, no noise or baked shadows.

- Generate OBJ files using `python Tools/Blender/build_mission01_cliff_trial.py`. The same script under Blender background mode creates an editable `Resources/Blender/Mission01_CliffTrial.blend`; the blend file is not supplied by this patch. Combined OBJ geometry is closed; separate material group meshes share coincident boundary positions.
- Open the saved mission scene and choose **Air Combat > Missions > Place Mission 01 Cliff Mesh Trial**. It checks the flat footprint, creates three materials if missing and installs a single scene root with model instances. Repeat selects the existing root. Save scene and generated materials/metas only if retaining the experiment. Disable/delete that root to compare or remove. No changes to terrain data, baked terrain, existing ridge, camera, flight or HUD. No collider or crash logic yet.
- Test from both sides at flight height, using the editor debug air start/Space fast travel. Look for the distinction between foot, cliff wall and crown, not just triangle density. Unity/Blender execution must be checked locally. Terrain radar and collision remain separate atoms.

## MISSION-01H1f — Rock geometry polish (WIP)

Base `b16a441`. Replace only the three existing OBJ geometries and their deterministic Blender source. Twenty-one authored outline corners instead of thirteen, two additional irregular wall rings, variable-width intermediate shelf and a broken crown; 336 triangles instead of 156. Keep the original outer envelope, maximum height 154, material groups, names, GUIDs and scene placement. No extra material bands or random triangulation. Existing scene instances reimport automatically; do not place another trial. Review at the same flight height from both sides, checking that the foot, wall, shelf and crown still read clearly. No camera, palette, collision, radar or Terrain changes. Unity/Blender execution is local; the combined topology is validated by the generator.

## MISSION-01H1g — Flat plateau (WIP)

Base `2e0ca64`. Level the uppermost crown ring and its center at Y154; all cap triangles now share an upward normal. Preserve the irregular XZ outline, lower rings, wall and foot meshes, materials, placement and triangle count. Only the crown bevel and cap geometry change. Existing scene instances reimport automatically. No camera, terrain, collision or radar changes. Check in Unity from above and beside the cliff.
