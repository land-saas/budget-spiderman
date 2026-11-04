using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Connects the saved Canvas to the swing game. Typography, positions, colors
/// and artwork are authored in the scene/prefab rather than built by this script.
/// </summary>
[DefaultExecutionOrder(100)]
public sealed class TwinlineHud : MonoBehaviour
{
    public TwinSwingGame game;
    public Text distanceText;
    public Text countdownText;
    public Button actionButton;
    public Text actionLabel;
    public Image flashImage;
    public CanvasGroup readyGroup;
    public CanvasGroup flashGroup;

    [Header("Editable interface text")]
    public string beginText = "SPACE · BEGIN";
    public string retryText = "SPACE · RETRY";
    public string distanceFormat = "DIST {0:000}m";

    private void OnEnable() => Refresh();
    private void LateUpdate() => Refresh();

    public void Refresh()
    {
        if (game == null) return;

        if (distanceText != null)
            distanceText.text = string.Format(CultureInfo.InvariantCulture, distanceFormat,
                Mathf.FloorToInt(game.DistanceTravelled));

        bool actionVisible = game.State == TwinSwingGame.Mode.Ready || game.State == TwinSwingGame.Mode.Crashed;
        if (readyGroup != null)
        {
            readyGroup.alpha = actionVisible ? 1f : 0f;
            readyGroup.interactable = actionVisible;
            readyGroup.blocksRaycasts = actionVisible;
        }
        else if (actionButton != null) actionButton.gameObject.SetActive(actionVisible);
        if (actionButton != null) actionButton.interactable = actionVisible;
        if (actionLabel != null)
            actionLabel.text = game.State == TwinSwingGame.Mode.Crashed ? retryText : beginText;

        if (countdownText != null)
        {
            bool countdownVisible = game.State == TwinSwingGame.Mode.Countdown;
            countdownText.gameObject.SetActive(countdownVisible);
            if (countdownVisible)
                countdownText.text = Mathf.CeilToInt(game.CountdownRemaining).ToString(CultureInfo.InvariantCulture);
        }

        // A separate CanvasGroup changes only effect opacity. The Image color,
        // sprite, RectTransform and authored alpha remain the user's settings.
        if (flashGroup != null) flashGroup.alpha = Mathf.Clamp01(game.FlashOpacity);
    }

    /// <summary>The Button stores this callback as an ordinary persistent UnityEvent.</summary>
    public void PressAction()
    {
        if (game != null) game.PressAction();
    }
}
