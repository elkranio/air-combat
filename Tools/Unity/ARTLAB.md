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

## Archive

All V1–V13 experiments before cleanup are available in Git at `3aae3d12b84e5276a333060fdd1480828aa17f2c`; the heavy V10 is at `3d8ddf6`. Use a separate worktree or checkout to inspect them. Do not reintroduce their outputs into the active project. Authored tree assets and Blender sources are retained for potential reuse.
