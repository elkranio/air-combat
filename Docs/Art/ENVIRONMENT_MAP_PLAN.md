# Environment map direction — approved plan (2026-11-23)

**Status:** C3 fixed-world 50° + forward lead **green**; existing persistent 2×2 km E1 map remains the active work area. E1b approved a **muted matte flat ground** but rejected its miniature-feeling rock chunks, pale jet paint and scale/shadow balance. **E1c scale reset in progress:** one huge low plateau, contrast jet and Y78/110/150 camera comparison. [E1c test](ARCADE_DESERT_E1C.md). No scene regeneration. Older studies: V17/V19, V2A–V2A5, RockKitR1, BrokenVector, Unity Terrain T1.

## Target

Build **one cohesive, expandable, low-altitude arcade air-combat map**, beginning with a representative region rather than an isolated disposable showcase. The desired image is **readable, atmospheric and grounded but not photorealistic or toy-like**. Avoid the old-game split where crisp buildings/rocks sit on blurry ground. Art must work at actual flight height/speed, from the gameplay camera, not only an editor overview.

- A mostly open, navigable field with **broad hills, low areas and deliberate elevation variation**.
- **One fly-through canyon:** visible entrance, traversable corridor, visible exit. A turn into either wall must be dangerous and eventually cause a real crash. Walls must rise high enough relative to the aircraft's flight altitude (current art studies use ~Y48). A nearly straight canyon is fine; interesting flight beats matter more than geological complexity.
- Roads, a small industrial/military complex, other structures, some ruined/damaged elements, rocks and environmental props. The mix makes the world feel inhabited; avoid filling every empty patch.
- A single art language across terrain, cliff faces, rocks, road, buildings and aircraft: comparable **screen-space sharpness, texture frequency, material response, contrast and scale**. No noticeable stretched/blurred vertical slope textures, discontinuous joins, noisy procedural mountains or bright toy-plastic surfaces.
- Use **the accepted shared combat lighting rig** (`CombatLightingRig.prefab`) and real URP shadows when building the new map. Later test restrained color grading, atmosphere and shader/texture improvements; post-processing must not be used to hide weak geometry or bad source maps.

## Design/production approach

1. **Use V19 / `FrontierPracticalBake.unity` as the composition/reference benchmark**, especially the more convincing road, structures and crisp rocks shown in the 2026-11-23 screenshots. Preserve the older test scene intact. This is a *visual reference*, not blanket acceptance of its materials, terrain or old lighting.
2. **Hybrid environment:** terrain or a simpler ground mesh for open earth and broad smooth elevation; **authored meshes** for canyon walls, sharp cliffs, strong silhouettes and meaningful obstacles. Neither an all-Terrain solution nor a universal all-mesh solution is mandatory. Pick whichever method is most convincing for each form.
3. **Create reference-driven assets** where needed, especially geology. Use 2–3 geological or strong stylized references per important rock/structure; block out silhouette and scale before normals/AO/Blender bakes. Keep Blender sources editable; import only necessary game geometry. Do not repeat blind procedural mountain generators.
4. **No unification by blurring the sharp assets.** Improve ground texel density, tiling, transitions, UV mapping and slope treatment first; bring mesh and terrain shading/contrast into the same range. Check both up close for seams and from the moving gameplay view for clarity.
5. **Real collisions, not camera tricks.** Flight over open ground stays permissive, canyon walls are actual obstacles; prototype collision suited to the art-scene Transform-driven flight first, then integrate safely with combat flight without modifying core prefabs prematurely.
6. Build and **extend one map in stages**, preserving the best accepted section as the foundation for the next; keep active experiments isolated from `WeaponLab`, `GroundAttackLab`, `Sandbox`, V17/V19 and the T1 study.

## Stages (one reviewed atom at a time)

**C1 — camera feel (visual green, combat usability problematic):** isolated copy of V19. Test a strong three-quarter *heading-locked* view, where the aircraft appears approximately upper-right on screen while turning rotates the camera/world. Compare against old north-up framing with a hotkey, same art and zoom. Keep or reject quickly, with a single Play Mode verdict. **C1b is a separate combat-validation atom, not a relighting/environment rebuild.** See below.

