using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Teaches the swing: a how-to-play panel before each run, short prompts until the
/// player has done each move a few times, a gem/streak counter, "perfect" popups and
/// a tip after each crash. Builds its own overlay Canvas so the saved HUD is untouched.
/// </summary>
[DefaultExecutionOrder(110)]
public sealed class TwinlineGuide : MonoBehaviour
{
    public TwinSwingGame game;
    [Tooltip("Prompts stop once the player has grabbed and launched this many times.")]
    [Min(0)] public int promptRepetitions = 3;
    public KeyCode helpKey = KeyCode.H;

    private const string HowToPlay =
        "<color=#FFE02E>1  TAP SPACE</color>  to grab the glowing anchor.\n" +
        "     The rope keeps your speed and turns it into a swing.\n\n" +
        "<color=#FFE02E>2  HOLD SPACE</color>  to charge. The ring around you fills.\n\n" +
        "<color=#FFE02E>3  LET GO</color>  to launch. The arc shows where you will fly.\n" +
        "     More charge = farther. Nothing else adds speed.\n\n" +
        "<color=#FFAD14>PERFECT</color>  Let go while swinging <b>up and forward</b>\n" +
        "     with at least half charge. The arc turns gold.\n" +
        "     <color=#FFAD14>Gems sit on the perfect path</color> out of each anchor:\n" +
        "     when you grab, they settle onto your swing.\n" +
        "     Line the gold arc up with them and let go.\n" +
        "     Perfects in a row make every gem worth more.\n\n" +
        "Stay between the <color=#FF4738>red lines</color>.";

    private static readonly Color PanelColor = new Color(.07f, .04f, .12f, .9f);
    private static readonly Color PillColor = new Color(.07f, .04f, .12f, .72f);
    private static readonly Color Accent = new Color(1f, .68f, .08f);

    private Canvas canvas;
    private Text panelText, promptText, statsText, popupText, tipText;
    private GameObject panel;
    private RectTransform promptPill, statsPill, tipPill;
    private Sprite rounded;
    private bool helpOpen;

    private void Start()
    {
        if (game == null) game = GetComponent<TwinSwingGame>();
        Build();
    }

    private void Build()
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        rounded = RoundedSprite();
        var root = new GameObject("Swing Guide (runtime)");
        root.transform.SetParent(transform, false);
        canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.matchWidthOrHeight = 1f;

        // How-to-play card: rounded dark panel, gold title and rule, roomy body text.
        var card = Box(root.transform, "How to play", new Vector2(0, 40), new Vector2(780, 640), PanelColor);
        panel = card.gameObject;
        var stripe = Box(card, "Accent", Vector2.zero, new Vector2(780, 8), Accent);
        stripe.anchorMin = stripe.anchorMax = new Vector2(.5f, 1); stripe.anchoredPosition = new Vector2(0, -4);
        var title = Label(card, font, 44, TextAnchor.MiddleCenter, new Vector2(0, 268), new Vector2(700, 60));
        title.text = "HOW TO SWING"; title.color = Accent; title.fontStyle = FontStyle.Bold;
        Box(card, "Rule", new Vector2(0, 230), new Vector2(120, 3), new Color(Accent.r, Accent.g, Accent.b, .6f));
        panelText = Label(card, font, 23, TextAnchor.UpperLeft, new Vector2(0, -50), new Vector2(680, 520));
        panelText.lineSpacing = 1.12f;
        panelText.text = HowToPlay;

