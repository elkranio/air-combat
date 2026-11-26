# CV01 — Picket mobile visual and deployment

Base `6383003`. New animation-ready visual prefab, no combat or movement changes. Existing Mission01 Picket prefab, scene, assets and rail presentation are untouched. The mobile visual shares the accepted unchanged geometry/materials and splits the previously combined body/support/rail meshes. It is not a second SAM gameplay unit.

- `Assets/_Project/Prefabs/Environment/SAM01_Picket_Mobile.prefab`: default march pose, launcher horizontal and supports retracted above the ground. Separate yaw and pitch transforms; reusable side support meshes. No launch component or damage system in this visual-only atom.
- `PicketDeploymentVisual`: visual-only reversible pose transition, default 2.4 seconds. Arms extend, legs lower, then the rails finish raising. `SetDeploymentTarget` / `SetDeploymentImmediate` and `IsDeployed` are the composition contract for a later atom; it does not arm anything.
- To review, open CombatSandbox, place a temporary prefab instance beside the flight route on flat ground, inspect its march pose. Enable **Preview Deploy On Play** on that instance and Play; deployment starts after Preview Delay (2 seconds). Alternatively use Start Deployed to inspect the final pose in Play. Remove the trial instance before saving. No scene regeneration.
- Check all support feet clear the ground in march and touch down in deploy, the rails and rockets rotate as one, no cab/rail/support clipping, and the model is readable at actual flight zoom. Confirm existing Mission01 Pickets and launches are unchanged. Unity import and Play Mode are local acceptance gates.
- Source: `Tools/Blender/build_sam01_picket_mobile.py` imports the accepted original geometry recipe without running its exporter. Python regenerates only the split OBJ groups; Blender additionally produces an editable reference blend. No need to run it for normal Unity import. Blender execution not performed for this patch.

## Next atoms

CV02: straight convoy of six vehicles, marching (unarmed). CV03: two Warden escorts at adjustable offsets with independent firing cycles. CV04: combat playtest. CV05: ground damage and NURS. CV06: arrival/deployment animation and explicit arming only after deployment completes; adapt existing Picket rail binding to the new hierarchy rather than introduce another launcher. Mission02 composition not part of CV01.
