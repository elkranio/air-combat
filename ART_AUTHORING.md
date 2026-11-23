# Model authoring and bake rules

Current goal: a readable stylized arcade military base built on the accepted 2×2 km muted desert map. [Active B1 plan](Docs/Production/BASE_DESIGN.md).

## Geometry first

- Treat Unity units as meters for proportion comparisons, but allow stylized exaggeration of gameplay-important objects. Keep rooflines, doors, vehicle silhouettes, and large structural volumes legible from yaw -45°, 50° pitch, ortho 80.
- Prefer deliberate meshes (Blender `.blend` / OBJ or FBX), not procedural noisy triangles. Check face winding/normals, manifoldness for collidable solids, model axis and unapplied transforms. Don't replace a strong large silhouette with small details.
- Reuse big modular pieces only when joins are invisible from gameplay, and test collision separately. A collider's presence alone does not make the current Transform-moving aircraft crash: explicit collision gameplay comes later.

## Material and lighting contract

- Unity 6.6 URP Lit, non-metallic matte surfaces as default. Controlled albedo swatches and light/shadow contrast carry the forms. Ground remains muted and largely flat-color. Keep relative screen-space detail coherent across ground, plateau and buildings.
- Use `CombatLightingRig` with only one scene sun, actual cast/receive shadows, no painted drop-shadows or baked direct-light patches.
- Use baking **only for useful local detail** (soft AO at eaves/recesses, modest normal depth), not for an invented full-map texture style. Preserve source maps, use sane texel density, avoid UV stretching, and inspect under gameplay light and movement.
- `ArcadeCombat_URP` 600m/4 cascades/4096 is a test candidate. Don't assume infinite shadow distance or zero performance cost. Check screen-edge shadows and shadow quality across height/plateau transitions.

## Delivery checklist

Readable silhouette at gameplay zoom; correct proportions to Kestrel and 430×275×30 plateau; matte colors contrast against `#B8A993`; correct shadows and colliders; no missing materials; reasonable asset weight; reproducible source and `.meta` files included. Keep only approved assets in the main map.
