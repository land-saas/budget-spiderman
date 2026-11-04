using System;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Adds a saved, editable Canvas to the existing scene without rebuilding its course or art.</summary>
public static class TwinlineInterfaceAuthoring
{
    private const string FlightScene = "Assets/Scenes/Flight.unity";
    private const string HudPrefab = "Assets/Prefabs/UI/GameHud.prefab";

    [MenuItem("Twinline/Upgrade Saved Interface")]
    private static void UpgradeFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Stop Play mode before upgrading the saved interface.");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        UpgradeInterface();
    }

    /// <summary>Batch entry point. Existing scene objects and existing HUD prefab edits are retained.</summary>
    public static void UpgradeInterface()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Cannot upgrade the interface while Unity is in Play mode.");

        MigrateEditableTints();
        var scene = EditorSceneManager.OpenScene(FlightScene, OpenSceneMode.Single);
        var game = UnityEngine.Object.FindAnyObjectByType<TwinSwingGame>();
        if (game == null) throw new InvalidOperationException("Flight.unity has no TwinSwingGame controller.");

        var hud = game.hud != null ? game.hud : UnityEngine.Object.FindAnyObjectByType<TwinlineHud>();
        if (hud == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefab);
            if (prefab == null) prefab = CreateHudPrefab();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = "Game HUD (edit Canvas and text here)";
            hud = instance.GetComponent<TwinlineHud>();
        }
        hud.game = game;
        game.hud = hud;
        PrefabUtility.RecordPrefabInstancePropertyModifications(hud);
        EditorUtility.SetDirty(hud);

        var eventSystem = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            var events = new GameObject("UI EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            eventSystem = events.GetComponent<EventSystem>();
        }
        // The game owns the Space key. Disable keyboard navigation/submission
        // rather than assigning invalid empty InputManager button names.
        // Mouse clicks remain handled normally by StandaloneInputModule.
        eventSystem.sendNavigationEvents = false;
        eventSystem.firstSelectedGameObject = null;
        EditorUtility.SetDirty(eventSystem);

        if (game.musicSource == null)
        {
            var music = new GameObject("Music (assign your own track)");
            music.transform.SetParent(game.transform, false);
            game.musicSource = music.AddComponent<AudioSource>();
            game.musicSource.loop = true;
            game.musicSource.playOnAwake = false;
            game.musicSource.spatialBlend = 0f;
        }

        TwinlineSceneEditing.ArrangeScene(game);
        EditorUtility.SetDirty(game);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(FlightScene);
        Selection.activeGameObject = game.player.gameObject;
        Debug.Log("TWINLINE_INTERFACE_UPGRADED: saved GameHud.prefab, linked Flight.unity Canvas, mouse-only UI events and empty music slot. Existing gameplay objects preserved.");
    }

    private static void MigrateEditableTints()
    {
        const string spriteFolder = "Assets/Art/Sprites/Gameplay/";
        var whiteDisc = AssetDatabase.LoadAssetAtPath<Sprite>(spriteFolder + "WhiteDisc.png");
        if (whiteDisc == null) return;
        var originalColors = new System.Collections.Generic.Dictionary<string, Color>
        {
            { spriteFolder + "Player.png", TwinSwingGame.PlayerRed },
            { spriteFolder + "Anchor.png", TwinSwingGame.NodeYellow },
            { spriteFolder + "AnchorCore.png", TwinSwingGame.PlayerRed },
            { spriteFolder + "Gem.png", TwinSwingGame.GemGold }
        };
        foreach (string name in new[] { "Player", "Anchor", "Gem" })
        {
            string path = "Assets/Prefabs/Gameplay/" + name + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool changed = false;
                foreach (var renderer in contents.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    // Convert only untouched generated sprites. A custom image
                    // or an existing user tint remains exactly as authored.
                    Color tint;
                    if (renderer.sprite == null || renderer.color != Color.white ||
                        !originalColors.TryGetValue(AssetDatabase.GetAssetPath(renderer.sprite), out tint)) continue;
                    renderer.sprite = whiteDisc;
                    renderer.color = tint;
                    changed = true;
                }
                if (changed) PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }
    }

    private static GameObject CreateHudPrefab()
    {
        Directory.CreateDirectory("Assets/Prefabs/UI");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        var root = UiObject("GameHud", null);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1440f, 900f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;
        root.AddComponent<GraphicRaycaster>();
        var hud = root.AddComponent<TwinlineHud>();

        var panel = UiObject("Distance panel", root.transform);
        Place(panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -10f), new Vector2(245f, 68f));
        var panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, .78f);
        panelImage.raycastTarget = false;
        hud.distanceText = MakeText("DistanceText", panel.transform, "DIST 000m", 34, TextAnchor.MiddleLeft,
            new Color(.85f, .85f, .85f));
        Place(hud.distanceText.gameObject, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -8f), new Vector2(225f, 52f));
        AddShadow(hud.distanceText);

        var actionGroup = UiObject("Begin and retry group", root.transform);
        Stretch(actionGroup);
        hud.readyGroup = actionGroup.AddComponent<CanvasGroup>();
        var buttonObject = UiObject("BeginRetry Button", actionGroup.transform);
        Place(buttonObject, new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0f, 22f), new Vector2(320f, 48f));
        var buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = Color.clear;
        buttonImage.raycastTarget = true;
        hud.actionButton = buttonObject.AddComponent<Button>();
        hud.actionButton.targetGraphic = buttonImage;
        hud.actionButton.transition = Selectable.Transition.None;
        var navigation = hud.actionButton.navigation;
        navigation.mode = Navigation.Mode.None;
        hud.actionButton.navigation = navigation;
        hud.actionLabel = MakeText("ActionLabel", buttonObject.transform, hud.beginText, 21, TextAnchor.MiddleCenter,
            new Color(.95f, .95f, .95f));
        Stretch(hud.actionLabel.gameObject);
        UnityEventTools.AddPersistentListener(hud.actionButton.onClick, hud.PressAction);

        hud.countdownText = MakeText("CountdownText", root.transform, "2", 112, TextAnchor.MiddleCenter,
            new Color(.95f, .95f, .95f));
        Place(hud.countdownText.gameObject, new Vector2(.5f, 1f), new Vector2(.5f, 1f),
            new Vector2(0f, -122f), new Vector2(250f, 157f));
        AddShadow(hud.countdownText);
        hud.countdownText.gameObject.SetActive(false);

        var flash = UiObject("Flash overlay (non-blocking)", root.transform);
        Stretch(flash);
        hud.flashImage = flash.AddComponent<Image>();
        hud.flashImage.color = Color.white;
        hud.flashImage.raycastTarget = false;
        hud.flashGroup = flash.AddComponent<CanvasGroup>();
        hud.flashGroup.alpha = 0f;
        hud.flashGroup.interactable = false;
        hud.flashGroup.blocksRaycasts = false;

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, HudPrefab);
        UnityEngine.Object.DestroyImmediate(root);
        if (prefab == null) throw new InvalidOperationException("GameHud.prefab could not be saved.");
        return prefab;
    }

    private static GameObject UiObject(string name, Transform parent)
    {
        var root = new GameObject(name, typeof(RectTransform));
        root.layer = LayerMask.NameToLayer("UI");
        root.transform.SetParent(parent, false);
        return root;
    }

    private static void Place(GameObject root, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        var rect = (RectTransform)root.transform;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(GameObject root)
    {
        var rect = (RectTransform)root.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static Text MakeText(string name, Transform parent, string value, int size, TextAnchor alignment, Color color)
    {
        var root = UiObject(name, parent);
        var text = root.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.supportRichText = false;
        text.resizeTextForBestFit = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static void AddShadow(Text text)
    {
        var shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = Color.black;
        shadow.effectDistance = new Vector2(2f, -2f);
        shadow.useGraphicAlpha = true;
    }
}