**C2/C3 — accepted gameplay camera:** fixed-world yaw **-45°**, pitch **50°**, ortho 80, smoothed heading-based cruise lead **48** with **+14** on boost. Do not restore auto-rotating world just for visual depth.

**E1a — archived decision:** preferred direction is flat-color ground, not noisy texture. The earlier `T` Original / Flat / Macro / Fine preview remains in C1b for diagnosis. See [original study](GROUND_STYLE_E1A.md).

**E1 — active persistent foundation / E1c gate:** continue the **existing 2×2 km editable native Terrain** with the approved muted `#B8A993` flat ground, real shared lighting, fixed C3 camera. Retire tiny-object scale tests; compare **one broad 430×275m / ~30m plateau**, isolated dark aircraft paint, and Y78/110/150 flight. Choose a coherent scene scale **before** roads, industry or a traversable canyon. [E1c details](ARCADE_DESERT_E1C.md), [style](ARCADE_VISUAL_DIRECTION.md).

**E2 — aircraft art + ground coherence:** first compare paint variants on the **current jet**, then mild stylized silhouette options at the same gameplay camera. In parallel, if E1 passes, tune ground/rock palettes, transitions, material response and crispness. Only then sculpt larger hills/lowlands and extend the authored environment. No mandatory texture noise: the flat ground may remain intentional.

**E3 — fly-through canyon:** shape one entrance, near-straight flight corridor and exit; authored steep walls, high enough to intercept a low-flying jet, blended naturally into open ground. Test wall clearance, real collision/crash and readable anticipation of the entrance. Exact length/width are **playtest tuning**, not locked values.

**E4 — inhabited world details:** roads/paths, a small military/industrial group, damaged structures and a restrained mix of prop sizes/rock clusters. Model or source licensed/reference-informed props as needed; maintain one art language and clear flight space. Do not generate a large asset catalog in advance.

**E5 — final image pass and map expansion:** compare shared-light presets, shadow quality, tasteful atmospheric treatment/color grading and only necessary shader adjustments. Extend accepted regions into one larger map, reuse the same materials/kit and validate performance/readability while moving. No global postprocess preset before base artwork passes.

## Visual/playtest approval

- At gameplay ortho size (~80 initially), terrain and objects **do not look like different resolution generations**. Clear edge/rock/building hierarchy; no obvious texture blur, stretching or harsh stitching, including canyon slopes.
- Hills and lowlands read while flying, but the ground does not consume the entire screen or hide forward threats. Flight path is largely open, with deliberate navigable choke points.
- Canyon is **flyable through from entrance to exit**, and sideways wall hits demonstrably crash/reset (not merely implied by a collider component). High walls reach the player's flight band.
- Lighting/shadows are produced by the shared modern URP rig, with no duplicated scene sun. No aggressive bloom, saturation, fog or sharpening.
- Compare equivalent views before/after and test while turning/boosting. Write `accepted / rejected / unresolved`, camera angle, flight height/zoom and any new asset sizes into `HANDOFF.md` after actual Unity approval.

## Camera C1 — precise experiment contract

**Question:** does a much more oblique, approximately isometric *aircraft-relative* camera make low flight and obstacles feel better, or simply make steering/aiming unreadable?

