# AUD-01 — Lynx engine loop trial

Open Mission01_FirstSortie in Edit Mode. Run **Air Combat > Missions > Install Mission01 Engine Audio v0**, then Play. Save the scene and generated live audio assets/metas after acceptance. Re-running the installer reuses the component, preserves mix settings and leaves existing live copies intact.

Raw source files remain under Resources/Audio outside Unity Assets. Installer converts `128614__smidoid__jetengine-loop.aiff` to `Assets/_Project/Art/Aircraft/Audio/Lynx_Engine_Cruise.wav` and copies `146770__qubodup__rocket-boost-engine-loop.wav` to `Lynx_Engine_Boost.wav`. Cruise is decoded to PCM16 WAV at the original sample rate and channel count (no resampling or normalization). Both use PCM / Decompress On Load; raw files remain untouched. Provenance: https://freesound.org/people/smidoid/sounds/128614/ and https://freesound.org/people/qubodup/sounds/146770/. Confirm original license and attribution requirements before distribution (including repository distribution); this atom does not establish those rights.

Two independent looping 2D AudioSources, created at runtime. Cruise stays audible during flight; boost roar fades in/out over .22/.35 seconds using actual Afterburner.IsActive, not input. S changes pitch only through current speed. Idle and landing are quieter; scripted takeoff uses both layers. Intro, crash, boundary failure, landed and disable stop audio. Existing Tab listener pause is respected; retry starts the appropriate mix. No changes to weapons, camera, flight tuning or other scenes.

Inspector on mission launch object: idle .16, cruise .38, boost .55, pitch 1 each. These are initial mix guesses; tune by ear against cannon, missile warning and explosion. Test loop seams, W release/fuel exhaustion, S, Tab, R, takeoff, landing and crash. Original clips are not edited or auditioned by this patch. Unity runtime and subjective sound check required.

## AUD-01A — AIFF import workaround

Unity FSBTool rejected the generated cruise AIFF. The installer now decodes uncompressed AIFF/AIFC PCM (8/16/24/32-bit, big or little endian) or AIFC float32/float64 to standard PCM16 WAV without external tools. On successful cruise import, remove the obsolete generated AIFF asset. Repeat the same menu command; no scene reset or mix changes. If the file uses an unsupported codec the installer reports it explicitly; convert externally in that case. Unity import validation remains required.
