# G4c — NURS camera and approximate impact area

GroundAttackLab-only runtime presentation. **No changes to NURS rocket physics (muzzle 170), 6-shot salvo, G4a maneuver, Q/E, bombs or shared prefab assets.**

- **Select 5:** tactical camera pans forward smoothly toward the *future* salvo footprint; the impact area is drawn as four thin, unfilled corner brackets in the established pale cyan/gray HUD color. It has no crosshair, labels or individual rocket points.
- **Click:** the existing 2.5s cabrage launches six real rockets. The brackets freeze at the anticipated strike area; during the committed maneuver the camera pans farther forward to center that area. Rockets use true G4b collision and damage, not the marker for targeting.
- **Other slots:** marker disappears and the camera returns smoothly to the normal flight framing. The ordinary follow and look-ahead run unchanged; no camera zoom, rotation or prefab edits.
- **Prediction:** uses the live `NursBallistics.LaunchVelocity` and `Integrate` equations and the rack's actual speed, spread and release schedule. Samples nominal trajectories for all six rockets and extreme spread for the first/middle/last, then pads the footprint for their small blast radius. Swept physics collisions check terrain and buildings, ignoring the player's own collider and air targets. This predicts a *rough envelope*, not deterministic hits: future velocity changes, the six random errors, hits on uneven roofs, and frame timing can alter actual impacts.

## Visual test

1. In GroundAttackLab choose **5** and allow the camera to settle. The bracket area should follow turns and sit near the expected impact site. The jet may leave the frame: with the approved 600-ish unit attack distance, 80-unit orthographic half-height and **no zoom**, it is mathematically impossible to show both the aircraft and the far impact area at the same time. This prototype intentionally previews the distant target area; judge whether that tradeoff is acceptable.
2. Start a cabrage and watch the camera settle on the distant bracket. Rocket impacts should generally fall inside/near the four corners. Vary heading, and try launches toward depot roofs.
3. Try Q/E early during the salvo: launched rockets still fly, no new rockets launch, and the stored bracket stays put throughout emergency return. After completion the preview recomputes for the next attack.
4. Switch to **4** and then **1/2/3**: the brackets vanish, bomb nadir marker only appears on 4, camera returns. Test R and repeat. WeaponLab and Sandbox are unchanged.

If target-centric framing feels too detached from the jet, the next feel adjustment is a negotiated preview/zoom balance rather than misleadingly moving the true ground-impact marker toward the aircraft.
