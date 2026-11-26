# CLEAN-03B - Retire unreferenced legacy scripts

Removed the following explicit legacy candidates and their metas after a fixed-point GUID/type-name audit of tracked project text:

- `Assets/_Project/Scripts/UI/EnemyIndicator.cs`
- `Assets/_Project/Scripts/Visual/BombNadirMarker.cs`
- `Assets/_Project/Scripts/Weapons/BombProjectile.cs`
- `Assets/_Project/Scripts/Weapons/BombRack.cs`
- `Assets/_Project/Scripts/Weapons/BombTrajectory.cs`
- `Assets/_Project/Scripts/Weapons/DirectNursRack.cs`
- `Assets/_Project/Scripts/Weapons/MissileLauncher.cs`
- `Assets/_Project/Scripts/Weapons/NursRocketRack.cs`

Retained candidates (surviving references):

- `Assets/_Project/Scripts/Camera/CameraLookAhead.cs` <- `Assets/_Project/Prefabs/Camera/CombatCameraRig.prefab`, `Assets/_Project/Scripts/Aircraft/CombatCameraRig.cs`, `Assets/_Project/Scripts/Weapons/WeaponController.cs`
- `Assets/_Project/Scripts/Camera/NursCameraCue.cs` <- `Assets/_Project/Scripts/Camera/ArcadeCameraFollow.cs`
- `Assets/_Project/Scripts/Camera/TopDownCameraFollow.cs` <- `Assets/_Project/Prefabs/Camera/CombatCameraRig.prefab`, `Assets/_Project/Scripts/Aircraft/CombatCameraRig.cs`, `Assets/_Project/Scripts/Camera/CameraLookAhead.cs`
- `Assets/_Project/Scripts/Enemy/EnemyFlightController.cs` <- `Assets/_Project/Prefabs/Aircraft/EnemyJet.prefab`, `Assets/_Project/Scripts/Weapons/WeaponLabEnemy.cs`, `Assets/_Project/Scripts/Weapons/WeaponLabEvasion.cs`
- `Assets/_Project/Scripts/Enemy/LockAfterburnerReaction.cs` <- `Assets/_Project/Scripts/Enemy/EnemyFlightController.cs`
- `Assets/_Project/Scripts/Flight/CabrageManeuver.cs` <- `Assets/_Project/Scripts/Camera/NursCameraCue.cs`, `Assets/_Project/Scripts/Flight/FlightController.cs`
- `Assets/_Project/Scripts/Targeting/BoundedAim.cs` <- `Assets/_Project/Prefabs/Aircraft/PlayerJet.prefab`, `Assets/_Project/Scripts/Weapons/WeaponController.cs`, `Assets/_Project/Scripts/Weapons/WeaponLabEvasion.cs`
- `Assets/_Project/Scripts/Targeting/ITargetLockEligibility.cs` <- `Assets/_Project/Scripts/Aircraft/CombatCameraRig.cs`, `Assets/_Project/Scripts/Aircraft/RadarAirContact.cs`, `Assets/_Project/Scripts/Targeting/TargetLock.cs`, `Assets/_Project/Scripts/Weapons/WeaponLabEnemy.cs`
- `Assets/_Project/Scripts/Targeting/TargetDesignation.cs` <- `Assets/_Project/Scripts/Weapons/ArcadeMissileLauncher.cs`
- `Assets/_Project/Scripts/Targeting/TargetLock.cs` <- `Assets/_Project/Prefabs/Camera/ArcadeCameraRig.prefab`, `Assets/_Project/Prefabs/Camera/CombatCameraRig.prefab`, `Assets/_Project/Scripts/Aircraft/CombatCameraRig.cs`, `Assets/_Project/Scripts/Camera/ArcadeCameraFollow.cs`, `Assets/_Project/Scripts/Enemy/LockAfterburnerReaction.cs`, `Assets/_Project/Scripts/UI/TargetLockHud.cs`, `Assets/_Project/Scripts/Weapons/GuidedMissileLauncher.cs`, `Assets/_Project/Scripts/Weapons/WeaponController.cs`
- `Assets/_Project/Scripts/Targeting/WeaponLock.cs` <- `Assets/_Project/Scripts/Enemy/EnemyMissileStressTest.cs`, `Assets/_Project/Scripts/Targeting/TargetDesignation.cs`, `Assets/_Project/Scripts/Weapons/ArcadeMissileLauncher.cs`, `Assets/_Project/Scripts/Weapons/MissileProjectile.cs`
- `Assets/_Project/Scripts/UI/TargetLockHud.cs` <- `Assets/_Project/Prefabs/Camera/ArcadeCameraRig.prefab`, `Assets/_Project/Prefabs/Camera/CombatCameraRig.prefab`
- `Assets/_Project/Scripts/Visual/BombImpactFx.cs` <- `Assets/_Project/Scripts/Weapons/NursProjectile.cs`
- `Assets/_Project/Scripts/Visual/NursImpactMarker.cs` <- `Assets/_Project/Scripts/Weapons/ArcadeNursLauncher.cs`
- `Assets/_Project/Scripts/Weapons/Autocannon.cs` <- `Assets/_Project/Prefabs/Aircraft/PlayerLynx_Combat.prefab`, `Assets/_Project/Scripts/Weapons/AutocannonEmitter.cs`, `Assets/_Project/Scripts/Weapons/AutocannonPreset.cs`, `Assets/_Project/Scripts/Weapons/AutocannonWeapon.cs`, `Assets/_Project/Scripts/Weapons/WeaponController.cs`
- `Assets/_Project/Scripts/Weapons/ContinuousCannon.cs` <- `Assets/_Project/Scripts/Weapons/AutocannonWeapon.cs`
- `Assets/_Project/Scripts/Weapons/GaussCannon.cs` <- `Assets/_Project/Scripts/Weapons/GaussEmitter.cs`, `Assets/_Project/Scripts/Weapons/WeaponController.cs`
- `Assets/_Project/Scripts/Weapons/GaussEmitter.cs` <- `Assets/_Project/Scripts/Weapons/GaussCannon.cs`, `Assets/_Project/Scripts/Weapons/WeaponController.cs`
- `Assets/_Project/Scripts/Weapons/GuidedMissileLauncher.cs` <- `Assets/_Project/Prefabs/Aircraft/PlayerJet.prefab`
- `Assets/_Project/Scripts/Weapons/WeaponController.cs` <- `Assets/_Project/Prefabs/Aircraft/PlayerJet.prefab`, `Assets/_Project/Scripts/Weapons/AutocannonEmitter.cs`
- `Assets/_Project/Scripts/Weapons/WeaponLabEnemy.cs` <- `Assets/_Project/Prefabs/Aircraft/EnemyJet.prefab`
- `Assets/_Project/Scripts/Weapons/WeaponLabEvasion.cs` <- `Assets/_Project/Prefabs/Aircraft/EnemyJet.prefab`, `Assets/_Project/Scripts/Enemy/ArcadeFighterPilot.cs`, `Assets/_Project/Scripts/Enemy/ArcadeFighterUnit.cs`, `Assets/_Project/Scripts/Enemy/EnemyFlightController.cs`, `Assets/_Project/Scripts/Enemy/Mission01ReconEncounter.cs`, `Assets/_Project/Scripts/Enemy/OverlordTarget.cs`, `Assets/_Project/Scripts/Flight/Mission01EngineAudio.cs`, `Assets/_Project/Scripts/Scenes/Mission01LaunchSequence.cs`, `Assets/_Project/Scripts/UI/Mission01StatusHud.cs`, `Assets/_Project/Scripts/Weapons/WeaponLabEnemy.cs`
- `Assets/_Project/Scripts/Weapons/WeaponSelection.cs` <- `Assets/_Project/Prefabs/Aircraft/PlayerJet.prefab`, `Assets/_Project/Scripts/Camera/NursCameraCue.cs`, `Assets/_Project/Scripts/Flight/CabrageManeuver.cs`, `Assets/_Project/Scripts/Weapons/AutocannonEmitter.cs`, `Assets/_Project/Scripts/Weapons/WeaponController.cs`

No scene, prefab, gameplay settings or surviving scripts were modified. Binary assets, reflection and dynamically constructed names are not proven safe by text search alone. Keep this list for the subsequent Unity asset dependency audit. Close Unity before applying; test both scenes and all sandbox modes.
