# CLEANUP-01 — accepted baseline consolidation

Base `7881719ca7be6d53a485ad009c681c167c7ec2d4`. No intended gameplay tuning. AA-01e2 is the accepted mobile AA variant. Retired modes are moved out of Unity, not silently substituted with the old stationary lab AA. Existing Mantis script GUID, scene transforms, route, weapon values, and runtime canister visuals stay intact. Remove obsolete serialized fields from the one scene Mantis; explicitly preserve the active cold-launch defaults and the existing mount references.

## Editor

Thirteen one-time editor installers/palette/refinement scripts retired outside Assets, including two InitializeOnLoad installers. Remove the Mantis trial installer command from the reusable patrol editor; leave route creation and Inspector utilities. No new automatic scene editing. The original installer sources are archived for recovery only; do not restore them into Assets without checking compatibility.

## Resources

Moved the Unity template welcome Readme, TutorialInfo assets and the empty Kenney folder metadata outside Assets, together with their metadata. The template Readme editor itself offers a Remove Readme Assets command. Checked the candidate template and retired projectile GUIDs against the 37 committed scene/prefab files (including art prefabs), plus main project/build/graphics settings for the template Readme references. No scene/prefab references found. Old Mantis gun meshes/materials remain in use by the temporary visual prefab and must not be deleted before launcher art replacement. Runtime Resource/Shader lookups and lab assets stay protected. The broader art inventory is not automatically pruned: use the dependency report in Unity and verify candidates before deleting.

## Verification

Git patch context and paths checked against the pinned source; editor compilation and Play Mode require the local Unity project. Check both ground and air loadouts, both labs, asset imports, HUD/debug, patrol authoring, reset and the absence of obsolete menus. No regeneration or prefabization in this atom. See [HANDOFF](../HANDOFF.md).
