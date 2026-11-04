# Twinline

Swing on a rope, collect gems, and let go at the right moment.

This is the existing one-button Twinline prototype, organized as an editable Unity project. Its player, anchors, gems, rope, camera, and boundaries are saved in the scene. Reusable prefabs, imported sprites, materials, and sound files live in `Assets`.

The game keeps its original red, yellow, and orange geometric style and rope physics.

![Twinline shared-camera gameplay](docs/images/ready.png)

## Open and play

1. In Unity Hub, choose **Add project from disk** and select this folder, which contains `Assets`, `Packages`, and `ProjectSettings`.
2. Open with **Unity 6000.6.2f1**.
3. Open `Assets/Scenes/Flight.unity`.
4. Press **Play**, then **Space** to begin.

| Input | Action |
| --- | --- |
| **Space** on the start or crash screen | Begin or retry |
| **Space** near an anchor | Attach to the nearest reachable anchor |
| A quick tap while attached | Release the rope |
| Hold while attached, then release | Charge and apply a momentum boost |

Collect orange gems and avoid the floor, ceiling, and obstacles. The course scrolls and recycles as you travel. The nearest reachable anchor grows to show the attachment target.

## Edit the starting scene directly

Open **Assets/Scenes/Flight.unity**, not a `.cs` script. The Hierarchy exposes **Player**, **Main Camera**, **Course**, **Interface (Canvas)**, **Audio**, and **Game Controller** as ordinary scene objects. Use **Twinline → Show Starting Scene For Editing** to frame the starting area in 2D.

See [Start editing the scene](docs/EditableProject.md) for changing visuals, native UI, audio, and gameplay settings through the Inspector.

## Edit the game

Open `Flight.unity` before pressing Play to inspect the saved scene objects. Select the object with `TwinSwingGame` to tune gravity, attachment range, scrolling speed, rope limits, anchor spacing, gem spacing, and pump strength in the Inspector.

With **Use Authored Course** enabled, moving the initial anchor and gem instances changes the opening layout. Retry restores those authored positions. The later endless course uses the controller's spacing settings and seeded placement.

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
