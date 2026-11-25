# M01-AA2 — Picket combat (WIP)

Base `d942947`. In Edit Mode open `Mission01_FirstSortie`, run **Air Combat > Missions > Install Tactical Map**, save the scene and generated `Assets/_Project/Prefabs/Enemies/SAM01_PicketMissile.prefab` with its meta. The installer preserves existing launcher radius and already configured missile profile on subsequent runs. The temporary top launch socket is calculated from the visual bounds; no animation of the decorative canisters yet.

Each Picket retains the authored coverage radius. AA3 tuning uses 2 s acquisition (including a minimum .65 s warning lead), 5 s minimum launch-to-launch interval, and no cap on its concurrent flying missiles. Acquisition can overlap the last part of reload; a full lock, warning and reload must all complete before launch. Acquisition outside the coverage is silent and cancels on exit. Three short pre-launch beeps and the existing bearing cue start together. Leaving coverage after launch does not remove the projectile. The Picket-specific prefab exposes missile tuning (125 speed, 100 deg/s turn, 140 deg total homing turn budget, 6.5 s lifetime, 3.5 hit radius, 4 HP). Vertical/pitch-over and terrain collision reuse SAM1. Lab missile prefab and single-shot gating are unchanged. R disables and destroys every live missile owned by each launcher.

Radar displays the actual horizontal missile heading, or a dot during vertical ejection. Legacy contacts retain their original glyph behavior. The map continues to show site coverage; recon and optional objectives remain placeholders. Tab freezes projectile, acquisition and cue timing. Aircraft loss uses the existing retry flow; R cancels all owned flying missiles and resets sites. No global launch cap, map edge enforcement, ground attack or visual destruction polish yet.

## Local checks

- No acquisition on the runway or during auto takeoff (disable debug air start to test).
- Approach a zone, leave during acquisition or the warning, then enter again. Verify that the warning cancels and a new one starts correctly.
- Stay in coverage: three beeps, directional cue, visible launch, missile track and actual course on radar.
- Test Tab during acquisition, warning and flight; R during flight; lethal hit and retry.
- Check two overlapping zones and a terrain-blocked missile. Test at normal flight speed, not debug fast travel. Tune in AA3.
