# M03 — Power complex / visual trials

This is the current environment direction for the protected site, superseding the older off-map target proposal in MISSION_03.md. No combat logic is introduced by the first art pass.

- Primary mission concept: two Sparrow groups threaten a fictional power complex. Protect three individually critical props (two transformers and a control building); loss of any one fails the mission. The precise objective implementation is deferred. Player loadout stays AC01 + AAM, no friendly-fire system for the site in this pass.
- PG01 is a reusable visual transformer prefab; its local concrete footing is not the whole site. Build the first preview via **Air Combat > Missions > M03 - Build PG01 Transformer Preview**. The authoring tool creates the instance once and preserves its pose on subsequent rebuilds. It never regenerates the scene or changes the friendly runway (45 degrees).
- Art direction: clean low-poly industrial silhouettes and neutral muted colors. Readability at the existing flight camera matters more than engineering fidelity. PG01 V1B preserves the 24 x 18 unit footprint (10.72 high), with an offset rounded upper assembly, a separate service housing, clearer ceramic silhouettes and a shallower local footing. PG01 V1B is visually accepted; PG02B control building is also visually accepted. Preserve both models during later art passes.
- PG03 V1 is the second transformer visual trial: 28 x 20 units, 10.1 high, with a longer body, split cooling silhouettes, and two rows of stylized ceramic details. Use **Air Combat > Missions > M03 - Build PG03 Transformer Long Preview**. New instance defaults to 36 units east of the original PG01 position; rebuilds preserve existing instance pose. No combat behavior.
- Future site composition: one connected yard with appropriate concrete/gravel, a service road leading out into the broader road network, a few stylized transmission pylons and lines continuing beyond the site, sparse trees and irregular vegetation outside the yard, and terrain transitions that integrate it into the green biome. Avoid a giant isolated rectangular pad or a uniform ring of trees. Add these only after the core props and composition are accepted.
- No target routing, destruction, SAM changes, new collisions, or tactical-map markers in PG01.

Source: `Tools/Blender/build_pg01_transformer.py`; it regenerates `Assets/_Project/Editor/PG01Geometry.json`. Running it in Blender also writes the editable `Resources/Blender/PG01_Transformer.blend`. Keep prefab/mesh/material asset paths stable during art passes.

PG03 source: `Tools/Blender/build_pg03_transformer_long.py`, generating `Assets/_Project/Editor/PG03Geometry.json` and (in Blender) `Resources/Blender/PG03_TransformerLong.blend`.

## Composition decisions after visual trials

PG03B and PG04A accepted for visual composition. PG05 is a decorative pylon and span preview; build with **Air Combat > Missions > M03 - Build PG05 Power Line Preview**. It creates two reusable prefabs and a temporary group of three pylons and two spans. Rebuilds preserve existing poses. Source: `Tools/Blender/build_pg05_power_line.py`, with editable Blender output when run in Blender.

PG06: remove broad individual display slabs from the three major props when composing a single continuous yard. Retain only integral equipment bases and small structural feet. Use one coherent shared surface with local variation and access paths. Wear, surface details and vegetation transitions are deferred to polish. Never move the friendly airfield during site authoring.

## PG06A — shared power complex yard

PG01B, PG02B, PG03B, PG04A and PG05A are accepted visual assets. PG06 is an authored composition pass, not new combat behavior.

Open the existing `Mission03_SparrowInterception` scene, then use **Air Combat > Missions > M03 - Compose PG06 Power Complex** once. Save the scene after reviewing. The command reuses the named preview instances or creates missing ones from the existing prefabs. It intentionally adjusts the site poses only on the first run. A second run does nothing; use the normal Unity tools to edit the composed layout. The previous preview commands can still rebuild their meshes without moving their named instances.

The continuous gravel yard has a clipped perimeter, service lanes, open southern access gate, low fence, and decorative links to the existing power line. Large display slabs on the three main props are hidden with scene-only renderer overrides; equipment bases and small feet remain. PG02's original entry step shares the hidden mesh, so a matching step is recreated as part of the yard. Do not apply these scene overrides back to the shared prop prefabs. Yard assets are in `Art/Environment/Infrastructure/PG06_PowerComplex`.

The existing meadow, 45-degree friendly runway, aircraft, HUD, and flight settings are unchanged. No new collision, objectives, SAM or damage components. The yard remains inside the existing flat site reserve. The normal tactical-map auto-bake still applies; no map-specific artwork is added in this pass.

Next: review in gameplay camera, then connect the southern access road to the wider environment, add sparse trees and terrain transitions, and polish surface wear/variation. No real engineering simulation is implied by the decorative scenery.
