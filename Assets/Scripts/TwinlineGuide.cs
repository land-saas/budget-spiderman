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
        "<b>HOW TO SWING</b>\n\n" +
        "<color=#FFE02E>1  TAP SPACE</color>  to grab the glowing anchor.\n" +
        "     The rope keeps your speed and turns it into a swing.\n\n" +
        "<color=#FFE02E>2  HOLD SPACE</color>  to charge. The ring around you fills.\n\n" +
        "<color=#FFE02E>3  LET GO</color>  to launch. The arc shows where you will fly.\n" +
        "     More charge = farther. Nothing else adds speed.\n\n" +
        "<color=#FFAD14>PERFECT</color>  Let go while swinging <b>up and forward</b>\n" +
        "     with at least half charge. The arc turns gold.\n" +
        "     <color=#FFAD14>Gems sit on the perfect path</color> out of each anchor:\n" +
        "     line the gold arc up with them, then let go.\n" +
        "     Perfects in a row make every gem worth more.\n\n" +
        "Stay between the <color=#FF4738>red lines</color>.";

    private Canvas canvas;
    private Text panelText, promptText, statsText, popupText, tipText;
    private GameObject panel;
    private bool helpOpen;

    private void Start()
    {
        if (game == null) game = GetComponent<TwinSwingGame>();
        Build();
    }

    private void Build()
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var root = new GameObject("Swing Guide (runtime)");
        root.transform.SetParent(transform, false);
        canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.matchWidthOrHeight = 1f;

        panel = new GameObject("How to play", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(root.transform, false);
        var panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(.5f, .5f);
        panelRect.sizeDelta = new Vector2(760, 560);
        panelRect.anchoredPosition = new Vector2(0, 30);
        panel.GetComponent<Image>().color = new Color(0, 0, 0, .82f);
        panelText = Label(panel.transform, font, 26, TextAnchor.MiddleLeft, Vector2.zero, new Vector2(680, 520));
        panelText.text = HowToPlay;

        promptText = Label(root.transform, font, 30, TextAnchor.MiddleCenter, new Vector2(0, -360), new Vector2(1200, 60));
        statsText = Label(root.transform, font, 30, TextAnchor.MiddleRight, Vector2.zero, new Vector2(520, 60));
        var statsRect = statsText.rectTransform;
        statsRect.anchorMin = statsRect.anchorMax = statsRect.pivot = new Vector2(1, 1);
        statsRect.anchoredPosition = new Vector2(-28, -22);
        popupText = Label(root.transform, font, 54, TextAnchor.MiddleCenter, new Vector2(0, 230), new Vector2(900, 90));
        popupText.fontStyle = FontStyle.Bold;
        tipText = Label(root.transform, font, 28, TextAnchor.MiddleCenter, new Vector2(0, 120), new Vector2(1200, 120));
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
            string.Format(CultureInfo.InvariantCulture, "GEMS {0}   STREAK ×{1}", game.Score, game.GemValue);

        promptText.text = Prompt(state);

        float popup = game.PerfectPopup;
        popupText.text = popup > 0 ? (game.Streak > 1 ? $"PERFECT ×{game.Streak}" : "PERFECT!") : "";
        popupText.color = new Color(1f, .68f, .08f, Mathf.Clamp01(popup * 1.5f));
        popupText.rectTransform.localScale = Vector3.one * (1f + .25f * popup * popup);

        tipText.text = state == TwinSwingGame.Mode.Crashed && !helpOpen ? CrashTip() + "\n<size=22>H · how to play</size>" : "";
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
