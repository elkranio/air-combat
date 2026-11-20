# Art pipeline

The checked-in Sandbox is the playable reference scene. Keep art experiments separate from the flight and combat code.

- `Resources/Blender/`: editable Blender sources (not imported by Unity).
- `Resources/References/`: optional visual references, not shipped with the game.
- `Tools/Blender/`: reproducible asset generators; run from the repo root with `blender -b --python-exit-code 1 --python ...`.
- `Tools/Unity/`: editor-only import, prefab and scene assembly tools.
- `Assets/_Project/Art/`: authored FBX meshes, textures, materials, prefabs, grouped by Aircraft, Buildings, Nature, Terrain, Weapons and VFX. Keep related assets together.
- `Assets/_Project/Generated/`: map-build outputs, not source models. Keep the existing Battlefield until its replacement is ready.
- `Assets/_Project/Scenes/`, `Scripts/`, `Shaders/`: scene and gameplay resources.
- `Assets/ThirdParty/`: temporary dependencies only; see `THIRD_PARTY_ASSETS.md` for attribution.

## 09a.1 — migration

Run `unity command script run --file Tools/Unity/MigrateArtLayout.cs --entry MigrateArtLayout.Configure` once after applying the patch, with Play Mode stopped and all scenes saved. Unity moves assets together with their `.meta` files and checks dependencies before deleting retired assets. Commit generated scene and asset changes after a successful playtest. Do not regenerate Blender files until migration finishes, to avoid duplicating paths.

The old airplane model setup and template-scene builder are retired. `ConfigureSandboxKestrel` can reinstall a model using its installed predecessor as the size reference. The current Kenney battlefield, skybox, inactive procedural landscape and third-party missile remain until their own replacements are installed. The next art atoms will eliminate these remaining dependencies and introduce a separate ArtLab scene at the same camera settings as Sandbox.
