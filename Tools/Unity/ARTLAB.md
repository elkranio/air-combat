# ArtLab palette study (09b)

Generate after runtime scripts compile:

```
unity command script run --file Tools/Unity/ConfigureArtLab.cs --entry ConfigureArtLab.Configure
```

Stop Play Mode and save all scene changes before generating. The tool creates/rebuilds
`Assets/_Project/Scenes/ArtLab.unity` and `Assets/_Project/Generated/ArtLab/`. It opens
ArtLab but does not change Sandbox, its lighting, build settings, source FBXs or
shared model materials. Re-running replaces the generated ArtLab scene, so do not
put hand-authored content there without first updating the generator.

In Play Mode: `1` Emerald, `2` Desert, `3` Northern; `L` toggles palette-specific
versus common neutral lighting; `H` toggles the legend. The camera is static,
60 degrees / orthographic size 80; there is no combat HUD or flight simulation.
The environment geometry is identical in each study, including temporary tree and
rock shapes. The two aircraft, two hangars and two turrets remain the same objects
while the palette switches. No Kenney assets are used by ArtLab.

Use screenshots at one resolution and compare all three palettes under both light
settings. This is a style study, not the final environment or a new playable map.
