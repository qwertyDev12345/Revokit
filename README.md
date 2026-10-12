# Revokit — Unity mobile prototype

An English-language drill planner for iPhone. Unity **6000.3.20f1**, standard uGUI, local storage, no account or external services.

## Run

Open `Assets/Scenes/Cones.unity` and press Play. Select a portrait Game View resolution such as 393 × 852 or 375 × 667. The app starts at the main menu.

## Features

The approved visual redesign uses warm white cards, orange primary actions, green drill fields, a generated cone illustration on Home, and a live circular workout timer. The editor separates Cones and Route actions to keep the field larger. Tap the field-size chip to enter dimensions, or More tools for the remaining features. All built-in text is English.

The illustration and generation prompt are documented in `Design/Cones-ui-design.md`; the approved reference is `Design/Cones-ui-concept.png`. Artwork is decorative; controls, diagrams, previews and progress remain interactive Unity UI.

- Main menu: continue editing, create a blank drill, choose a template, or open My drills.
- Four adjustable templates: Zigzag, Shuttle runs, Square loop, and T route. Choose your cone count and field dimensions before generating the layout.
- Field sizes from 2 × 2 to 40 × 40 metres. Resizing scales cone positions proportionally.
- Up to 24 cones. Add, select, remove, or drag with 0.25 m snapping.
- Routes with up to 100 steps, distance labels, direction arrows, and animated playback.
- Route settings: speed from 0.5 to 6 m/s, 1–20 rounds, closed-loop toggle, reverse direction, step reordering and removal.
- Closed loops include the final return segment in distance and playback. Open routes restart at the first cone each round.
- Undo/redo for the last 50 edits, including cone dragging, field resizing, route changes, and preview settings.
- Named local drill library with paging, rename-on-save, Save a copy, duplicate, and confirmed deletion.
- Confirmation before replacing an edited drill. Navigating to menus keeps the current draft in memory.
- Import of the previous prototype's single-slot save, when present. The original file is retained.

## Setup, training, and sessions

- **Drill tools → Setup mode:** enlarged diagram, selectable cones, distance from each field edge, segment distances, and placement checkmarks. Checkmarks stay during the current app run and reset when the drill changes.
- **Cone details:** exact X/Y positions in metres, a label up to 16 characters, and orange/blue/green/red cone colors. These are saved with the drill and included in undo/redo.
- **Export image:** cropped PNG containing only the diagram artwork, title, dimensions, cone labels, and route distances. On Windows, open the local export folder. On iPhone, use the native share sheet to send or save the image; sharing does not happen automatically.
- **Training mode:** preparation/work/rest phases, sets, pause/resume/reset, optional generated sound cues, and a large countdown. The display stays awake during an active timer. Backgrounding pauses the timer; background audio or lock-screen timing is not promised.
- **Session plan:** one named, persistent session containing up to 20 drill snapshots. Add saved drills or the current draft, reorder/remove entries, and switch between drills during training. Library changes do not silently change the session's snapshots. Session edits save immediately.
- **Draft recovery:** the current editing draft saves after a short debounce and on background/quit. Startup restores it independently of named library saves. A valid backup is used if the primary file is unreadable; unreadable primary files are preserved before replacement.

Use **Save** to update a named library drill. Draft autosave does not overwrite library entries. Abrupt termination can lose edits made within the short debounce window. Playback settings and timer durations are user controls, not exercise recommendations.

## Advanced drill tools

- **Reaction mode:** random cone numbers or colors, 2–30 seconds per command, 1–100 commands per round, and 1–20 rounds. Optional immediate repeats and English voice commands. iPhone uses native speech; the desktop preview uses a tone. Leaving the screen or backgrounding pauses the drill. Completed rounds are stored as one activity.
- **Variations:** mirror horizontally or vertically, reverse direction, or rotate the starting route step. Changes support undo/redo. Rotating an open route changes its connections.
- **Share drill:** export an editable `.cones` file, including movements, zones and training settings. On the receiving iPhone, open Revokit → More tools → Share drill → Import from Files. Review the preview, then open it or save a separate library copy. Import validates the contents and accepts files up to 256 KB. Desktop preview uses a local file path.
- **Coach mode:** up to 20 unique participant names, manual stopwatch, pause/resume, and Stop & save. Leaving the screen pauses the attempt. Saved results retain the participant name even after removing that participant.
- **Training history:** the latest 200 completed workouts, reaction drills and coach attempts, stored locally with backups. Add notes up to 280 characters and a subjective difficulty rating from 1 to 5. Delete individual activities with confirmation.
- **Movement types:** Run, Side step, Backpedal and Dribble on individual route segments. Labels appear on the diagram and are preserved in saves, exports and route reversal.
- **Obstacles:** up to 20 rectangular walls, goals or no-go zones, defined in metres. Route segments crossing marked zones appear red; the editor counts crossing segments and cones inside zones. These are geometric warnings, not automatic rerouting.

