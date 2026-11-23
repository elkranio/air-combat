# R1 — compact aircraft radar (R1b north-up)

Baseline: R1 radar tested green and pushed (2026-11-23). R1b changes only orientation; AA, projectiles, player movement, camera and weapon balance are unchanged.

## Behaviour

- `AircraftRadar` lives on the `GroundAttackLab` aircraft **instance only**, not the shared `PlayerJet` prefab. Detection radius is **460 world units**, measured in the X/Z flight plane; height is ignored. Out-of-range targets are invisible, rather than pinned to the radar rim.
- North-up display: +Z stays up and +X right. The player triangle turns with the aircraft; contacts retain world bearings and translate only as the aircraft moves. It is still a local radar scan, **not a minimap of the entire level**.
- Red dot = registered hostile AA turret. The red outline around that dot = **the turret's actual `detectionRange` (230)** in matching world scale; both are clipped to the radar disc. The dot and ring disappear when that turret is destroyed, and return on R reset. No fake warning/telegraph or extended weapon reach.
- GroundAttackLab registers its one AA turret as an `IRadarContact`. The sensor owns range and filtering; `RadarHud` owns only drawing. New threats can implement the same interface and be registered by their scene binder. No global FindObjects scan; no extra AA prefab/scene authoring.
- `RadarHud` is a transparent mesh UI drawn in a screen-space overlay Canvas; no raycast target or GraphicRaycaster, so it never consumes LMB. R1 original: bottom-left, above the fuel bar; H1 moves it to the top-right; size scales with screen height and is clamped to **112–166 px**. Screen-height fraction (.15), inset (18,16), and opacity (.82) are serialized fields in the runtime component for later settings work.
- The *diagnostics panel* in both WeaponLab and GroundAttackLab starts **hidden**. Press **H** to toggle it. The persistent afterburner bar remains unchanged and visible. Sandbox is untouched.

## Playtest

1. Open GroundAttackLab: the AA turret at `(85,0,300)` should appear on radar from spawn (distance ~312 < 460). The red ring should NOT reach the centre at spawn (312 > 230).
2. Fly toward it: on entering 230 world units the red ring reaches the player triangle; AA should begin tracking/shooting according to its own rules. Retreat outside 460: both dot and ring disappear.
3. Use A/D or Q/E to change heading: the player triangle rotates; the turret contact stays at its world-relative bearing (apart from actual aircraft movement). Check all four headings and R reset.
4. Approach from an oblique angle and boost W: the turret contact translates relative to the centred aircraft; no flight/camera changes. The red ring must not render outside the radar boundary.
5. Bomb/NURS kill the turret: no red contact while its HP is zero. Press R: turret/aircraft reset and dot returns. Check the new sensor doesn't intercept weapons or touch physics.
6. Check 720p and 1080p layouts: the radar must be readable yet unobtrusive, clear of the afterburner bar, and not block clicking weapons. No AA warning beams. H starts hidden, H toggles; verify WeaponLab also starts without the debug box and Sandbox unchanged.

## Limitations / next

R1 shows **registered AA contacts only**, not terrain, neutral buildings, or every enemy found in the scene. It neither knows where off-range enemies are nor discovers spawned enemies dynamically. Once multiple threat types actually exist, extend registration and category styling; avoid building a generic contact framework prematurely.
