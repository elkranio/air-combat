# G4c — NURS camera and approximate impact area

GroundAttackLab-only runtime presentation. **No changes to NURS rocket physics (muzzle 170), 6-shot salvo, G4a maneuver, Q/E, bombs or shared prefab assets.**

- **Select 5:** camera adds modest look-ahead while retaining the jet within the Game viewport; the physically correct far-off salvo footprint remains four thin unfilled brackets in pale cyan/gray, with no crosshair or individual predicted impacts.
- **Click:** the existing 2.5s cabrage launches six real rockets. During commitment the camera moves slightly farther ahead **but never loses the aircraft**. The impact-area marker stays in world space; rockets use true G4b collision and damage, not the marker for targeting.
- **Other slots:** marker disappears and the camera returns smoothly to the normal flight framing. The ordinary follow and look-ahead run unchanged; no camera zoom, rotation or prefab edits.
- **Prediction:** uses the live `NursBallistics.LaunchVelocity` and `Integrate` equations and the rack's actual speed, spread and release schedule. Samples nominal trajectories for all six rockets and extreme spread for the first/middle/last, then pads the footprint for their small blast radius. Swept physics collisions check terrain and buildings, ignoring the player's own collider and air targets. This predicts a *rough envelope*, not deterministic hits: future velocity changes, the six random errors, hits on uneven roofs, and frame timing can alter actual impacts.

## Visual test

1. In GroundAttackLab choose **5** and let the camera settle. The jet must stay visible with at least a 20% viewport margin from the extra G4c pan; turning and Q/E must not push it offscreen. At ~600m range, the actual four-corner ground marker will usually be **outside** this unzoomed camera view.
2. Start a cabrage and verify a little more lead while the jet remains visible. For strike accuracy, inspect the actual area and hits in Scene view: rockets still hit at real distances; the marker never shifts closer just to fit on screen.
3. Try Q/E early during the salvo: launched rockets still fly, no new rockets launch, and the stored bracket stays put throughout emergency return. After completion the preview recomputes for the next attack.
4. Switch to **4** and then **1/2/3**: the brackets vanish, bomb nadir marker only appears on 4, camera returns. Test R and repeat. WeaponLab and Sandbox are unchanged.

**Known UX gap:** the player cannot simultaneously see their aircraft and a ~600m impact bracket at the current orthographic scale. The safe follow-camera hotfix prioritizes flying; a later dedicated tactical preview, temporary zoom, or split view must address long-range aiming. Do not falsify the projected impact location.
