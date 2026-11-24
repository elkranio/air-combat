# Art direction — Stylized Arcade Military
> **Current visual addendum (2026-11-24):** The authored B1f arched hangar is present alongside the original B1e hangar. The new EF-01 Tiger D1 is a matte, F-5-inspired, full-size first-wave enemy fighter chassis in static visual comparison; no AI yet. Art is reviewed at the production camera (Y78), and playable arcade readability matters more than realism.

---


Working visual target, 2026-11-23. **Arcade, graphic and punchy**, not milsim photorealism and not cute low-poly/mobile-casual.

- **Ground:** quiet muted mid-light sand (`#B8A993` first accepted value), nearly flat fill, no texture noise or dark realism. Ground is the stage, not the star.
- **Large terrain:** broad geological masses and a 430×275×30 m plateau reference, bold clean edges and a few deliberate softened faceted cuts. No toy pebbles, spiky mountains or fine procedural noise. Y78 is the current chosen playable art height; Y150 remains a useful comparison, not a physics unit guarantee.
- **Buildings:** clear simple military/industrial volumes, slightly exaggerated readable doors/roofs, few large color zones and a small amount of functional roof detail. Not miniature architecture.
- **Aircraft:** contrasts against sand: dark blue-grey Kestrel paint, controlled orange accents. Current body is slightly short/square; a separate lengthened silhouette test may be warranted after B1 scale tests.
- **Shadows/light:** dynamic URP sun with real geometry, material roughness kept low. E1 shadow profile tests 600-unit range and four cascades; do not hide clipped shadows with blobs. Evaluate at pitch 50°, ortho 80, **altitude 78**, and while crossing edges; compare higher trial altitudes when relevant.
- **VFX/audio later:** punchy firing, readable tracers, hit sparks/debris, aircraft breakup and falling wreckage, smoke/explosions, powerful synced SFX. Weapons work mechanically today but are not presentation-finished.

**Approval gate:** silhouette, contrast, proportion and shadow read in Game view **while flying**. A beauty render or orthographic Scene view is not enough. Add only the detail that improves gameplay read.