        promptPill = Box(root.transform, "Prompt", new Vector2(0, -360), new Vector2(600, 58), PillColor);
        promptText = Label(promptPill, font, 28, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(1200, 58));
        statsPill = Box(root.transform, "Stats", Vector2.zero, new Vector2(360, 58), PillColor);
        statsPill.anchorMin = statsPill.anchorMax = statsPill.pivot = new Vector2(1, 1);
        statsPill.anchoredPosition = new Vector2(-24, -20);
        statsText = Label(statsPill, font, 28, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(600, 58));
        tipPill = Box(root.transform, "Crash tip", new Vector2(0, 120), new Vector2(900, 110), PanelColor);
        tipText = Label(tipPill, font, 27, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(1100, 110));
        popupText = Label(root.transform, font, 60, TextAnchor.MiddleCenter, new Vector2(0, 230), new Vector2(900, 100));
        popupText.fontStyle = FontStyle.Bold;
    }

    private RectTransform Box(Transform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        var rect = (RectTransform)obj.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.sizeDelta = size; rect.anchoredPosition = position;
        var image = obj.GetComponent<Image>();
        image.sprite = rounded; image.type = Image.Type.Sliced; image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    /// <summary>A small rounded-rectangle texture, nine-sliced so any box keeps crisp corners.</summary>
    private static Sprite RoundedSprite()
    {
        const int size = 64, radius = 18;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Rounded" };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + .5f, radius, size - radius), cy = Mathf.Clamp(y + .5f, radius, size - radius);
                float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(cx, cy));
                byte a = (byte)(Mathf.Clamp01(radius - d + .5f) * 255);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
        texture.SetPixels32(pixels); texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0,
            SpriteMeshType.FullRect, new Vector4(radius + 2, radius + 2, radius + 2, radius + 2));
    }

    private static Text Label(Transform parent, Font font, int size, TextAnchor align, Vector2 position, Vector2 box)
    {
        var obj = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(Outline));
        obj.transform.SetParent(parent, false);
        var rect = (RectTransform)obj.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.sizeDelta = box;
        rect.anchoredPosition = position;
        var text = obj.GetComponent<Text>();
        text.font = font; text.fontSize = size; text.alignment = align;
        text.supportRichText = true; text.color = new Color(.95f, .95f, .95f);
        text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        obj.GetComponent<Outline>().effectColor = new Color(0, 0, 0, .8f);
        return text;
    }

    private void Update()
    {
        if (game == null || canvas == null) return;
        var state = game.State;
        bool betweenRuns = state == TwinSwingGame.Mode.Ready || state == TwinSwingGame.Mode.Crashed;
        if (Input.GetKeyDown(helpKey) && betweenRuns) helpOpen = !helpOpen;
        if (!betweenRuns) helpOpen = false;

        panel.SetActive(state == TwinSwingGame.Mode.Ready || helpOpen);

        statsText.text = state == TwinSwingGame.Mode.Ready ? "" :
            string.Format(CultureInfo.InvariantCulture, "GEMS <color=#FFAD14>{0}</color>    STREAK <color=#FFAD14>×{1}</color>", game.Score, game.GemValue);
        FitPill(statsPill, statsText);

        promptText.text = Prompt(state);
        FitPill(promptPill, promptText);

        float popup = game.PerfectPopup;
        popupText.text = popup > 0 ? (game.Streak > 1 ? $"PERFECT ×{game.Streak}" : "PERFECT!") : "";
        popupText.color = new Color(1f, .68f, .08f, Mathf.Clamp01(popup * 1.5f));
        popupText.rectTransform.localScale = Vector3.one * (1f + .25f * popup * popup);

        tipText.text = state == TwinSwingGame.Mode.Crashed && !helpOpen ? CrashTip() + "\n<size=21><color=#B9A9C9>H · how to play</color></size>" : "";
        tipPill.gameObject.SetActive(tipText.text.Length > 0);
        if (tipPill.gameObject.activeSelf)
            tipPill.sizeDelta = new Vector2(Mathf.Min(1100, tipText.preferredWidth + 70), tipText.preferredHeight + 40);
    }

    /// <summary>Sizes a pill to its text and hides it when there is nothing to say.</summary>
    private static void FitPill(RectTransform pill, Text text)
    {
        bool show = text.text.Length > 0;
        pill.gameObject.SetActive(show);
        if (show) pill.sizeDelta = new Vector2(text.preferredWidth + 56, pill.sizeDelta.y);
    }

    private string Prompt(TwinSwingGame.Mode state)
    {
        if (state == TwinSwingGame.Mode.Countdown) return "Hold SPACE to grab the first anchor as soon as it starts";
        if (state != TwinSwingGame.Mode.Flying) return "";
        if (game.Attached)
        {
            if (game.TotalChargedLaunches >= promptRepetitions && !game.Charging) return "";
            if (game.Charging)
                return game.LaunchWouldBePerfect ? "<color=#FFAD14>Gold arc: line it up with the gems and let go</color>"
                                                 : "Let go to launch along the arc";
            return "HOLD SPACE to charge your launch";
        }
        if (game.TotalGrabs < promptRepetitions)
            return game.AnchorInReach ? "TAP SPACE to grab the glowing anchor" : "Fly toward an anchor until it glows";
        return "";
    }

    private string CrashTip()
    {
        if (game.LastCrashReason.Contains("boundary"))
            return game.LastCrashWasHigh
                ? "Too high. Use less charge, or let go a little earlier in the upswing."
                : "Fell too low. Grab the next glowing anchor sooner, or charge longer for more distance.";
        return "Hit something. Watch the arc before you let go.";
    }
}
