# Third-party asset provenance

Preserve license files for **surviving assets** under `Assets/ThirdParty`. Cleanup B removes unreferenced old Kenney samples when safe; if a dependency remains in the project, it keeps its accompanying source/license. This page records provenance without claiming that all retired packages still ship.

| Source | Author / license | Original link | Usage |
| --- | --- | --- | --- |
| PolyPizza Missile | Poly by Google / CC BY | https://poly.pizza/m/dPVCvXP-S58 | Retained missile source only if referenced by active gameplay |
| Kenney Nature Kit | Kenney / CC0 | https://kenney.nl/assets/nature-kit | Any retained original models after dependency audit |
| Kenney Skyboxes | Kenney / CC0 | https://kenney.nl/assets/skyboxes | Any retained skybox dependency |
| Kenney City Kit Industrial | Kenney / CC0 | https://kenney.nl/assets/city-kit-industrial | Any retained city/industrial asset dependency |
| Kenney City Kit Suburban | Kenney / CC0 | https://kenney.nl/assets/city-kit-suburban | Any retained city asset dependency |

Original third-party files must not be redistributed without following their licenses. New B1 buildings will be independently modeled from references; photos are not meshes and must not be copied as assets without license permission. Removed historical sources remain in Git history.

## Mission01 engine audio — provisional provenance

- Cruise: smidoid, [Freesound 128614](https://freesound.org/people/smidoid/sounds/128614/) (`128614__smidoid__jetengine-loop.aiff`), live PCM WAV `Lynx_Engine_Cruise.wav`.
- Boost: qubodup, [Freesound 146770](https://freesound.org/people/qubodup/sounds/146770/) (`146770__qubodup__rocket-boost-engine-loop.wav`), live WAV `Lynx_Engine_Boost.wav`.

Original license terms and attribution requirements still need explicit verification before distribution. This provenance note does not assert redistribution rights. Raw sources remain outside Unity Assets.
