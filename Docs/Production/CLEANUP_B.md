# Cleanup B — retire experiments, preserve live dependencies

**Base SHA:** use on `master` after green Cleanup A (`8a5d1ce7...` at authoring time). **Before Apply:** commit current work, close Play Mode and save/discard dirty scenes. This is a destructive Git-working-tree cleanup with an explicit dependency check; Git history is the rollback.

## Steps

1. Apply this patch; let Unity compile.
2. Run **Air Combat > Maintenance > Cleanup B - Preview**. Read Unity Console for `DELETE` and `protected` paths. Files required by the main map, both combat labs and every `Assets/_Project/Prefabs/*.prefab` remain protected.
3. If expected, run **Air Combat > Maintenance > Cleanup B - Apply** and confirm the dialog. The command saves the main scene after removing the **inactive** E1/E1b pebble/rock roots; deletes unused old art scenes, generated art folders, one-time builders, C1/T1/Sandbox-specific scripts when unreferenced; prunes empty folders; makes `ArcadeDesertE1` first Build Settings scene; updates root docs to the new live summaries and removes superseded art/cleanup/history docs and known obsolete Blender sources. The Cleanup B menu deletes itself at completion.
4. Inspect `git status --short` — this should show many **intentional deletions** as well as README/HANDOFF and Build Settings changes. Commit deleted `.meta` files too. Never rerun old E1 installers. The one-time helper disappears if successfully applied.
5. **Play Mode regression:** (a) main map loads, no missing scripts/materials, retained plateau and dark-contrast Kestrel, Y150 and 7/8/9, camera A/D/W/Q/E/R, light and shadow edge; (b) WeaponLab air weapons; (c) GroundAttackLab slots 1–6, AA/SAM, radar/HUD, R and light. Build Settings starts on the map. Check Unity Console before calling green.

## Intentional holdbacks / known limitations

- **WeaponLab and GroundAttackLab are kept**: main map has no combined enemy/weapon session yet. Do not claim a single-scene combat game exists. The legacy `CombatCameraRig`, `TopDownCameraFollow` and `CameraLookAhead` remain because the labs use them.
- Some old generated materials/third-party files may survive because active scenes/prefabs still reference them. **This is correct**, not incomplete cleanup: inspect and migrate those dependencies as separate atoms before deletion. Likewise `Assets/_Project/Art/Buildings/{Hangar-04,MT-01-Sentinel}` remains as optional usable assets, pending future art verdict rather than blind erasure.
- Current main URP shadow profile (**600 / 4 / 4096**) remains a candidate; do not claim final performance or shadow issue fixed without checking actual Play Mode.
- Build Settings main entry starts an **art/flight** scene; full combat stays in the two labs. The consolidated gameplay mission is separate future work after visual/base production.
- `Resources/Audio` and third-party license documentation are deliberately not raw-deleted by filename. Preserve attribution for surviving dependencies.
- Files outside `Assets/` are removed by the one-shot script using explicit allowlists (old docs and known rejected Blender sources). The rest of the project is untouched.

## Next

Start [B1 Base Design](BASE_DESIGN.md): small site blockout → one reference-driven warehouse → review in Game view → build additional structures/vehicles one at a time. Don't resurrect the old scene-builder menu. Only then add gameplay sites and mission wiring.

**Documentation result:** `README`, `HANDOFF`, architecture, art guides and third-party provenance are replaced by concise current versions. The old `Docs/Archive` and `Docs/Art` histories are removed from the working project; previous decisions remain recoverable in Git. Tools/Unity's obsolete standalone setup scripts are removed; the current Blender tools for aircraft/weapons stay.
