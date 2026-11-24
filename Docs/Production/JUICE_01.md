# JUICE-01 — confirmed aircraft hit feedback

Base `71192e7` / FLIGHT-03b green. Authored for the next Unity Play Mode check; no damage, flight, weapon, enemy behavior or camera framing tuning.

- `PlayerHitPoints.Damaged` reports only accepted damage after HP changes. `Air01Encounter` opts in to `CombatHitFeedback` for the player and each live Tiger. Labs have no listeners and keep their previous presentation. No new scene or prefab references.
- Hull flashes pale yellow-white for 0.09s using cached renderer slots and temporary property blocks, then restores prior overrides. The existing AC-01/02 projectile sparks remain. A scene-owned bounded particle pair adds bright fast sparks and darker slower debris at the accepted hit point; larger burst on fatal damage. A short synthesized metallic impact sound (placeholder) and heavier fatal sound distinguish outcomes. Incoming player damage also flashes red at the screen edges.
- `ArcadeCameraFollow` applies a screen-axis translation after the ordinary follow pose, never changing rotation, smoothing or heading. Initial normal-hit amplitude/duration 0.28 units / 0.12s; incoming 0.65 / 0.19s; fatal 2.1 / 0.38s. Strong impulses take priority over weaker ones; reset clears offsets. No shake from a shot or a miss. Parameters are on the runtime CombatHitFeedback component on the encounter object (change defaults in its script for persistent tuning).
- Each Tiger is untracked on removal; a fatal hit's particles/audio outlive its removal. R/map reset clears residual particles, sound, flash and shake. No changes to radar or hit rules. Shader uses existing SandboxEffect; no external assets, lights or collision particles.

## Play Mode gate

1. In ArcadeDesertE1, fire AC-01 and AC-02 into empty sky: no hit feedback or shake. Hit Tiger: visible short flash, fragments, audible impact and subtle camera movement. Check rapid consecutive hits do not leave materials white or cause drifting camera.
2. Kill either Tiger: noticeably stronger impulse and larger sparks/impact sound; verify radar removal and surviving Tiger behavior. The dead plane still disappears for this atom (falling wreck belongs to JUICE-02).
3. Take hits: more pronounced shake and red edge flash. Lose or reset during a hit: no stuck overlay, flash, audio or shake. R repeatedly, check no duplicated FX objects/listeners.
4. Compare WeaponLab and GroundAttackLab with baseline; confirm no new feedback or camera changes there. Check Console and frame rate under sustained fire.

Next JUICE-02: critical HP smoke, detached noncombat wreck, burning tumble/fall and ground explosion; strong destruction shake is already available. JUICE-03: short wingtip trails during hard turns. Then missiles.
