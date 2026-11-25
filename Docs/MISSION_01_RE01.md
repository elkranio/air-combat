# M01-RE01 — Watcher recon encounter v1 (UAV trial)

After applying the patch let Unity import the updated OBJs. Reinstall the three Watchers to update their hit zones and add the rotor. Existing OBJ names, paint groups, prefab and scene marker references are retained; the new Rotor OBJ receives its meta on import.

Mission premise: Dominion utility UAVs have lost contact with command and are waiting in autonomous holding patterns. They are passive in First Sortie; future armed variants are outside this atom.

Open `Mission01_FirstSortie`, run **Air Combat > Missions > Install Mission01 Watchers v0**, save the scene. Uses the three existing authored recon marker transforms (the tactical map keeps its references). Installer is repeatable and preserves tuned HP. No changes to the SAMs, terrain, aircraft or weapons.

- Fictional Dominion RE-01 Watcher: 17.94 x 25 units, slate/olive with gold wing stripes, long wings, narrow fuselage, sensor turret, inverted V tail and independently animated pusher rotor. Five existing paint groups plus a rotor mesh using Dark. Three arcade hit zones (fuselage, wings, tail) share the root target; no full-span hull box. Python generator also supports Blender `.blend` export.
- Three independent circular patrols centered on the map's existing recon positions. Radius 42/52/46; speed 23% of player cruise. Same altitude as the mission cruise, passive, 10 HP (AC01 does 1 per round, 4 per burst). Uses normal physical AC collisions and existing radar contact registry.
- Damage smoke from 40% damage; lethal hit disables hitbox/radar/map marker, counts objective immediately. Burning glide, two air bursts, pitched/rolled descent and ground explosion. Existing combat FX used for explosions; separate bounded world-space smoke/fire particles. No weapons or damage from falling wreck.
- HUD recon counter, tactical map live positions and kill count. R resets all three targets, clears encounter effects and old player rounds. Tab freezes motion and particle simulation with the rest of the mission.
- RTB/landing, optional target and full mission result are deliberately outside this pass. No custom audio, markings textures or wing separation yet; the existing burning descent is retained.

## Test
1. Check three visible targets in the north zone, dark silhouettes against sand, radar and moving map markers.
2. Hit with AC01; roughly 2.5 fully connected bursts per kill; smoke after four hits, then falling wreck. Check readability/scale and VFX.
3. Kill all three; verify 3/3, no remaining target markers. R should restore all targets and clear effects; Tab should pause even during a fall.
4. Save authored scene after installation. No manual tactical-map reinstall required.
