# ROCKKIT_R1 — small authored rock kit

Purpose: prove that **our own** low-poly rock shapes can look believable next to the aircraft,
without hiding weak geometry behind a whole terrain study.

## Scope

R1 contains only three reusable modules:

- `Cliff_A` — the main tall cliff / wall piece
- `Ridge_B` — a lower elongated ridge
- `Talus_C` — a rubble / talus cluster for the base of cliffs

No procedural ground, no canyon, no road, no buildings.

## Blender output

Run from the repo root:

```bash
blender -b --python-exit-code 1 --python Tools/Blender/generate_rockkit_r1.py
```

Output folder:

- `Assets/_Project/Art/Environment/RockKitR1/`

Generated files:

- `RockKitR1.fbx`
- `RockKitR1.blend`
- `RockKitR1.ready.txt`

## Unity assembly

Open any existing flight test scene first, then run:

- `Air Combat -> Art -> R1 Assemble Rock Kit Into Open Scene`

The menu command:

- imports the FBX
- creates simple URP Lit materials
- adds non-convex mesh colliders to each module
- places the three modules ahead of the player if a `Player` object exists,
  otherwise around world origin

## Success criteria

R1 is considered good enough only if:

1. each module has a readable silhouette in flat shading
2. there are no self-intersections, exploded triangles or black wedges
3. the modules feel large enough next to the aircraft
4. at least `Cliff_A` looks reusable as a future canyon wall or mountain side

If this still looks weak, the next step is not another procedural landscape. We inspect
free reference assets and reverse-engineer their silhouette language.
