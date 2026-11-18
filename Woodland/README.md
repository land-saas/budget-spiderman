# Woodland: editable 2.5D rope prototype

Open **this Woodland folder** as a project in Unity Hub with **Unity 6000.6.9f1**. Open `Assets/ReferenceWorld25D/WoodlandReference.unity` and press Play. Do not import it into the original 2D project: this edition uses URP, the Input System and 3D CharacterController physics. The repository's existing root project is preserved unchanged.

## Controls
- WASD / arrows: walk; Shift: run; Space: jump.
- Aim at a tree canopy or elevated surface, hold left mouse: shoot and reel toward a fixed anchor; release: detach. WASD steers while airborne.
- R: reset; N: toggle day/night.

The grapple is a CharacterController adaptation, not the original game's Rigidbody2D joint simulation.

## Editing
Scene objects are authored and editable, not recreated on Play. Terrain, dirt paths, boundaries, cabin panels, trees, lighting, player/camera, backdrop and rope anchors have separate numbered hierarchy groups. Move the cabin facade parent as one camera-facing unit; its collider is separate. Change movement and grapple values on Explorer. The 44 × 36 playable region has a larger decorative ground mesh to avoid exposing its edge. The backdrop is decorative, not the walkable map.

Older disabled artwork groups are retained within the scene for recovery; they do not run. Historical migration scripts and backup scenes are deliberately excluded from this edition to avoid automatic modifications on import.

## Public-safe artwork
This is a public-safe variant, not an identical export of the private local scene. Paid character sprites and screenshot-derived trees/grass/props were replaced with original procedural pixel placeholders. All nine 14 × 16 character frames share the same style, dimensions and pivot. Shooting uses the native side-facing frame and restores the walking/idle frames on release. It is not a dedicated arm-extension animation. See `PUBLIC-ART.txt`. The original local scene and licensed assets are unchanged.

## Validation
`WoodlandValidation.Validate` checks scene load, missing scripts, nine matching character frames, firing geometry, left/right fire and release, out-of-range rejection and ground collision. It writes `Verification/result.txt` and a render when a graphics device is available. Run via Unity `-batchmode -projectPath <Woodland> -executeMethod WoodlandValidation.Validate -quit`.

Automated checks are not a full manual keyboard/mouse playtest or a platform player build. No multiplayer, save system or complete game progression is included.
