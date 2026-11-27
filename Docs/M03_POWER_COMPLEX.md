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
