# Start editing the scene

Open this folder as a Unity project in **Unity 6000.6.2f1**, then double-click **Assets/Scenes/Flight.unity** in the Project window. This opens the editable starting scene. Double-clicking a `.cs` file opens a script instead.

Leave Play mode while making changes. Use the **Hierarchy**, **Scene** view, and **Inspector** to edit the actual saved objects. The scene is arranged into visible game objects and groups:

| Hierarchy object or group | What to edit |
| --- | --- |
| **Player** | Sprite, color, transform, physics body, and collision radius |
| **Main Camera** | Background color, orthographic size, and camera settings |
| **Course** | Placed anchor and gem instances and boundary colliders |
| **Interface** | Native Canvas, text labels, action button, and flash overlay |
| **Audio** | Sound-effects source and optional background-music source |
| **Game Controller** | Gameplay settings and references to the saved objects |

The Scene view can be set to **2D** and centered on the starting camera area. Select an object in the Hierarchy and press **F** to frame it. Press Play to try the scene; changes made during Play are temporary unless copied back to the saved scene.

## Change the look directly

Select the player's or gem's visual child and edit its `SpriteRenderer` sprite or color. Edit an anchor prefab to change the shared anchor appearance, or use an instance override for a single anchor. Select **Main Camera** to change the background. The imported sprites, reusable prefabs, materials, and sound cues remain available in the Project window; see [the asset library guide](AssetLibrary.md).

Under **Interface**, select a native `Text` object to edit its font, font size, color, alignment, and `RectTransform`. Select the native action `Button` to change its image and button colors. These saved style and layout values remain in place when Play starts and when a run retries. Runtime code updates the distance, countdown, action label, visibility, and flash opacity.

The `TwinlineHud` component connects the saved UI references and contains the begin/retry wording and distance format. The button's saved **On Click** event calls `TwinlineHud.PressAction`; keep that binding when changing its appearance. There is one saved Canvas and EventSystem for the interface.

## Adjust gameplay without opening scripts

Select **Game Controller**, then change the fields on `TwinSwingGame` in the Inspector:

| Settings | Effect |
| --- | --- |
| Swing physics | Gravity, attachment range, rope lengths, scrolling, and pump strength |
| Course generation | Seed, anchor and gem height ranges, spacing, and opening scroll speed |
| Run timing | Countdown duration and retry delay |
| Camera and visual effects | Camera following and lead, anchor highlight size, and player charge growth |
| Sound library | Imported cue references and individual cue volumes |

With **Use Authored Course** enabled, move the placed anchors and gems to edit the opening course. The scene restores those saved positions on retry. Later recycled objects use the course-generation settings. If you add or remove course objects, use **Register anchors and gems in this scene** in the controller's custom Inspector to update its arrays.

An optional music `AudioSource` is provided as a normal editable scene object. Assign an imported track and configure its loop, volume, and playback settings there. **No background-music track is supplied.**

## Save and verify

Save the scene after editing. Normal project opening and entering Play preserve the saved objects and styles. **Twinline → Validation → Validate Current Saved Project** checks the current saved scene without rebuilding it. **Twinline → Validation → Run Swing Play Mode Checks** exercises the real button, swinging, collection, collision, and retry behavior.

**Twinline → Upgrade Saved Interface** adds or connects the native interface in the existing scene. **Twinline → Build Editable Project** recreates the supplied default project assets and layout, so preserve a copy of a customized scene before using that rebuild command.
