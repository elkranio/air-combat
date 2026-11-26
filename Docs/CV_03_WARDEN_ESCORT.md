# CV03 — Warden convoy escort

Base `19431bb`. CombatSandbox / Convoy retains the six visual-only Pickets and spawns two instances of ED01_Warden. AC-01 only; no NURS, ground damage, deployment or campaign changes yet. Drones follow the convoy head at fixed XZ offsets at the player's test altitude, with no pursuit or evasion. Their patrol preview is disabled, but rotors, health, radar, turret tracking, projectiles and hit feedback remain active. The authored Warden trial is hidden only in Convoy mode.

Inspector on CombatSandboxEncounter: Escort Alpha/Bravo Offset uses X = lateral (right positive), Y = longitudinal (forward positive), relative to the convoy head. Defaults (-55,-45) and (55,-105) with 30-unit Picket spacing. Acquire delays .15 and 1.25 seconds apply when entering range, while existing burst cadence, accuracy and damage remain unchanged. Independent counters and clocks; no shared firing scheduler. Positions can be changed before or during Play. At the route end surviving escorts hold position and continue firing.

Local gate:
- Play Convoy; H shows both drones' HP and hit/fired counts; radar shows two air contacts and six unarmed ground contacts.
- Kill Alpha; it disappears, produces a wreck and its rounds clear. Bravo continues. Kill Bravo; ESCORT DOWN appears but the convoy continues moving until its route ends. R restores both escorts and six trucks, clears old projectiles and effects, and does not duplicate contacts.
- Fly into overlapping firing range, compare two firing windows, test player defeat and R, and test altitude keys 7/8/9. Try alternate offsets in Inspector. Switch to standalone Warden and verify circular patrol and original weapon behavior; legacy sandbox modes and M01 unchanged.
- Unity compile/import and Play Mode still need local verification; offsets and acquisition delays are provisional tuning.
