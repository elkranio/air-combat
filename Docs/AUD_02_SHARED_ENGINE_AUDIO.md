# AUD02 — Shared player engine audio and M02 cargo timing

The existing `Mission01EngineAudio` script and GUID remain for scene and authoring compatibility. Its mixer now reads `IPlayerEngineAudioContext`, implemented by both mission encounters. M01 preserves its previous state mapping, serialized flight and audio settings. M02 resolves its player from the already-bound route trial at Awake (FlightSession executes earlier). Its scene component uses the existing Lynx cruise/boost clips and the original default mixer settings. No new audio assets or changes to thrust, fuel, weapons or SAMs.

Ready: idle loop; Takeoff: boost; Flight: cruise or actual active boost; Landing: reduced engine (M01); failure/intro/landed and stationary CA06 range: silent. Tactical map retains ownership of audio pause. R reuses the same two sources, not a new pair.

## M02 V07B full-sortie timing pass

The saved M02 scene and component defaults now use C1 dispatch 49 s, C2 dispatch 124 s, 60 units/s flight speed, 60 s reserve dispatch delay after an unloaded carrier kill. Reserve delay was already 60 s in the saved scene. C1/C2 test dispatch remains 2 s; the saved Full Sortie, runway start and disabled base/range overrides are unchanged.

Approximate arrival above pad: C1 ~63 s, C2 ~140–141 s; approximate loaded exit at the off-map path endpoint: C1 ~90–91 s, C2 ~170–171 s. These are estimates from the V07B route lengths, braking/acceleration and pickup phase durations, not measured gameplay deadlines. The carrier crosses the operation boundary earlier than the off-map endpoint. Timing is measured from the existing sortie clock (W takeoff or airborne start); reserve timing is relative to kill, not the main dispatch. Tune after full sortie playtest.

## Local acceptance

- M02: idle on runway, W takeoff, cruise, boost/release, fuel depletion, map pause/resume, failure and R. Ensure exactly one pair of audio loops and no missing clip/flight warnings. Verify the stationary CA06 range stays silent and returning to normal M02 restores audio.
- M01: repeat audio regression, including landing, failure and retry. The sandbox is not changed.
- Full Sortie: log C1 and C2 arrival, capture, loaded departure and escape; confirm reserve after 60 s and no change to convoy, map or mission geometry. Unity compilation and Play Mode acceptance remain local.
