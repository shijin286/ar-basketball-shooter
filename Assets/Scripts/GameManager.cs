using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// STAGE 5 + 6 + 9 - the rules and flow of the game.
///
/// Score (Stage 5):
///  - Every shot (BasketballShooter.ShotFired)     -> Shots + 1
///  - Every basket (HoopScoreTrigger.BasketScored) -> Score + Points Per Basket
///  - ScoreChanged fires straight away so UIManager updates the screen in the same frame.
///
/// Game states (Stage 6, GameOver added in Stage 9):
///   Title ──Free Play / Challenge──▶ PlacingHoop ──hoop placed──▶ Playing ◀──▶ Paused
///                                        ▲                          │
///                                        └──── Reposition Hoop ─────┤
///                                                     time up (Challenge only)
///                                                                   ▼
///                                Play Again / Main Menu ◀────── GameOver
///
/// Challenge mode (Stage 9):
///  - The clock only runs while Playing (not while placing the hoop or paused).
///  - At 0 the game goes to GameOver: shooting stops and the ball in hand is removed,
///    but balls already in the air can still score during the Buzzer Grace time.
///  - Then ChallengeFinished fires and a top-10 result can be saved with SubmitChallengeScore.
///
/// Attach to: GameSystems.
/// </summary>
[DisallowMultipleComponent]
public class GameManager : MonoBehaviour
{
    public enum GameState { Title, PlacingHoop, Playing, Paused, GameOver }
    public enum GameMode { FreePlay, Challenge }

    private const string PlayerNameKey = "ARBasketball.PlayerName";

    [Header("References (found automatically if empty)")]
    [SerializeField] private ARPlanePlacement placement;
    [SerializeField] private BasketballShooter shooter;
    [SerializeField] private BallSpawner spawner;

    [Header("Rules")]
    [Tooltip("Points for one basket. The project rules say 1.")]
    [SerializeField, Min(1)] private int pointsPerBasket = 1;

    [Header("Flow")]
    [Tooltip("Show the title screen first. Untick to jump straight to placing the hoop.")]
    [SerializeField] private bool showTitleOnStart = true;

    [Tooltip("Pause when the Android Back button (or Esc in the Editor) is pressed.")]
    [SerializeField] private bool backButtonPauses = true;

    [Header("Challenge mode (Stage 9)")]
    [Tooltip("Length of a challenge in seconds.")]
    [SerializeField, Min(5f)] private float challengeSeconds = 60f;

    [Tooltip("Balls already in the air when time runs out can still score for this long.")]
    [SerializeField, Min(0f)] private float buzzerGraceSeconds = 2f;

    [Tooltip("Beep every second for the last N seconds.")]
    [SerializeField, Min(0)] private int countdownFrom = 5;

    /// <summary>Raised whenever the score or shot count changes: (score, shots).</summary>
    public event Action<int, int> ScoreChanged;

    /// <summary>Raised once per basket, after the score has gone up.</summary>
    public event Action<BasketballController> BasketScored;

    /// <summary>Raised whenever the game state changes.</summary>
    public event Action<GameState> StateChanged;

    /// <summary>Challenge: raised once per second for the last few seconds (5, 4, 3, 2, 1).</summary>
    public event Action<int> CountdownTick;

    /// <summary>Challenge: raised when the clock hits zero.</summary>
    public event Action TimeUp;

    /// <summary>Challenge: raised after the buzzer grace time. true = the score qualifies for the leaderboard.</summary>
    public event Action<bool> ChallengeFinished;

    public int Score { get; private set; }
    public int Shots { get; private set; }
    public int Baskets { get; private set; }

    /// <summary>Baskets per shot, 0..1.</summary>
    public float Accuracy => Shots > 0 ? Mathf.Clamp01((float)Baskets / Shots) : 0f;

    public GameState State { get; private set; } = GameState.Title;
    public GameMode Mode { get; private set; } = GameMode.FreePlay;
    public bool IsPaused => State == GameState.Paused;
    public bool IsChallenge => Mode == GameMode.Challenge;

    public float ChallengeSeconds => challengeSeconds;
    public float TimeRemaining { get; private set; }

    /// <summary>Challenge results are final (after the buzzer grace time).</summary>
    public bool ResultsReady { get; private set; }

    /// <summary>Rank of the last saved result (1 = best), 0 if not saved or not in the top 10.</summary>
    public int LastRank { get; private set; }
    public bool ScoreSubmitted { get; private set; }

    /// <summary>The name typed last time (offered again on the next high score).</summary>
    public string LastPlayerName => PlayerPrefs.GetString(PlayerNameKey, "Player");

    /// <summary>The local leaderboard (created on first use).</summary>
    public Leaderboard Leaderboard => leaderboard ??= new Leaderboard(Leaderboard.DefaultPath);

