# B1a — first base asset: three barracks
> **Historical atom — not an active installation instruction (2026-11-24).** The three barracks already exist in the main map and were updated by B1b. Do not rerun B1a, or adopt its old Y150 startup instructions. Current state: [BASE_DESIGN.md](BASE_DESIGN.md).

---


**Status:** model sources and one-time scene installer prepared; actual Play Mode look **not yet approved**. This changes only the *existing* `ArcadeDesertE1.unity` when the installer is run. It is not a whole-base generator.

## Install

1. Apply the B1a patch on the cleaned, green `master` (tested against `5647d25e9f7e76b58cf054ab5397e7ef4568c477`). Stop Play Mode and save/discard dirty scenes.
2. Let Unity import the four `Barracks_B1a_*.obj` models and compile the new Editor script.
3. Menu: **Air Combat > Art > B1a - Install Three Barracks + Base Approach** (once only).
4. Open/Play `Assets/_Project/Scenes/ArcadeDesertE1.unity`; use **R** for the new approach reset. **7/8/9** still compare altitude 78/110/150; start at Y150.
5. After accepting the playtest, commit the changed scene, generated prefab at `Assets/_Project/Prefabs/Environment/Base/Barracks_B1a.prefab`, generated materials at `Assets/_Project/Art/Buildings/Base/Barracks_B1a/`, and **all Unity `.meta` files**. The installer refuses to run a second time; subsequent iterations edit prefab/source and scene directly.

## Asset and placement

- **One source building; three instances** in a tidy row, spaced 25 m center-to-center, along X, buildings' long axes along Z. Barracks unit approximately **32.1 m long × 11.7 m wide × 6.2 m high**. BoxCollider, URP shadow casting/receiving, no flight/crash collision logic yet.
- **Massing:** low single-story block, simple pitched gable roof and small eaves, visible foundation, large dark window panels / doors, sparse trim and one roof ridge. No interiors, no normal/albedo/AO bake.
- **Palette:** cool neutral sage-grey walls `#B8C2B9`, dark blue-slate roof `#53666B`, deep navy-charcoal doors/windows `#293B42`, grey trim `#777F79`; all URP Lit, metallic/smoothness 0. Evaluate under the existing Day lighting rather than changing the sun.
- **Editable source:** `Tools/Blender/generate_barracks_b1a.py` (plain Python, reproduces four OBJ source sections; import/edit them in Blender). The imported `.obj` meshes are nested in a scene-neutral reusable prefab. To rebuild geometry run `python3 Tools/Blender/generate_barracks_b1a.py` from repo root, then allow Unity to reimport. No textures or bake needed for this visual gate.
- **Base sector (not the whole future base):** barracks row centered near **(-485, 0, -270)**. Existing large plateau stays intact at approximately **(110, 0, 135)**. No road, warehouse, fence, parked vehicles, airstrip or extra plateau in this atom.
- **Flight start:** jet moves to **(-362, 150, -393)**, facing NW toward the barracks row. The accepted **fixed-world C3 camera** rig relocates with it; look-ahead unchanged. This deliberately places the row in the initial gameplay view, rather than spawning the player above the buildings. `R` returns to the new spawn.

## Visual gate

At normal Game view / Y150, does the row read as **three real barracks**, not toy sheds? Can the player judge their scale vs the Kestrel and the huge plateau? Are walls, roofs and openings legible without garish colors? Does the **real** URP shadow read on the ground and around eaves while moving? Judge shape and composition first; only then consider a restrained AO bake if recesses actually need it.

The existing combat labs and their prefabs are untouched. This is still an art-only scene; weapons and damage remain in the regression labs.
