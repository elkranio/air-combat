# REF-01C3 — reusable Picket launch profile

Base `805775e37643430a257b080b9c53d5ab689be5b6` (C2 green).

`SurfaceToAirLauncher` already owns its LateUpdate, radar acquisition, HP, reload, projectiles and rail presentation. Instead of adding another controller, name the two existing runtime profiles explicitly (Legacy and Picket). The Picket binding API no longer implies Mission01. Any composition can bind the Picket target, arm/disarm it, cancel its projectiles and reset the encounter. The existing legacy BindTarget remains unchanged; bind one profile per launcher lifetime as before. The mission still owns arming/disarming at its phase transitions and cancellation on landing/retry. The launcher remains responsible for acquisition and firing, not mission phases.

Move shared warning cadence to `MissileWarningTiming`; the launcher and warning HUD no longer read constants from EnemyMissileStressTest. That class retains constant aliases for existing consumers. Its warning cue provider and HUD binding remain until presentation/sandbox cleanup. The three values (.18/.105/.48) are unchanged. No serialized field, execution order, prefab, scene, existing GUID, missile behavior, rail timing, audio waveform or tuning changes. Legacy SAM1 lab behavior stays in place until CLEAN-03.

## Local regression gate

- Compile. Check Mission01 with no missing reference or assertion. Picket radar and map contacts remain.
- Normal W takeoff: no acquisition on ground, then warning vector, three beeps, rail tracking/launch/reload during flight. Leave range during warning to cancel. Multiple shots can overlap as before.
- R with missiles airborne: reset HP, rails, warning, radar and active shots. Boundary/crash/landing still disarm. Landing cancels active SAM projectiles. Tab pauses as before.
- Check GroundAttackLab SAM1 still uses legacy acquisition beeps and one shot at a time. Check existing AAM warning HUD/audio if used.
- Unity compile and Play Mode are the acceptance gate. No authoring or scene save required.