- **Not** merely pitching the old fixed north-up camera. Aircraft movement/heading remain unchanged in world X/Z, but the **camera yaw follows aircraft heading minus about 45 degrees**, so the jet points upper-right in screen space and the **map rotates when steering**. The jet still banks visually as before. This is a presentation test, not new flight physics.
- Orthographic projection at **size 80** for comparable scale. Three pitches **50° / 58° / 65°** via `1/2/3` (**50° is the accepted starting preference**). A modest forward camera focus gives room to see ahead. Smooth yaw/pitch/focus enough to avoid judder; avoid severe lag.
- Press **C** to toggle *heading-locked 3/4* vs *original-style north-up 65°* A/B while flying. `A/D` turn, `W/S` speed, `Space` boost, `R` reset, `H` legend in the **V19 art study**, not combat controls. Don't confuse the art-study control scheme with WeaponLab/GroundAttackLab (`W` boost).
- New study scene `Assets/_Project/Scenes/FrontierCameraC1.unity` is made **once**, by editor menu `Air Combat > Art > C1 - Create Heading-Locked Camera Study`, copying `FrontierPracticalBake.unity`. **Do not rerun** if it exists. Existing camera script is disabled **in the copy only**. Keep V19's old light/materials unchanged to isolate the camera question; E1 is where new shared light arrives.
- Test 30–60 seconds of straight flight, sharp A/D turns, boost, around the refinery/rocks and near map limits. Watch camera rotation, perceived plane heading, forward visibility, sense of speed, shadows/occlusion and ability to choose a direction intuitively.
- Verdict immediately: **keep / reject / revise only if one small obvious tweak could decide it**. If rejected, return to the familiar camera for E1 without detouring into a new camera subsystem. If accepted, adapt the design to combat HUD/targeting only *after* C1, with separate implementation and regression tests.

**Observed result (2026-11-23):** First C1 Play Mode test **visually promising**: rotating 3/4 makes the older map feel more volumetric, with **50° preferred**. C1b full combat then revealed reduced awareness/forward anticipation, harder combat and **mild but noticeable motion sickness** from world rotation. Aesthetic success is not permission to sacrifice comfort. See [C1b/C2 combat camera](CAMERA_C1_COMBAT.md).

## C1b — full combat feel with the 50° camera

Source: the **committed C1 camera study**, not a regenerated V19 and not a mutation of the combat labs. Once via `Air Combat > Art > C1b - Create 3-4 Camera Combat Study` in Unity Edit Mode; it creates `Assets/_Project/Scenes/FrontierCameraCombatC1.unity`. Read [CAMERA_C1_COMBAT.md](CAMERA_C1_COMBAT.md).

Use unchanged, authored prefab gameplay from GroundAttackLab: 1 BURST, 2 GAUSS, 3 A2A locked missile, 4 four bombs, 5 cabrage NURS, 6 direct NURS; AA1 autocannon, SAM1 vertical missile, four damageable depot assemblies, 20 HP, fuel bar and radar. Add the established WeaponLab fighter **only to exercise real air-target lock**, not as a new enemy system. C1b creates a terrain mesh collider because V19 had none: otherwise ground weapons fall through the scenery. Camera tilts `J/K/L` = `50/58/65` in C1b (the `1–6` keys must stay reserved for weapons); **50 starts selected**, `C` compares heading-locked vs old north-up. Preserve old V19 lighting **for the camera/combat comparison**. The shared modern light belongs in E1, not this isolation test.

Originally, C1b's **heading-up** radar fixed the aircraft marker upward; this was wrong because the jet actually appears **upper-right**. **C2 corrects the radar to camera-relative**: aircraft marker, site positions and threat rings all follow the camera's actual yaw axes. Fixed yaw means the marker turns and world orientation is stable; heading-locked means the marker stays upper-right and the world rotates. Ordinary combat labs without C1 remain north-up.

**C1b combat verdict:** visuals improved, combat readability worsened, mild motion sickness. Do not promote heading-locked camera to default.

**C2 — fixed yaw 50° (current):** In the *existing committed* `FrontierCameraCombatC1.unity`, the default camera stays at **yaw -45° / pitch 50° / ortho 80** as the aircraft steers. The jet initially points upper-right and rotates naturally relative to the stable ground. Press **C** to compare against the old heading-locked camera at the **same 50°** (no simultaneous pitch change); `J/K/L` remain 50/58/65 for optional checks, `1–6` remain weapons. Radar is camera-relative. Everything else — six weapons, fighter, AA1, SAM1, HP, source art and lighting — stays the same. **No scene regeneration and no new hybrid camera mode.** Watch motion sickness, heading cues, cursor aim, threat anticipation and target framing. If C2 passes, use it as the camera baseline for E1; otherwise prefer a comfortable fixed camera and solve scenic impact via map design.
