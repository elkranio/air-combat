# GUN-02 — arcade tracer / muzzle juice (visual only)

**Status:** authored for main-scene Play Mode evaluation; not yet visually approved. Applies to the modular `AC01_Burst` preset introduced by GUN-01. No combat-balance or input changes.

- The first modular autocannon keeps its four-shot cadence, 0.45s burst cooldown, 420 muzzle velocity, 430 range and 1 damage. Fire remains fixed to aircraft root forward and LMB-triggered.
- `AC01_Burst.asset` now owns adjustable **presentation-only** values: `arcadeCoreWidth` (.72), `arcadeGlowWidth` (2.8), `arcadeGlowAlpha` (.64), `arcadeFlashSeconds` (.085), plus a glow material reference. Tune these on the existing preset; duplicating a preset creates a different look without changing pilot or AI code.
- `ArcadeTracerGlow.shader` is an additive URP unlit *soft-width* gradient, not a postprocess/Bloom dependency. A warm wide bloom-like layer surrounds the existing brighter, narrow tracer; a small additive flare extends from each muzzle socket during firing. Effects do not cast shadows or create realtime lights.
- The shader has a material asset (`Art/Weapons/AC01_TracerGlow.mat`) explicitly referenced by the preset, so it is retained in player builds. Legacy `WeaponController` emitters receive no arcade config: `WeaponLab` and `GroundAttackLab` retain their previous tracers and flashes.

## Test gate

1. Apply patch after GUN-01 green, let Unity import the new shader/material, and open `ArcadeDesertE1.unity` in Game view.
2. Hold LMB through multiple bursts. Compare against muted sand, dark asphalt, and the buildings; fly at different speeds/headings and check whether the wider, soft glow is visible but **not a fat neon tube**. Check muzzle flare at each gun socket, tracer lifetime, and whether there is unwanted screen flicker or pink shader fallback.
3. Test a green/bright background later when available. If excessive, reduce `arcadeGlowAlpha` first, then `arcadeGlowWidth`; if too thin, raise them on `AC01_Burst.asset`. No scene or prefab rewiring / installer required.
4. Regression-check `WeaponLab` legacy BURST, `GroundAttackLab` 1–6 and the HUD/radar. Unity compile / rendered look require local user verification.

**Next after visual green:** HUD-02 — mouse-wheel cycling of equipped modular weapons, bottom-left combat status panel flush with left screen edge and about 20% shorter, selected weapon ID plus a readiness bar that empties on burst and refills over cooldown. Distinguish the *burst firing phase* from *cooldown*: the UI must not claim READY while rounds are still being emitted. Keep weapon UI independent of a particular weapon type; a second weapon can be equipped later without rewriting the HUD.