    private HoopScoreTrigger hoopTrigger;
    private Leaderboard leaderboard;
    private GameState stateBeforePause = GameState.Playing;
    private bool hoopIsPlaced;
    private float graceRemaining;
    private int lastCountdown;

    private void Awake()
    {
        if (placement == null) placement = FindFirstObjectByType<ARPlanePlacement>();
        if (shooter == null) shooter = FindFirstObjectByType<BasketballShooter>();
        if (spawner == null) spawner = FindFirstObjectByType<BallSpawner>();

        if (placement == null) Debug.LogError("[GameManager] ARPlanePlacement not found - baskets can't be detected.", this);
        if (shooter == null) Debug.LogWarning("[GameManager] BasketballShooter not found - shots won't be counted.", this);
        if (spawner == null) Debug.LogWarning("[GameManager] BallSpawner not found - Restart can't reset the balls.", this);
    }

    private void OnEnable()
    {
        if (placement != null)
        {
            placement.HoopPlaced += OnHoopPlaced;
            placement.HoopRemoved += OnHoopRemoved;
            if (placement.IsHoopPlaced) OnHoopPlaced(placement.HoopTransform);
        }
        if (shooter != null) shooter.ShotFired += OnShotFired;
    }

    private void Start()
    {
        // Runs after every Awake, so it overrides "Placement Enabled On Start" etc.
        SetState(showTitleOnStart ? GameState.Title : PlayStateForHoop());
    }

