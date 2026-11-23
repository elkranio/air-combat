# B1d — access road, barracks service lane and prepared ground

**Status: implemented as a one-time editor authoring pass; Unity Game-view verdict pending.** Uses the *existing* `Assets/_Project/Scenes/ArcadeDesertE1.unity` with B1b barracks, B1c HQ courtyard and the preserved plateau. Start altitude remains **Y78**. No new buildings, vehicles, runway, flight mechanics or lighting changes.

## Install

1. Apply B1d patch on the green commit and let Unity compile `InstallBaseRoadB1D.cs`.
2. Exit Play Mode and **save/discard open scene changes**.
3. Run **Air Combat > Art > B1d - Install Road + Prepared Ground** **once**.
4. Open/play the main scene. Check the first approach and fly around the three barracks and HQ. Compare road/soil contrast and wear at normal Game view, not just in a zoomed Scene view.
5. On green, commit the edited **`.unity` scene and `E1_Terrain.asset`** plus generated `BaseRoadB1d/` assets, road prefabs and all Unity-generated `.meta` files. The original pre-B1d soil texture/terrain layer, HQ, barracks and plateau are not overwritten.

The installer deliberately refuses to run twice or overwrite its outputs; **edit the generated meshes/materials/prefabs and Terrain texture painting afterward**. The Editor installer is the reproducible source for the initial geometry and terrain stamping. It is not runtime geometry generation.

## Site composition

- **Prepared soil:** an editable `B1d_PreparedGround` Unity TerrainLayer (`PreparedGround.terrainlayer`, matching 128px tile texture). Painted directly into the existing terrain alphamap, with rounded, loosely irregular, ~10–13m soft edges and a very small large-scale color variation. The darkening is *subtle*, not a full black-asphalt base or a flat decorative slab. It sits behind and around barracks, HQ and the arriving access road; the rest of the 2×2km sand terrain is untouched.
- **01 / Road - Access:** north-south, ~9.4m asphalt core with slightly lighter dusty shoulders; reaches the site from the south and meets the barracks service lane.
- **02 / Road - Barracks:** east-west service lane, ~8.7m core in front of the barracks row, feeding the access road.
- **03 / Road - HQ_Entry:** short ~7.6m connector ending at the west edge of the existing independent HQ concrete courtyard slab. The slab itself is not edited.
- **04 / Road wear - sparse:** a separately movable/disableable reusable prefab containing a handful of muted repair patches and *broken, faint* skid/tire traces. No random trash specks, painted cast shadows, signs, painted arrows or high-contrast racing stripes.

Three separate road prefabs each have independent Shoulder and Asphalt child renderers, authored mesh assets, matte URP materials and surface collider. Roads do not cast fake elevated shadows; the original buildings cast real URP shadows onto road/Terrain. Wear itself has no collider. The original plateau stays at approximately `(110,135)`, and barracks/HQ stay at their accepted positions.

## Visual gate and next work

At Y78 the base should read as a **single established site**, not separate model props scattered on a color-fill desert. Road widths must support exaggerated arcade vehicles later, not read as highways; edge and ground treatment should feel worn but *clean*, with broad silhouette and negative space preserved. If the paving looks too dark, change `B1d_Asphalt.mat`; if soil looks too uniform, use the Terrain paint/edit tools rather than adding clutter. No AO or directional-light bake yet.

**Next after green:** a controlled variation pass on one already-accepted building (e.g. limited roof wear/repair), then an independent warehouse/maintenance-hall test. Avoid changing everything in one atom.
