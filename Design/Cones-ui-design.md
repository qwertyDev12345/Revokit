# Revokit UI direction

Approved concept: `Cones-ui-concept.png`. Implemented with Unity uGUI, responsive safe-area containers, warm white cards, orange primary actions, and a green metric field.

The generated illustration is a decorative home asset. Buttons, text, routes, grid, markers, previews, and workout progress are live UI. Distances come from the drill model rather than the illustrative values in the concept.

## Generated asset

Tool: built-in image_gen. Runtime asset: `Assets/Cones/Resources/ConesHero.jpg`.

The opaque illustration uses JPEG quality 90 at its original 1536×1024 resolution: 108,079 bytes instead of approximately 1.28 MiB for the PNG (about 92% smaller). Unity limits the imported texture to 1024 pixels, disables mipmaps and the readable CPU copy, and uses ASTC 6×6 for iOS. The resource name and GUID are preserved. Use **Revokit → Optimize UI artwork** to reapply these import settings. Design references and preview screenshots live outside Assets and are not included in the player.

Compression review (2026-10-12): the JPEG was decoded and visually inspected; importer settings are saved in its `.meta` file. A fresh Unity import could not finish because the local licensing service did not initialize. The earlier player verification below predates this compression change.

Final generation prompt:

> Generate a production UI illustration asset for the approved CONES sports drill planner app: two vivid safety-orange plastic training cones, one standing upright on a square base on the right and one lying sideways on the left, realistic polished 3D product illustration, subtle soft contact shadows, clean warm-white background #FAFAF8, wide landscape 3:2 composition. Cones fill middle 80% width and 80% height. Soft studio lighting, orange plastic material with delicate highlights, physically plausible shapes, understated premium sports utility aesthetic. No text, no letters, no logos, no borders, no UI, no phone, no grass, no extra objects. This will sit in a white home card underneath a heading and above an orange button.

## Navigation

- Home: New drill, Continue editing, My drills, Session plan, Ready-made drills.
- Editor: field size, cone details, undo/redo, Cones/Route modes, setup, save, more tools.
- Cones mode: add, edit, delete. Route mode: play/pause, restart, remove last step.
- More tools includes setup, precise editing, route settings, variations, movement types, obstacles, training, reaction mode, coach mode, history, image/file export, and sessions.
- Workout: live phase progress, phase durations, start/pause/resume/repeat, reset, and session navigation.

All built-in copy remains English. Dynamic Island and home-indicator clearance use `Screen.safeArea`; no fake status bar or camera cutout is drawn in the app.

## Verification

The main workspace development build passed the model checks and completed successfully. The advanced-feature player run exited with code 0 after checking 30 screens at 375×667, 393×852, and 430×932, safe-area bounds, English copy, editing/undo, storage/recovery, sessions, timers, PNG export, variations, movements, obstacle validation, editable file transfer, coach timing, reaction completion and history notes/ratings. The result is recorded in `Logs/ConesAdvancedVerified/smoke-result.txt`. Screenshots from that verified player are in `Design/Previews`. Desktop insets simulate the phone safe area; native speech, Files import, sharing and physical iPhone validation are still required.
