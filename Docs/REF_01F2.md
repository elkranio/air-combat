# REF-01F2 — Shared flight session

Base: `88afd56591edede44568d8cd4795b62dc8b75289`.

`E1MapFlightSession` configures the player, camera, maneuverability, radar and HUD
in both Mission01 and ArcadeDesertE1. Depend on `IFlightSessionEncounter` instead
of the concrete Air01Encounter. Its existing Bind, Restart, SetAltitude and
DebugStatus methods implement the interface without changes to the sandbox logic.

Standalone retry restores the player pose/camera and restarts the scenario.
Campaign retry restores only the pose/camera; Mission01LaunchSequence owns its
other reset side effects. Mission01 has no Air01Encounter, so its actual retry
sequence is unchanged. Retain execution orders -80/-70/-40, H and 7/8/9 debug
controls, prefab settings, script identity, scene and serialized fields.
Rename the historical session/scene in the next atom, not this one.

## Local acceptance

- Mission01: takeoff, HUD/camera, R on ground and airborne, R after failure.
- ArcadeDesertE1: all three modes bind and R resets the encounter; H and 7/8/9.
- Terrain01Playground (if still used): no encounter and no regression in flight.
- Unity compile and Play Mode are the acceptance gate.
