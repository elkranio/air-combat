# Scorched Frontier visual playground

V13 was accepted after flight testing. The stable scene is `Assets/_Project/Scenes/ScorchedFrontier.unity`. It is independent of the retired forest scenes. The current gameplay reference remains Sandbox.

## One-time migration from V13

Stop Play Mode and save all open scenes, refresh scripts and wait for compilation. Run a read-only preview first:

```sh
unity command script run --file Tools/Unity/CleanupArtLabs.cs --entry CleanupArtLabs.Preview
```

If the dependency check succeeds, apply the migration:

```sh
unity command script run --file Tools/Unity/CleanupArtLabs.cs --entry CleanupArtLabs.Configure
```

It moves the accepted scene, generated folder, lighting asset and three aircraft materials preserving GUIDs. It deletes only explicitly listed obsolete ArtLab scenes, outputs, scripts and editor generators, not Sandbox or authored models. A dependency conflict stops deletion. If interrupted, rerunning accepts already-completed moves. Do not regenerate Frontier merely to migrate it: this keeps the tested scene intact. Inspect git status, play both scenes and commit changes.

## Rebuild Frontier when changing the generator

```sh
unity command script run --file Tools/Unity/ConfigureArtLabScorchedFrontier.cs --entry ConfigureArtLabScorchedFrontier.Configure
```

Run from root with Play Mode stopped and saved scenes. This rebuilds the stable scene from Sandbox's flight setup plus the existing Frontier materials and lighting pipeline, producing the same deterministic landscape and reusable V0 modules. Sandbox is not changed. The existing Frontier scene is a generated destination, so preserve new hand-authored content separately.

Play: WASD, Space boost, 1/2/3 camera pitch, R recenter, H legend. No combat, target objects or destruction mechanics yet.

## V14A — Hero refinery and rock kit (pending visual acceptance)

From the repository root, generate the full kit with Blender (Cycles OptiX GPU):

```sh
blender -b --python-exit-code 1 --python Tools/Blender/generate_frontier_hero_kit.py -- --device OPTIX
```

The script prints detected GPU names, generates 20 modules with base color, local AO and high-to-low normal maps, and saves an editable Blender file. Defaults: 1024 textures, four hero units at 2048, 48 samples. For a fast geometry check append `--quick` (512 base only); use `--device CUDA` or `--device CPU` if OptiX is unavailable. A successful run writes `HeroKitManifest.json`. No lights or directional shadows are baked. The bake process has not yet been tested on the local Blender/GPU installation.

Refresh Unity and let runtime scripts compile. Stop Play Mode and save scenes before generating:

```sh
unity command script run --file Tools/Unity/ConfigureFrontierHeroKit.cs --entry ConfigureFrontierHeroKit.Configure
```

Open `FrontierHeroShowcase.unity` (the generator opens it). Flight controls as above; B cycles base color only, local AO only, high-poly normal only, and both. H hides both legends. Start near the new refinery; rock formations are immediately north. Real dynamic shadows in every mode. The original Scorched Frontier and Sandbox are unchanged. Compare screenshot/flight results before V14B integration. The Blender FBX/PNG assets and `.blend` source, material/prefab imports and scene are generated locally; commit only complete successful output. Keep the kit modest; inspect file sizes.

## Archive

All V1–V13 experiments before cleanup are available in Git at `3aae3d12b84e5276a333060fdd1480828aa17f2c`; the heavy V10 is at `3d8ddf6`. Use a separate worktree or checkout to inspect them. Do not reintroduce their outputs into the active project. Authored tree assets and Blender sources are retained for potential reuse.

## V14B — Surface & Composition

From the tested V14A showcase, after saving scenes and stopping Play Mode:

```
unity command script run --file Tools/Unity/ConfigureFrontierSurfacePass.cs --entry ConfigureFrontierSurfacePass.Configure
```

Opens `FrontierSurfacePass.unity`, a separate scene with new terrain/yard and road color PNGs, preserved V14A hero models and existing B material comparison. No Blender run and no changes to stable Frontier or Sandbox. Dynamic shadows remain on; the generated color textures contain material wear, not lighting.
