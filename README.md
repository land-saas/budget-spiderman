# Woodland: editable 2.5D rope prototype

Open **this repository root folder** as a project in Unity Hub with **Unity 6000.6.9f1**. On first interactive import, the playable **WoodlandReference** scene opens automatically; press **Play**. If Unity is restoring an existing scene or you already have unsaved work, the helper deliberately leaves it alone. In that case, open `Assets/ReferenceWorld25D/WoodlandReference.unity` manually. It is also the enabled first build scene. Do not import it into the original 2D project: this edition uses URP, the Input System and 3D CharacterController physics. This replaces the earlier root 2D prototype; that version remains available in Git history.

The automatic opener runs only once per local Library folder, never saves scene changes, and does not run in batch mode. Subsequent launches keep your own scene selection. Deleting Library resets that one-time check.

## Controls
- WASD / arrows: walk; Shift: run; Space: jump.
- Hold left mouse to automatically attach to the nearest sky anchor and build swing momentum; release to launch with your current velocity. WASD / arrows pump the swing and steer in flight. Click another ring to chain swings. Space performs a ground jump plus one air jump. R resets. The camera follows your altitude.
- R: reset; N: toggle day/night.

The grapple is a CharacterController adaptation, not the original game's Rigidbody2D joint simulation.

## Editing
Scene objects are authored and editable, not recreated on Play. Terrain, dirt paths, disabled boundaries, cabin panels, trees, lighting, player/camera, backdrop and rope anchors have separate numbered hierarchy groups. Move the cabin facade parent as one camera-facing unit; its collider is separate. Change movement and grapple values on Explorer. The authored central region extends into procedural terrain during Play. The backdrop is decorative, not the walkable map.

Older disabled artwork groups are retained within the scene for recovery; they do not run. Historical migration scripts and backup scenes are deliberately excluded from this edition to avoid automatic modifications on import.

## Public-safe artwork
This is a public-safe variant, not an identical export of the private local scene. Paid character sprites and screenshot-derived trees/grass/props were replaced with original procedural pixel placeholders. All nine 14 × 16 character frames share the same style, dimensions and pivot. Shooting uses the native side-facing frame and restores the walking/idle frames on release. It is not a dedicated arm-extension animation. See `PUBLIC-ART.txt`. The original local scene and licensed assets are unchanged.

## Validation
`WoodlandValidation.Validate` checks scene load, missing scripts, nine matching character frames, firing geometry, left/right fire and release, out-of-range rejection and ground collision. It writes `Verification/result.txt` and a render when a graphics device is available. Run via Unity `-batchmode -projectPath <Woodland> -executeMethod WoodlandValidation.Validate -quit`.

Automated checks are not a full manual keyboard/mouse playtest or a platform player build. No multiplayer, save system or complete game progression is included.

## Momentum upgrade
11 stationary golden sky rings form an editable route in `08 Sky anchors - editable swing route`. Reposition each object in the scene. On Explorer, tune reel speed, spring strength, radial damping, gravity, maximum air speed, and steering. Release preserves velocity, with no canned launch teleport or reset. `WoodlandMomentumSetup.Validate` tests moving attachment, release velocity continuity, ballistic travel, midair reattachment, repeated release and landing.

## Extending traversal
Left mouse automatically locks onto the **nearest sky anchor to the player** within 30 units; hold to reel at 9 units/s and build physical momentum, release to retain that velocity. WASD/arrows steer/pump. Space performs a ground jump, then one air jump; the air-jump allowance resets on landing, not on attaching/releasing. R resets. Invisible boundary colliders are disabled.

`09 Procedural extension - runtime chunks` streams a deterministic 5×5 neighborhood of 32-unit terrain chunks around the player, with golden anchors and decorative non-colliding pine silhouettes. Old chunks unload; the authored central town remains editable. Change the seed, materials, size and radius on that object. Generated chunks appear during Play and are not saved scene content. There is no floating-origin system, so extreme travel distances eventually encounter floating-point precision limits.

`WoodlandTravelSetup.Validate` checks jump limits, fast reel, nearest-anchor selection, removed air boundaries, bounded chunk counts and ground/anchors beyond the original map. These are automated simulation checks, not a manual full-game playtest.
