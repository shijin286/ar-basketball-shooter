using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// STAGE 5 + 6 + 9 - everything the player sees on screen.
///
///  - HUD: Score, Shots, Accuracy and (challenge only) the countdown timer.
///  - Basket feedback: a short pop-up message ("SWISH!", "+1", ...) and a "punch" on the score.
///  - Panels for each GameManager state:
///      Title       -> Title Panel (Free Play, 60s Challenge, Leaderboard, best score)
///      PlacingHoop -> HUD + Place Hoop instructions
///      Playing     -> HUD (+ Aim & Shoot instructions for a few seconds after starting)
///      Paused      -> HUD + Pause Panel (Resume / Restart Game)
///      GameOver    -> HUD + Game Over Panel (results, name entry for a top-10 score,
///                     Play Again / Main Menu / Leaderboard)
///  - Leaderboard Panel: top 10, opened from the title or results screen.
///
/// Buttons call GameManager (or this UIManager for the leaderboard and Save) via On Click.
/// Attach to: the Canvas. Animations use unscaled time, so they still play while paused.
/// </summary>
[DisallowMultipleComponent]
public class UIManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("GameManager on GameSystems. Found automatically if empty.")]
    [SerializeField] private GameManager gameManager;

    [Header("HUD")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text shotsText;
    [Tooltip("Optional. Shows baskets per shot, e.g. 'Accuracy: 40%'.")]
    [SerializeField] private TMP_Text accuracyText;

    [Header("Basket message")]
    [Tooltip("Big text in the middle of the screen. Starts hidden.")]
    [SerializeField] private TMP_Text basketMessage;
    [SerializeField] private float messageDuration = 1.2f;
    [SerializeField] private string[] successMessages = { "SWISH!", "BASKET!", "Nice shot!", "+1" };

    [Header("Panels (Stage 6)")]
    [Tooltip("Game title + Start buttons.")]
    [SerializeField] private GameObject titlePanel;

    [Tooltip("Score, shots, pause button and in-game buttons.")]
    [SerializeField] private GameObject hudPanel;

    [Tooltip("'Place the hoop' instructions, shown while placing.")]
    [SerializeField] private GameObject placeHoopPanel;

    [Tooltip("'Aim and shoot' instructions, shown for a few seconds once the hoop is placed.")]
    [SerializeField] private GameObject aimShootPanel;

    [Tooltip("Resume + Restart Game.")]
    [SerializeField] private GameObject pausePanel;

    [Tooltip("The pause button in the HUD (hidden on the title, pause and results screens).")]
    [SerializeField] private GameObject pauseButton;

    [Tooltip("Seconds the Aim & Shoot instructions stay on screen (they also hide on the first shot).")]
    [SerializeField] private float aimHintSeconds = 5f;

    [Header("Challenge (Stage 9)")]
    [Tooltip("Countdown in the HUD, e.g. '0:42'. Only shown in the 60s Challenge.")]
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private float lowTimeSeconds = 10f;
    [SerializeField] private Color lowTimeColour = new Color(1f, 0.3f, 0.25f);

    [Tooltip("Results screen shown when the time is up.")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private TMP_Text rankText;

    [Tooltip("Name field + Save button, shown only for a top-10 score.")]
    [SerializeField] private GameObject nameEntry;
    [SerializeField] private TMP_InputField nameInput;

    [Header("Leaderboard (Stage 9)")]
    [SerializeField] private GameObject leaderboardPanel;
    [SerializeField] private TMP_Text leaderboardText;
    [Tooltip("Optional text on the title screen, e.g. 'Challenge best: 14'.")]
    [SerializeField] private TMP_Text bestScoreText;

    private Coroutine messageRoutine;
    private Coroutine punchRoutine;
    private Coroutine aimHintRoutine;
    private bool aimHintPending = true;
    private Color timerBaseColour = Color.white;

    private void Awake()
    {
        if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();
        if (gameManager == null)
        {
            Debug.LogError("[UIManager] GameManager not found in the scene.", this);
            enabled = false;
            return;
        }

        if (scoreText == null || shotsText == null)
        {
            Debug.LogWarning("[UIManager] Score Text or Shots Text is not assigned.", this);
        }

        if (basketMessage != null) basketMessage.gameObject.SetActive(false);
        if (timerText != null) timerBaseColour = timerText.color;
        if (nameInput != null) nameInput.characterLimit = Leaderboard.MaxNameLength;
    }

    private void OnEnable()
    {
        if (gameManager == null) return;
        gameManager.ScoreChanged += Refresh;
        gameManager.BasketScored += OnBasketScored;
        gameManager.StateChanged += ShowState;
        gameManager.ChallengeFinished += OnChallengeFinished;
        Refresh(gameManager.Score, gameManager.Shots);
        ShowState(gameManager.State);
    }

    private void OnDisable()
    {
        if (gameManager == null) return;
        gameManager.ScoreChanged -= Refresh;
        gameManager.BasketScored -= OnBasketScored;
        gameManager.StateChanged -= ShowState;
        gameManager.ChallengeFinished -= OnChallengeFinished;
    }

    private void Update()
    {
        UpdateTimer();
    }

    // ---------------------------------------------------------------- public API

    /// <summary>Writes the score, shot count and accuracy on screen.</summary>
    public void Refresh(int score, int shots)
    {
        if (scoreText != null) scoreText.text = $"Score: {score}";
        if (shotsText != null) shotsText.text = $"Shots: {shots}";
        if (accuracyText != null)
        {
            float accuracy = gameManager != null ? gameManager.Accuracy : 0f;
            accuracyText.text = $"Accuracy: {Mathf.RoundToInt(accuracy * 100f)}%";
        }

        if (shots > 0) HideAimHint(); // the player has got the idea
        if (gameOverPanel != null && gameOverPanel.activeSelf) UpdateResultText(); // buzzer beaters
    }

    /// <summary>Shows the panels that belong to a game state.</summary>
    public void ShowState(GameManager.GameState state)
    {
        bool title = state == GameManager.GameState.Title;
        bool placing = state == GameManager.GameState.PlacingHoop;
        bool playing = state == GameManager.GameState.Playing;
        bool paused = state == GameManager.GameState.Paused;
        bool gameOver = state == GameManager.GameState.GameOver;

        SetActive(titlePanel, title);
        SetActive(hudPanel, !title);
        SetActive(placeHoopPanel, placing);
        SetActive(pausePanel, paused);
        SetActive(pauseButton, placing || playing);
        SetActive(gameOverPanel, gameOver);
        SetActive(leaderboardPanel, false);

        if (title || placing) aimHintPending = true; // show the shooting tips again after (re)placing
        if (playing && aimHintPending)
        {
            aimHintPending = false;
            ShowAimHint();
        }
        else if (!playing)
        {
            HideAimHint();
        }

        if (gameOver) PrepareResults();
        if (title) UpdateBestScore();
        UpdateTimer();
    }

    /// <summary>Pops up a random success message for a moment.</summary>
    public void ShowBasketMessage()
    {
        if (basketMessage == null) return;

        string text = successMessages != null && successMessages.Length > 0
            ? successMessages[Random.Range(0, successMessages.Length)]
            : "+1";
        if (gameManager != null && gameManager.State == GameManager.GameState.GameOver) text = "BUZZER BEATER!";

        if (messageRoutine != null) StopCoroutine(messageRoutine);
        messageRoutine = StartCoroutine(MessageRoutine(text));
    }

    /// <summary>LEADERBOARD button.</summary>
    public void OpenLeaderboard()
    {
        ShowLeaderboard(0);
    }

    /// <summary>CLOSE button on the leaderboard panel.</summary>
    public void CloseLeaderboard()
    {
        SetActive(leaderboardPanel, false);
    }

    /// <summary>SAVE button on the results screen: stores the name and score, then shows the board.</summary>
    public void SaveScore()
    {
        if (gameManager == null) return;
        int rank = gameManager.SubmitChallengeScore(nameInput != null ? nameInput.text : "");
        SetActive(nameEntry, false);
        if (rankText != null) rankText.text = rank > 0 ? $"Saved! You are #{rank}" : "Not in the top 10 this time.";
        ShowLeaderboard(rank);
    }

    /// <summary>"m:ss", rounded up so the display shows 0:01 until the very end.</summary>
    public static string FormatTime(float seconds)
    {
        int whole = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        return $"{whole / 60}:{whole % 60:00}";
    }

    /// <summary>One line per entry: rank, name, score, accuracy. The highlighted rank is coloured.</summary>
    public static string FormatLeaderboard(IReadOnlyList<LeaderboardEntry> entries, int highlightRank)
    {
        if (entries == null || entries.Count == 0) return "No scores yet.\nPlay a 60s Challenge to set the first record!";

        var text = new StringBuilder();
        for (int i = 0; i < entries.Count; i++)
        {
            LeaderboardEntry e = entries[i];
            string line = $"{i + 1}.<pos=12%>{e.name}<pos=64%>{e.score}<pos=82%>{Mathf.RoundToInt(e.accuracy * 100f)}%";
            if (i + 1 == highlightRank) line = $"<color=#FFB020><b>{line}</b></color>";
            text.AppendLine(line);
        }
        return text.ToString().TrimEnd();
    }

    // ---------------------------------------------------------------- challenge UI

    private void UpdateTimer()
    {
        if (timerText == null) return;

        bool show = gameManager != null && gameManager.IsChallenge && gameManager.State != GameManager.GameState.Title;
        SetActive(timerText.gameObject, show);
        if (!show) return;

        float remaining = gameManager.TimeRemaining;
        timerText.text = FormatTime(remaining);

        bool low = remaining <= lowTimeSeconds && gameManager.State == GameManager.GameState.Playing;
        timerText.color = low ? lowTimeColour : timerBaseColour;
        float pulse = low ? 1f + 0.12f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * Mathf.PI)) : 1f;
        timerText.transform.localScale = Vector3.one * pulse;
    }

    private void PrepareResults()
    {
        UpdateResultText();
        SetActive(nameEntry, false);
        if (rankText != null) rankText.text = "Counting the last shots...";
    }

    private void OnChallengeFinished(bool qualifies)
    {
        UpdateResultText();

        if (qualifies)
        {
            if (rankText != null) rankText.text = "New high score! Enter your name:";
            if (nameInput != null) nameInput.text = gameManager.LastPlayerName;
            SetActive(nameEntry, true);
        }
        else if (rankText != null)
        {
            rankText.text = gameManager.Score > 0 ? "Not in the top 10 this time. Try again!" : "No baskets this time. Try again!";
        }
    }

    private void UpdateResultText()
    {
        if (resultText == null || gameManager == null) return;
        resultText.text = $"Score: {gameManager.Score}\nShots: {gameManager.Shots}\n" +
                          $"Accuracy: {Mathf.RoundToInt(gameManager.Accuracy * 100f)}%";
    }

    private void ShowLeaderboard(int highlightRank)
    {
        if (leaderboardText != null && gameManager != null)
        {
            leaderboardText.text = FormatLeaderboard(gameManager.Leaderboard.Entries, highlightRank);
        }
        SetActive(leaderboardPanel, true);
    }

    private void UpdateBestScore()
    {
        if (bestScoreText == null || gameManager == null) return;
        int best = gameManager.Leaderboard.BestScore;
        bestScoreText.text = best > 0 ? $"Challenge best: {best}" : "Set the first challenge record!";
    }

    // ---------------------------------------------------------------- aim hint

    private void ShowAimHint()
    {
        if (aimShootPanel == null) return;
        aimShootPanel.SetActive(true);
        if (aimHintRoutine != null) StopCoroutine(aimHintRoutine);
        if (Application.isPlaying && isActiveAndEnabled) aimHintRoutine = StartCoroutine(HideAimHintLater());
    }

    private void HideAimHint()
    {
        if (aimHintRoutine != null)
        {
            StopCoroutine(aimHintRoutine);
            aimHintRoutine = null;
        }
        SetActive(aimShootPanel, false);
    }

    private IEnumerator HideAimHintLater()
    {
        yield return new WaitForSecondsRealtime(aimHintSeconds);
        aimHintRoutine = null;
        SetActive(aimShootPanel, false);
    }

    // ---------------------------------------------------------------- basket feedback

    private void OnBasketScored(BasketballController ball)
    {
        ShowBasketMessage();
        if (scoreText != null)
        {
            if (punchRoutine != null) StopCoroutine(punchRoutine);
            punchRoutine = StartCoroutine(Punch(scoreText.transform, 1.3f, 0.25f));
        }
    }

    private IEnumerator MessageRoutine(string text)
    {
        basketMessage.text = text;
        basketMessage.alpha = 1f;
        basketMessage.gameObject.SetActive(true);
        Transform t = basketMessage.transform;

        // Pop in: 0.5x -> 1.15x -> 1x in 0.2 s
        for (float time = 0f; time < 0.2f; time += Time.unscaledDeltaTime)
        {
            float k = time / 0.2f;
            t.localScale = Vector3.one * (k < 0.6f ? Mathf.Lerp(0.5f, 1.15f, k / 0.6f) : Mathf.Lerp(1.15f, 1f, (k - 0.6f) / 0.4f));
            yield return null;
        }
        t.localScale = Vector3.one;

        // Stay, then fade out over the last 0.3 s.
        float stay = Mathf.Max(0f, messageDuration - 0.5f);
        for (float time = 0f; time < stay; time += Time.unscaledDeltaTime) yield return null;
        for (float time = 0f; time < 0.3f; time += Time.unscaledDeltaTime)
        {
            basketMessage.alpha = 1f - time / 0.3f;
            yield return null;
        }

        basketMessage.gameObject.SetActive(false);
        messageRoutine = null;
    }

    private IEnumerator Punch(Transform target, float scale, float duration)
    {
        for (float time = 0f; time < duration; time += Time.unscaledDeltaTime)
        {
            float k = Mathf.Sin(time / duration * Mathf.PI); // 0 -> 1 -> 0
            target.localScale = Vector3.one * Mathf.Lerp(1f, scale, k);
            yield return null;
        }
        target.localScale = Vector3.one;
        punchRoutine = null;
    }

    private static void SetActive(GameObject go, bool active)
    {
        if (go != null && go.activeSelf != active) go.SetActive(active);
    }
}
