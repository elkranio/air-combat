# B1 — first military base (as-built composition and deferred work)

**Status at `645081ba`:** the **main scene is already assembled** through B1e and includes a separate B1f **arched hangar style trial**. This is not an instruction to reinstall B1a–B1f. Current gameplay priority is player fixed-forward autocannon → one Tiger dogfight → multiple enemies; base additions are **on hold**. See [handoff](../../HANDOFF.md).

## Existing authored objects

- **Terrain/map:** 2×2 km persistent `ArcadeDesertE1.unity`, muted sand `#B8A993`, one accepted ~430×275×30 m plateau. Y78 main flight altitude; fixed-world oblique camera. **Never regenerate the map**.
- **B1b barracks:** three scene instances of `Barracks_B1a.prefab` (name/GUID retained after B1b style pass), each roughly 32×12 m. Enlarged gable eaves, entry canopy, two vents, flat cool-grey/blue materials, dynamic shadows. Live source: `Tools/Blender/generate_barracks_b1a.py`.
- **B1c headquarters:** two-height ~29×19 m HQ near barracks, independent entry, ~38×22 m paved courtyard, two planters and flagpole. Live source: `Tools/Blender/generate_hq_b1c.py`. Do not confuse the HQ's own plaza with prepared dirt.
- **B1d roads:** accepted asphalt access route, barracks lane and HQ spur, with editable shoulders and sparse wear marks. Separate prefabs/objects; don't rebuild from old installer. The original B1d/B1d2 painted Terrain compacted-soil experiment is superseded by B1d3.
- **B1d3 ground:** **one continuous irregular hard-edged prepared-lot mesh** ~183×226 m, matte warm earth `#A38B72`. Toggle scene root `B1 / Prepared lot - DISABLE to compare` to see unpainted sand while retaining roads and buildings. Its surface is visual only; original TerrainCollider owns collision. Original B1d TerrainLayer assets remain as historical dependencies, not the active painted covering.
- **B1e airfield:** runway/apron, original open-front hangar, control tower and two static Kestrels in a *separate* western aviation sector. **B1f arched hangar** sits near the B1e hangar for A/B and is a separate toggleable style trial. See [airfield as-built](AIRFIELD_B1E.md).
- **D1 Tiger:** toggleable static **EF-01 Tiger D1** test chassis between parked Kestrels. **Not** an enemy yet; see [Tiger D1](https://github.com/elkranio/air-combat/blob/8bfbbb7698879b4809812894a5c7ebaba6c623fd/Docs/Archive/TIGER_D1.md).

## Design rules and optional later improvements

The base should read as a compact **used military outpost**, not a city, toy diorama or low-poly clutter field. Prioritize big functional shapes, large color areas, clean aircraft/target visibility and convincing dynamic shadows from gameplay height Y78. No painted fake sun, dense procedural debris or gratuitous AO.

Deferred, **not current priorities**: intentional curved service roads between HQ/barracks and runway/apron (not right-angle tiled joins), subtle building variation, a few utility/perimeter markers, and eventually AA1/SAM1/depot props wired into a real mission. Reuse existing gameplay prefabs; don't remodel the whole base to integrate combat. Previous B1a–B1d install instructions are retained in the corresponding atom docs as **history only**, not a next-step plan.

When future art work resumes, author one reusable model (editable `.blend` or documented generator → imported mesh → matte URP materials → prefab/meta), inspect it in moving Game view at yaw -45°, pitch 50°, ortho 80, Y78; then accept or revert that one model.
