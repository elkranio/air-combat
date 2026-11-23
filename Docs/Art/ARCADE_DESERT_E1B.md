# E1b — muted ground, authored chunky outcrops, higher flight and paint

**Status: implementation delivered, Unity Game view verdict pending.** E1 scene already exists and is the permanent large-map work area; **do not rerun E1**. The map remains 2000×2000 and editable.

## Install once

1. Apply the E1b patch on the committed E1 baseline. Let Unity import three OBJ sources and compile.
2. Stop Play Mode, save/discard dirty scenes. Run **Air Combat > Art > E1b - Install Muted Rocks + Aircraft Paint**.
3. Play the **existing** `Assets/_Project/Scenes/ArcadeDesertE1.unity`; do not create another scene. On green, commit scene, E1B output assets and generated `.meta` files.

This installer deliberately refuses to run twice or overwrite its own output. The **original small-rock group is disabled, not deleted**. The original bright ochre TerrainLayer remains an asset. Only E1's terrain-layer reference, local scene objects, player instance and camera height change. Shared gameplay scenes, original player prefab and lighting rig are untouched.

## Exact art pass

- **Ground:** uniform, matte, no photo noise; light-medium muted warm grey sand `#B8A993` instead of the earlier saturated `#DDA44D`. Geometry remains a sculptable native Unity Terrain; do not confuse flat color with locked flat height.
- **Models:** `Models/Chunk_Broad.obj` (~33×25×16m), `Chunk_Ledge.obj` (~49×31×21m), `Chunk_Upright.obj` (~23×21×28m). Real standalone OBJ sources, editable in Blender; about 42–48 flat-shaded faces each. Broad bevelled/stepped forms, no spikes, circles, fine texture, or random noise. E1b has **18 individually editable placements**, spread through the 2km map, with several formations near the initial flight corridor. Rock palette `#9F8D7A` / `#B09B85` / `#83786E` and real shadow-casting MeshRenderers/MeshColliders.
- **Aircraft:** E1 scene-only Kestrel paint variants, hull `#CCD3D1`, upper `#496775`, panels `#D67A50`. The existing jet geometry and its shared prefab are unchanged. This is a color direction test, not approval of the final model.
- **Altitude:** E1 player spawn rises **Y48 → Y78**. Camera starts 30m higher as well, preserving its relative tracking height and **accepted fixed 50° C3** composition/look-ahead. Other combat labs retain their existing altitude. Real shadows come from the **existing CombatLightingRig**, not baked projection or a new sun.

## Visual gate

Look at flight zoom, not the Unity overview. Does the muted ground recede behind the more readable jet? Are the three outcrop silhouettes convincingly chunky rather than toy rocks/low-poly spikes? Can you read their contact shadows and the aircraft's height without the small-rock/shadow scale confusion? Check near/far placements and 2–3 sharp turns at cruise/boost. Preserve long clear corridors for future AA/SAM/structures.

Not in this atom: jet remodel, terrain sculpting, canyon collisions/crashes, new base structures, gun tuning, VFX and new lighting. These come as separate passes **in this same map**, based on the first visual result.
