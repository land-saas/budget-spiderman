# Twinline

Swing on a rope, collect gems, and let go at the right moment.

This is the existing one-button Twinline prototype, organized as an editable Unity project. Its player, anchors, gems, rope, camera, and boundaries are saved in the scene. Reusable prefabs, imported sprites, materials, and sound files live in `Assets`.

The game keeps its original red, yellow, and orange geometric style, with a taut, lightly elastic rope and fixed-world anchors.

![Twinline shared-camera gameplay](docs/images/ready.png)

## Open and play

1. In Unity Hub, choose **Add project from disk** and select this folder, which contains `Assets`, `Packages`, and `ProjectSettings`.
2. Open with **Unity 6000.6.2f1**.
3. Open `Assets/Scenes/Flight.unity`.
4. Press **Play**, then **Space** to begin.

| Input | Action |
| --- | --- |
| **Space** on the start or crash screen | Begin or retry |
| A first tap of **Space** near an anchor | Attach and stay connected |
| While attached, press/hold **Space**, then release | Detach and launch with your momentum and charge |
| Hold longer, then release | Store more spring tension and charge for a stronger launch |

Press Space to begin. During the run, a short first tap attaches and stays connected. While attached, press and hold Space to charge, then release to detach and launch. A second quick tap detaches without charging. A longer hold builds launch momentum with a small, damped stretch. The player keeps its normal size; the rope has no decorative waves or loose simulated strand. Holding the initial attaching press also charges; releasing that charged hold launches. You can hold through the opening countdown to catch the first anchor. Losing app focus safely detaches without launching. All anchors and gems stay fixed in world space. The player and camera travel through them; only objects far behind the player and offscreen recycle ahead.

Collect orange gems and avoid the floor, ceiling, and obstacles. The course remains stationary and recycles behind the camera as you travel. The nearest reachable anchor grows to show the attachment target.

## Edit the starting scene directly

Open **Assets/Scenes/Flight.unity**, not a `.cs` script. The Hierarchy exposes **Player**, **Main Camera**, **Course**, **Interface (Canvas)**, **Audio**, and **Game Controller** as ordinary scene objects. Use **Twinline → Show Starting Scene For Editing** to frame the starting area in 2D.

See [Start editing the scene](docs/EditableProject.md) for changing visuals, native UI, audio, and gameplay settings through the Inspector.

## Edit the game

Open `Flight.unity` before pressing Play to inspect the saved scene objects. Select the object with `TwinSwingGame` to tune gravity, attachment range, rope limits, anchor spacing, gem spacing, and pump strength in the Inspector.

With **Use Authored Course** enabled, moving the initial anchor and gem instances changes the opening layout. Retry restores those authored positions. The later endless course uses the controller's spacing settings and seeded placement.

Under **Elastic rope**, tune **Rope Frequency** (lower is softer), **Rope Damping** (lower bounces longer), **Hold Stretch Acceleration**, and **Maximum Hold Stretch**, **Release Recoil Strength** and **Maximum Release Recoil**. Under **Rope appearance**, tune segment count and slack. The visual rope is bounded between its two endpoints, and catching never multiplies player speed. Rope length limits set the resting length; physical stretch can exceed it. Existing pump strength and charge-time settings still apply.

Open `Assets/Scenes/AssetLibrary.unity` to browse the asset gallery. See [the asset library guide](docs/AssetLibrary.md) for prefab editing, sprite replacement, and sound customization.

Normal project opening does not rebuild the authored scene. **Twinline → Build Editable Project** recreates the supplied scene and assets; save a copy of any custom layout before using it.

## Project contents

| Location | Contents |
| --- | --- |
| `Assets/Scenes` | Playable scene and asset gallery |
| `Assets/Prefabs/Gameplay` | Reusable player, anchor, gem, rope, and boundary objects |
| `Assets/Prefabs/UI` | Editable native Canvas interface |
| `Assets/Art/Sprites` | Imported PNG sprites for game objects and optional visual variants |
| `Assets/Art/Materials` | Saved rendering materials |
| `Assets/Audio/SFX` | Imported attachment, release, pickup, and crash cues |
| `Assets/Scripts` | Runtime movement, collision, collection, and course logic |
| `Assets/Editor` | Explicit authoring tools and validation commands |

## Prototype history

The active game is **single-player rope swinging and gem collection**. The earlier cooperative flight scripts remain in the project for reference. The **Twinline → Validate Flight Mechanics** command checks that older mode; it is separate from swing-game verification.

- [Original co-op flight mechanics](docs/Mechanics.md)
- [Design research and playtest questions](Design-notes.md)
- [Previously published prototype](https://github.com/land-saas/twinline/releases)

## Rope verification

Run **Twinline → Validation → Run All Rope Checks** for the complete saved-project, physics and Play Mode suite. Use **Validate Elastic Rope** for real 2D spring-solver checks of bounce, a fixed pivot, first-press charging, detach/launch distance, focus loss, retry and repeated-catch stability. **Run Swing Play Mode Checks** tests the saved scene in actual Play mode, including rendered rope endpoints, input, collection, collision and retry. Reports and captured frames are written to `Verification/`. These checks do not rebuild or save the authored scene.
