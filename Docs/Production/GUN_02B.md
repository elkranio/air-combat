# GUN-02b — high-juice tracer pass (visual-only)

**Status:** authored against GUN-02 green (`28457f6a`); requires Unity Game-view approval.
No damage, velocity, range, burst, cooldown, target domain, flight or input changes.

## Why

GUN-02's first additive halo is too weak on the sand background: it reads as a yellow dash. The GUN-02b pass makes an intentionally exaggerated arcade round, closer to the provided Jet Attack reference, without requiring URP Bloom.

## Construction

- The modular AC-01 uses **three simultaneous streaks**: white-hot additive core, stronger golden mid glow, broad soft orange outer glow. All three share the same ballistic trajectory and are destroyed with the projectile; no projectile shadows or dynamic lights.
- The **hot leading head** is a short radial additive line quad, tracked at the actual projectile front. This produces a luminous bullet tip, rather than a glowing uniform rod.
- A brighter short-lived muzzle flare uses the existing authored left/right chassis sockets.
- On *successful air-target damage only*, four short additive sparks spawn at the hit point. The GUN-01 physical collision and damage rules are untouched; no ground-hit effect is added.
- Legacy `WeaponLab` and `GroundAttackLab` still use their previous tracer path because they do **not** call `ConfigureArcadeVisuals`.

## Tuning

In `Assets/_Project/Prefabs/Weapons/AC01_Burst.asset`:

- `arcadeCoreWidth` **0.82**;
- `arcadeGlowWidth` **3.4**, `arcadeGlowAlpha` **0.9**;
- `arcadeOuterGlowWidth` **7.5**, `arcadeOuterGlowAlpha` **0.24**;
- `arcadeHeadSize` **2.4**, `arcadeImpactScale` **1.0**;
- `arcadeFlashSeconds` **0.085**.

The shared additive material `AC01_TracerGlow.mat` uses emission strength **2.8**; separate `AC01_TracerCore.mat` and `AC01_TracerHead.mat` use **3.9** and **4.2**. All use `ArcadeTracerGlow.shader` with both streak and radial modes. Toning down starts with **glow material intensity**, `arcadeGlowAlpha`, `arcadeOuterGlowAlpha`, then `arcadeHeadSize`. These are shared asset values; changing the source asset affects every mount that references it.

## Test in Unity

Open `ArcadeDesertE1.unity`, Play, fire along multiple screen directions, over sand and dark apron. Judge the streak at *normal* game resolution, not just a magnified screenshot. Check bright head vs soft tail, muzzle punch, absence of pink shaders and opaque yellow sticks, correct movement at flight speed, and no stray trails after a round despawns. The static Tiger is **not** a damageable encounter, so hit sparks need a future real airborne damage target to be visible. Confirm existing WeaponLab/GroundAttackLab tracers have not changed.

**Next:** visual green, then the already planned HUD-02 weapon wheel + readiness display. No HUD work in this atom.
