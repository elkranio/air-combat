# Mission 01 — aircraft shadow reception trial (WIP)

Base `25be8fb`. The PF02 Hull/Wing/Glass materials already allow receiving shadows. The shared rig uses realtime URP soft shadows with authored strength 0.75 and a flat ambient contribution. The aim is to make actual shadow transitions on Lynx legible near large cliffs, not fake distance-based shading or a collision indicator.

Open Mission01_FirstSortie, enter Play Mode, fly into the cast shadow on the shadowed side of a sufficiently tall cliff. In **Air Combat > Missions > Aircraft Shadow Trial**, compare:

- A: restore actual original sun strength, ambient multiplier and chassis MeshRenderer receiveShadows flags.
- B: enable reception on chassis meshes and raise sun strength to at least 0.95. Original ambient.
- C: same as B, with the original ambient multiplier reduced by 20% (all scene objects receive less ambient light).
- D: same lighting as B, but disable reception on chassis meshes for comparison. This is a diagnostic, not a candidate look. The effect of receiveShadows can depend on the active rendering path/material.

Compare B vs D in the same position and heading; A vs B for the visual candidate and C only if contrast remains weak. The aircraft is at Y78: a ground shadow does not guarantee the shadow reaches the aircraft. The test logs actual receiver flags and warns if material flags disable reception. No changes to materials, sun direction, shadow map budget, terrain or radar. A/B values are intentionally Play Mode only and reset on exit. No prefab/scene serialization edits. If a candidate works, commit persistent tuning separately after the visual test. Unity compilation and actual shadow reception still need local verification.
