# REF-01E1 — mission objective presentation

Base `5a93717fb90ad764a3d60d058e3137c30af51979` (D green).

Move the two objective OnGUI callbacks into `Mission01ObjectiveHud`, attached once at mission Start. The view reads the existing primary progress and Overlord state, retains flight/ready/active/map gates, and uses exactly the existing label coordinates, font sizes, text, and colors. The accepted scene has one Overlord; resolve it once in the same scene, with no scene or prefab edits. If no optional target is present, omit the label; if several are present, warn and omit the ambiguous label. The binding is Mission01-specific, not a general mission UI framework. Overlord exposes its existing flight-active query to the view. Existing objective status property retained for compatibility. No changes to objective completion, movement, damage, FX, radar, retry, or landing. Mission phase/boundary/landing presentation and shared FX remain for subsequent atoms.

## Local regression gate

- Compile and enter Play Mode: no new errors; existing CS0618 warnings deferred to CLEAN-04.
- Check existing RECON and SECONDARY / OVERLORD labels for positions, size, color and text. Confirm no duplicates.
- Destroy Watchers; verify progress and green 3/3 text. Overlord charging/escaping/escaped/destroyed labels remain distinct.
- Tab hides labels; closing restores them. R resets both; takeoff, crash, boundary failure, landing hide them as before.
- Tactical map and RTB completion unchanged. No scene save required.
- Unity compile and Play Mode are acceptance gates.