## Safe area

All screens share `SafeAreaPanel`, which maps `Screen.safeArea` to normalised anchors and updates when the screen changes. Background fills the display; interactive content stays within the safe area with additional margins. Portrait orientation is enforced. Library and step lists use paging to fit small displays.

The camera cutout, Dynamic Island, and bottom home indicator are handled through the system safe area. Desktop tests simulate phone insets; physical iPhone validation remains necessary.

## Code

- `DrillPlan`: serialisable data, templates, validation, metric distance, and movement along the route.
- `DrillLibrary`: named JSON files with validated IDs, atomic replacement, backups, and corrupt-file handling.
- `FieldBoard`: grid, cones, route graphics, and touch/mouse input.
- `ConesApp`: editor UI and playback.
- `ConesApp.Navigation`: screens, library interactions, settings, and undo/redo.
- `ConesApp.Design`: shared card styling, home layout, editor layout, field-size inputs and navigation icons.
- `ConesVisuals`: rounded UI sprite, vector icons, and circular workout progress.
- `ConesApp.Workspace`: draft recovery, setup mode, exact cone editing, and adaptive-template inputs.
- `ConesApp.Training`: workout UI, sound cues, session storage and navigation.
- `ConesApp.Export`: PNG capture and the platform share entry point.
- `ConesApp.RouteTools`: variations, per-segment movements and obstacle editing.
- `ConesApp.Reaction`: reaction settings, commands, pause and native voice entry points.
- `ConesApp.Activity`: participant roster, manual timing and local training history.
- `ConesApp.Sharing`: validated editable file export/import and preview.
- `AdvancedModels` and `DrillPlan.Advanced`: reaction state, activity validation, route transformations and intersection checks.
- `WorkoutTimer`: deterministic timer state machine, independent of UI.
- `LocalWorkspace`: draft/session models, validation, atomic writes and backups.
- `Plugins/iOS/ConesShare.mm`: UIKit sharing, Files import and English speech. `ConesIOSExport` configures the generated iOS plist and AVFoundation framework.
- `SafeAreaPanel`: screen inset handling.
- `ConesProjectSetup`: scene creation, model checks, and development build.
- `ConesSmokeCheck`: development-only UI and interaction checks, explicitly enabled by a command-line flag.

Built-in text and documentation are in English. User-entered drill names are displayed as entered.

## Validation

`ConesProjectSetup.ValidateAndBuild` checks model behaviour and creates `Builds/RevokitPrototype/Revokit.exe`.

Run the development player with `-screen-width 400 -screen-height 800 -conesSmokeTest -conesTestOutput <absolute-directory>` to check 30 screens at three portrait sizes, safe-area bounds, English UI, editing/undo, library operations, setup checkmarks, metadata, draft/backup recovery, session navigation, workout timers and PNG export. Advanced checks cover route variations, movements, obstacles, editable file transfer, coach attempts, reaction completion and persistent history notes/ratings. Test data is isolated in a unique subdirectory. Screenshots, a sample exported PNG and `smoke-result.txt` are written to the specified output folder. Model checks additionally cover template counts, timer boundaries, geometry intersections and backward compatibility.

The unused desktop SSAO renderer feature is disabled because it produced missing-resource errors in the player and is unnecessary for this UI app.

Earlier Windows development runs intermittently exited with `0xC0000005` at `PlayerConnection::Cleanup`. The latest clean exit does not establish that the earlier intermittent shutdown issue is permanently resolved.

Final iPhone building/signing requires macOS, Xcode, and signing configuration. No physical iPhone run or signed iOS build has been performed. Native speech, Files import, sharing and the photo permission path must be validated on iPhone. AR is not included.
