# CA06 — Carryall stationary firing range

CA05 hull and CG01 breakup, including CA05C inertia, are accepted for v0. Further visual polish is deferred. CA06 is an opt-in runtime test in the existing M02 airfield-side preview, not the C1/C2 mission implementation.

## Setup

Open Mission02_ConvoyInterdiction. Keep the installed `M02 / CA01 flight preview`. On `Mission02RouteTrial`, enable **Carryall Firing Range** under CA06 and enter Play (or R if already playing). Start Airborne is not needed. The test fixes the player position and heading automatically using the existing NURS nominal trajectory at cruise speed, with CA01 directly above ground CG01. The flight motor is disabled; the real dual-slot weapon trigger remains active. Retaining nominal cruise-speed inheritance in the stationary lab intentionally reproduces normal NURS launch geometry. The convoy and escort are disposed for this mode. Disable the checkbox and R to restore ordinary M02.

| Input | Action |
| --- | --- |
| LMB | AC01, existing projectile and cadence |
| RMB | NURS, existing ballistic salvo, spread and ground blast |
| J | Begin descent from the initial hold |
| P | Pause/resume pickup motion only (projectiles, weapons and destruction continue) |
| N | Toggle lab-only nominal pitch aiming at CG01 versus the authored NURS angle |
| R | Reset the entire range, ammunition, HP, effects and positions |
| Tab | Existing tactical map pause |

The HUD shows phase, hold status, absolute carrier/cargo root Y, remaining hull/cargo HP and NURS nominal pitch. P can stop descent, clamp, lift or departure. A kill immediately releases the phase hold so the result plays. K/L remain available only in the older non-range preview. N is intentionally a laboratory aiming aid; it changes the nominal launch pitch while retaining the true projectile sweep, spread, blast, cooldown and damage policy. It does not steer in-flight rockets. Use NORMAL for weapon envelope tests; LAB AIM for reaching different heights without repositioning. Its pitch is bounded to 5..85 degrees; hits remain subject to actual geometry and spread. Do not interpret assisted-angle hits as proof of normal mission reachability.

## Rules

- CA01 is Air with a provisional 27.3 x 8.2 x 23.22 box based on authored geometry and a small lower margin; CG01 is Ground with an 8 x 5 x 16 box. HP defaults 5 and 2 respectively, editable on the preview root and applied by R. Colliders and receivers exist only on test instances, and are disabled outside range mode. No shared prefab edits.
- AC01 kills unloaded CA01: CA05 hull crash; CG01 remains independently damageable. AC01 kills loaded CA01: CA05C hull and cargo crash; cargo breaks on contact.
- NURS destroys unattached cargo: CG01 breakup on the pad, carrier aborts and climbs from current pose, then departs empty. No subsequent attachment attempt. NURS destroys attached cargo: both lose damage eligibility immediately and the loaded crash runs; CG01 breaks on terrain impact. This deliberately treats cargo and carrier as linked after capture. NURS does not directly damage an Air receiver; the existing ground blast policy and the cargo kill callback own this outcome.
- No fixed altitude switch: actual muzzle height, projectile trajectory, target collider and blast overlap determine eligibility. No global change to projectile domains or damage rules. The range is not a reward, objective, radar or extraction system.

## Local acceptance

1. At initial hold, LMB until CA01 falls; confirm grounded CG01 remains. R, confirm one live pair and full ammunition.
2. J, P in Descend with payload still on pad; RMB until CG01 breaks. Confirm immediate abort/climb and empty departure.
3. R, J, P at Clamp or early Lift; RMB, confirm both crash when cargo HP reaches zero. Repeat with N (LAB AIM) if the normal-angle salvo misses.
4. R, J, let Lift approach Y78 and P; test AC01 and NURS independently at several heights with R between kills. Record actual root Y and HP changes; repeat near the transition before finalizing damage envelopes. LAB AIM is a diagnostic, not normal weapon range.
5. R mid-fall, after breakup and during abort; test Tab. Disable range and R; verify normal M02 flight, convoy, escort and CA05 preview still work. No C1/C2 instances changed. Unity compilation and Play Mode acceptance remain local; overlap and visuals are not yet measured.
