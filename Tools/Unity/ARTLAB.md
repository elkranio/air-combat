# Scorched Frontier visual playground

`Sandbox.unity` is the gameplay reference and sole build scene. Current visual baseline: `Assets/_Project/Scenes/FrontierStrata.unity` (V17), with integrated rocky relief, V14A kit, V14B roads and dynamic shadows. `ScorchedFrontier.unity` (V13) remains the accepted fallback. `FrontierBakeTruth.unity` (V18) is the separate working bake diagnostic.

## V18 cleanup (completed; historical one-time procedure)

Stop Play Mode, save scenes, let Unity refresh. Run read-only preview:

```sh
unity command script run --file Tools/Unity/CleanupFrontierStudies.cs --entry CleanupFrontierStudies.Preview
```

Then apply:

```sh
unity command script run --file Tools/Unity/CleanupFrontierStudies.cs --entry CleanupFrontierStudies.Configure
```

Moves shared V14B road assets and V16 ground into `Generated/FrontierStrata/` preserving GUIDs; retires intermediate V14A/B and V16 scenes and obsolete generators, ground texture/mesh/FBXs after dependency preflight. Does not regenerate or edit any retained scene. Review git status; play V17, V18 and Sandbox before committing. If interrupted, rerun only after checking the worktree (moves accept source or destination). Never force deletion on a dependency conflict.

## V17 — active map

Open `FrontierStrata.unity` and play. WASD, Space boost, 1/2/3 camera pitch (90/75/65), R recenter, H legend. B cycles only the V14A hero kit material modes, not the terrain. Use this scene as the starting point for V19, not the retired scene-generator chain. Keep its existing terrain and roads intact. V17 Blender source and height manifest remain available under `Resources/Blender/FrontierStrata.*`. To recreate its original scene generation, use the pre-cleanup commit in a separate worktree.

## V18 — Bake Truth Test

To rebuild the isolated diagnostic only:

```sh
blender -b --python-exit-code 1 --python Tools/Blender/generate_bake_truth_test.py -- --device OPTIX
unity command script run --file Tools/Unity/ConfigureFrontierBakeTruth.cs --entry ConfigureFrontierBakeTruth.Configure
```

Stop Play Mode and save scenes before the Unity command. Left is base-only; right changes with 1 base, 2 AO, 3 normal, 4 full, 5 stronger AO, 6 normal x4, 7 both stronger, 8 raw AO, 9 raw normal, 0 control, T cycle controls. C toggles close/gameplay size 80; L cycles lighting; H hides labels. Tested: ordinary normal strength 1 produces clearly visible relief even at gameplay distance; AO is subtle. Keep separate maps, dynamic shadows, author large high-only surface details. No need to rerun the diagnostic or rebake the older hero kit just to begin V19.

## V19 — Practical Bake Test

From cleanup green `b21a5666`, generate the new roof and BrokenMesa detail maps (existing V17 height manifest and V18 bake helper required):

```sh
blender -b --python-exit-code 1 --python Tools/Blender/generate_frontier_practical_bake.py -- --device OPTIX
```

Optional `--device CUDA` / `--device CPU`, `--size 1024`, `--samples 48`. Wait for Unity refresh/compile, stop Play Mode and save open scenes:

```sh
unity command script run --file Tools/Unity/ConfigureFrontierPracticalBake.cs --entry ConfigureFrontierPracticalBake.Configure
```

Opens `FrontierPracticalBake.unity`, a copy of V17. One new process hall on the refinery main block footprint; BrokenMesa north of the refinery keeps its V17 geometry and albedo with a local baked detail normal/AO layer. B toggles base / AO / normal / both on exactly those two surfaces; H hides labels. Existing flight and dynamic lighting unchanged. Compare both at gameplay size 80; no changes to retained scenes or old kit. V19 generated and tested green `a463dc8d`; the normal effect is obvious, but the vein-like rock fissures were artistically disputed.

## V20 — Mesa Polish

```sh
blender -b --python-exit-code 1 --python Tools/Blender/generate_frontier_mesa_polish.py -- --device OPTIX
unity command script run --file Tools/Unity/ConfigureFrontierMesaPolish.cs --entry ConfigureFrontierMesaPolish.Configure
```

After Blender finishes, stop Play Mode, save scenes, allow refresh/compilation before running Unity. Opens `FrontierMesaPolish.unity`, a copy of V19. B: base / V19 1.0 / V19 .4 / V20 .45 (default) / V20 .8. Only BrokenMesa normals change; AO is disabled in all five comparison variants; roof stays in V19 full mode. R returns to the nearby start; H hides legends. New 1024 normal map (optional `--size 2048`), no terrain geometry or albedo changes. V20 pending local test.

## V13 — fallback generator

```sh
unity command script run --file Tools/Unity/ConfigureArtLabScorchedFrontier.cs --entry ConfigureArtLabScorchedFrontier.Configure
```

Rebuilds V13 from Sandbox's flight setup plus the existing Frontier materials and lighting pipeline. Do not run casually: V17 shares some assets with V13. Sandbox itself is not modified.

## Archive

All V14–V18 pre-cleanup scenes, scripts and generated output are preserved in Git at `a4ad17979ed9ebc222600625b2517919c10a3bbd` (use a separate worktree). V1–V13 before earlier cleanup: `3aae3d12b84e5276a333060fdd1480828aa17f2c`; heavy V10: `3d8ddf6`. Do not restore obsolete outputs into the active project or rewrite history. Existing authored hero kit, tree assets and source generators remain for reuse, not for in-place regeneration of V17.
