# B1c — command headquarters + modular courtyard
> **Historical atom — already installed (2026-11-24).** The HQ, courtyard and flags are present in the main map. Do not rerun the one-shot installer. Current state: [BASE_DESIGN.md](BASE_DESIGN.md).

---


**Status: authored first pass; Unity Play Mode visual approval pending.** Work in the existing `Assets/_Project/Scenes/ArcadeDesertE1.unity`, never regenerate the map. Keep the three accepted barracks at approximately X=-485/Z=-270 and the large plateau at X=110/Z=135. The HQ site is around **X=-422/Z=-320**, by the barracks but offset to leave space for a future connecting road. The main map and the two combat-regression scenes remain independent.

## One-time install

1. Apply this B1c patch against the latest green `master`. Allow Unity to import the 14 `HQ_B1c_*.obj` meshes and compile the Editor script. **Save/discard** unsaved scene changes; stop Play Mode.
2. Run **Air Combat > Art > B1c - Install HQ Courtyard + Y78** once.
3. Play `Assets/_Project/Scenes/ArcadeDesertE1.unity`. The aircraft keeps its previous X/Z approach and heading, but now **starts at Y78**. **R** resets to Y78, while **7/8/9** still compare Y78/Y110/Y150 in Play Mode. No flight-controller, camera prefab or weapon changes.
4. After confirming the composition, commit the modified `.unity` file, five generated prefabs and four new materials **with Unity-generated `.meta` files**, and the imported OBJ `.meta` files. The one-time installer refuses to overwrite existing assets or reinstall its site; manually edit the scene/prefabs after the first pass.

## Reusable separate pieces

| Scene object | Reusable prefab | Geometry and use |
| --- | --- | --- |
| `01 / Command building` | `HQ_B1c_Command.prefab` | 29×19 m one/two-storey block; separate body, roof, windows, trim and accent meshes; foundation, roof volumes and central entrance read from flight camera |
| `02 / Entrance + steps` | `HQ_B1c_Entrance.prefab` | Independent landing, two steps, heavy canopy and columns; not baked into the building |
| `03 / Front courtyard paving` | `HQ_B1c_Plaza.prefab` | Independent 38×22 m hardscape pad; the next road atom can attach without remodeling the HQ |
| `04/05 / Planter` | `HQ_B1c_Planter.prefab` | Two independent troughs with geometric greenery and visible soil; movable/reusable |
| `06 / Flagpole` | `HQ_B1c_Flag.prefab` | Independent 12 m eight-sided pole with simple opaque flag mesh |

**Source and palette:** `Tools/Blender/generate_hq_b1c.py` rebuilds the OBJ meshes using only Python standard library (Blender optional). The OBJ files are authoring source geometry, not one runtime blob. Existing B1 barracks' Walls, Roof, Openings and Trim materials are **reused**, so later recoloring of accepted base materials stays coherent. B1c adds just Accent `#C48150`, Concrete `#9BA39F`, Soil `#6C6458` and Green `#627564`. URP Lit, rough/matte, shadow cast and receive ON, simple colliders. **No albedo/AO/direct-light bake.** No road, runway, vehicles, AA/SAM or new plateau in this atom.

## Playtest gate

- Fly into the barracks and HQ from the existing approach at **Y78**; examine roofs, courtyard silhouette and readable contrast against muted sand.
- Compare HQ to the repeated barracks: related architectural materials, but HQ visibly more important without becoming a cartoon town hall.
- Check dynamic shadows on building and paving; avoid judging from one sun direction only. Verify no shadows get painted into the materials.
- Move the five site components independently in Hierarchy or use their prefabs on later base sectors. Ensure none depend on scene-local reference to the barracks or obsolete art labs.
- Green means scale/style/contrast work in **moving Game view**, not that the entire military base or final bake workflow is approved.

**Next:** B1d road/asphalt connectivity between HQ plaza and barracks; then contrast the three materials/shape categories (barracks, HQ, road) before further construction.