    private void Update()
    {
        Tick(Time.deltaTime);

        if (!backButtonPauses) return;
        Keyboard keyboard = Keyboard.current; // the Android Back button arrives as the Escape key
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) TogglePause();
    }

    private void OnApplicationPause(bool appPaused)
    {
        // The phone went to the home screen or a call came in.
        if (appPaused) PauseGame();
    }

    private void OnDisable()
    {
        if (placement != null)
        {
            placement.HoopPlaced -= OnHoopPlaced;
            placement.HoopRemoved -= OnHoopRemoved;
        }
        if (shooter != null) shooter.ShotFired -= OnShotFired;
        WatchTrigger(null);
        Time.timeScale = 1f; // never leave the app frozen
    }

    // ---------------------------------------------------------------- buttons

    /// <summary>FREE PLAY button (title screen): no time limit.</summary>
    public void StartGame()
    {
        if (State != GameState.Title) return;
        Mode = GameMode.FreePlay;
        BeginRound();
        SetState(PlayStateForHoop());
    }

    /// <summary>60s CHALLENGE button (title screen).</summary>
    public void StartChallenge()
    {
        if (State != GameState.Title) return;
        Mode = GameMode.Challenge;
        BeginRound();
        SetState(PlayStateForHoop());
    }

    /// <summary>Pause button. Works while placing the hoop or playing.</summary>
    public void PauseGame()
    {
        if (State != GameState.PlacingHoop && State != GameState.Playing) return;
        stateBeforePause = State;
        SetState(GameState.Paused);
    }

    /// <summary>Resume button (pause screen).</summary>
    public void ResumeGame()
    {
        if (State != GameState.Paused) return;
        SetState(stateBeforePause == GameState.Playing ? PlayStateForHoop() : GameState.PlacingHoop);
    }

    /// <summary>Pause when playing, resume when paused.</summary>
    public void TogglePause()
    {
        if (State == GameState.Paused) ResumeGame();
        else PauseGame();
    }

    /// <summary>Restart Game / Play Again: same mode, score and clock reset, fresh ball, hoop stays.</summary>
    public void RestartGame()
    {
        if (State == GameState.Title) return;
        BeginRound();
        if (spawner != null) spawner.ClearAllBalls();
        SetState(PlayStateForHoop());
    }

    /// <summary>Main Menu button (results screen): back to the title screen. The hoop stays placed.</summary>
    public void ReturnToTitle()
    {
        if (State == GameState.Title) return;
        BeginRound();
        if (spawner != null) spawner.ClearAllBalls();
        Mode = GameMode.FreePlay;
        SetState(GameState.Title);
    }

    // ---------------------------------------------------------------- score

    /// <summary>Counts one shot.</summary>
    public void RegisterShot()
    {
        Shots++;
        ScoreChanged?.Invoke(Score, Shots);
    }

    /// <summary>Counts one basket. Only HoopScoreTrigger should call this (it guarantees one basket per ball).</summary>
    public void RegisterBasket(BasketballController ball)
    {
        if (State == GameState.GameOver && ResultsReady) return; // too late: the results are final

        Baskets++;
        Score += pointsPerBasket;
        BasketScored?.Invoke(ball);
        ScoreChanged?.Invoke(Score, Shots);
    }

    /// <summary>Sets score, shots and baskets back to zero.</summary>
    public void ResetScore()
    {
        Score = 0;
        Shots = 0;
        Baskets = 0;
        if (shooter != null) shooter.ResetShotCount();
        if (hoopTrigger != null) hoopTrigger.ResetCount();
        ScoreChanged?.Invoke(Score, Shots);
    }

    // ---------------------------------------------------------------- challenge

    /// <summary>Advances the challenge clock. Called every frame by Update (tests call it directly).</summary>
    public void Tick(float deltaTime)
    {
        if (Mode != GameMode.Challenge || deltaTime <= 0f) return;

        if (State == GameState.Playing)
        {
            TimeRemaining = Mathf.Max(0f, TimeRemaining - deltaTime);

            int wholeSeconds = Mathf.CeilToInt(TimeRemaining);
            if (wholeSeconds < lastCountdown && wholeSeconds > 0 && wholeSeconds <= countdownFrom)
            {
                CountdownTick?.Invoke(wholeSeconds);
            }
            lastCountdown = Mathf.Min(lastCountdown, wholeSeconds);

            if (TimeRemaining <= 0f)
            {
                SetState(GameState.GameOver);
                TimeUp?.Invoke();
            }
        }
        else if (State == GameState.GameOver && !ResultsReady)
        {
            graceRemaining -= deltaTime;
            if (graceRemaining <= 0f) FinishChallenge();
        }
    }

    /// <summary>Saves the finished challenge to the leaderboard (once). Returns the rank, or 0.</summary>
    public int SubmitChallengeScore(string playerName)
    {
        if (Mode != GameMode.Challenge || !ResultsReady || ScoreSubmitted) return LastRank;

        ScoreSubmitted = true;
        LastRank = Leaderboard.Submit(playerName, Score, Shots, Accuracy);
        PlayerPrefs.SetString(PlayerNameKey, Leaderboard.SanitizeName(playerName));
        PlayerPrefs.Save();
        return LastRank;
    }

    /// <summary>Uses a different leaderboard (for tests, or a second save slot).</summary>
    public void UseLeaderboard(Leaderboard board)
    {
        leaderboard = board;
    }

    private void FinishChallenge()
    {
        ResultsReady = true;
        ChallengeFinished?.Invoke(Leaderboard.Qualifies(Score, Shots));
    }

    private void BeginRound()
    {
        ResetScore();
        TimeRemaining = challengeSeconds;
        graceRemaining = buzzerGraceSeconds;
        lastCountdown = int.MaxValue;
        ResultsReady = false;
        ScoreSubmitted = false;
        LastRank = 0;
    }

    // ---------------------------------------------------------------- states

    private GameState PlayStateForHoop() => hoopIsPlaced ? GameState.Playing : GameState.PlacingHoop;

    private void SetState(GameState newState)
    {
        State = newState;

        Time.timeScale = newState == GameState.Paused ? 0f : 1f;
        if (placement != null) placement.SetPlacementEnabled(newState == GameState.PlacingHoop);
        if (shooter != null) shooter.SetShootingEnabled(newState == GameState.Playing);
        if (spawner != null)
        {
            spawner.SetSpawningEnabled(newState == GameState.Playing || newState == GameState.PlacingHoop);
            if (newState == GameState.Playing) spawner.SpawnBallInHand(); // returns the held ball if there is one
            if (newState == GameState.GameOver) spawner.DiscardHeldBall(); // flying balls may still score
        }

        StateChanged?.Invoke(newState);
    }

    // ---------------------------------------------------------------- events

    private void OnHoopPlaced(Transform hoop)
    {
        hoopIsPlaced = true;

        HoopScoreTrigger trigger = hoop != null ? hoop.GetComponentInChildren<HoopScoreTrigger>(true) : null;
        if (trigger == null)
        {
            Debug.LogWarning("[GameManager] The hoop has no HoopScoreTrigger. Rebuild it with " +
                             "Tools > AR Basketball > Create Hoop Prefab.", this);
        }
        WatchTrigger(trigger);

        if (State == GameState.PlacingHoop) SetState(GameState.Playing);
    }

    private void OnHoopRemoved()
    {
        hoopIsPlaced = false;
        if (State == GameState.Playing) SetState(GameState.PlacingHoop);
    }

    private void WatchTrigger(HoopScoreTrigger trigger)
    {
        if (hoopTrigger == trigger) return;
        if (hoopTrigger != null) hoopTrigger.BasketScored -= OnBasketScored;
        hoopTrigger = trigger;
        if (hoopTrigger != null) hoopTrigger.BasketScored += OnBasketScored;
    }

    private void OnShotFired(BasketballController ball, float power) => RegisterShot();

    private void OnBasketScored(BasketballController ball) => RegisterBasket(ball);
}
