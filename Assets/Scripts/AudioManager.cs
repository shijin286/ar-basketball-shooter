using UnityEngine;

/// <summary>
/// STAGE 7 - all game sounds.
///
///  - Ball hits something (BallSpawner.BallBounced): floor bounce, rim clank or backboard
///    thud, chosen from what was hit. Louder for harder hits, slightly different pitch each
///    time, played in 3D at the ball so you hear where it is in your room.
///  - Basket (GameManager.BasketScored): net swish at the ball + a short success chime.
///  - Shot (BasketballShooter.ShotFired): a soft whoosh, louder for stronger shots.
///  - Buttons: ButtonFeedback calls PlayButtonClick().
///  - Challenge (Stage 9): a beep for each of the last 5 seconds and a buzzer at time up.
///  - Pause: game sounds pause with the game; button clicks still play.
///
/// Attach to: GameSystems. Uses a small pool of AudioSources (created at runtime), so no
/// objects are created or destroyed while playing.
/// </summary>
[DisallowMultipleComponent]
public class AudioManager : MonoBehaviour
{
    public enum ImpactSurface { Floor, Rim, Backboard }

    /// <summary>The AudioManager in the scene (used by ButtonFeedback).</summary>
    public static AudioManager Instance { get; private set; }

    [Header("References (found automatically if empty)")]
    [SerializeField] private BallSpawner spawner;
    [SerializeField] private BasketballShooter shooter;
    [SerializeField] private GameManager gameManager;

    [Header("Clips (Assets/Audio)")]
    [SerializeField] private AudioClip floorBounce;
    [SerializeField] private AudioClip rimHit;
    [SerializeField] private AudioClip backboardHit;
    [SerializeField] private AudioClip basketSwish;
    [SerializeField] private AudioClip basketChime;
    [SerializeField] private AudioClip shoot;
    [SerializeField] private AudioClip buttonClick;

    [Header("Challenge clips (Stage 9)")]
    [SerializeField] private AudioClip countdownTick;
    [SerializeField] private AudioClip buzzer;

    [Header("Volume")]
    [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;

    [Tooltip("Impacts at this speed (m/s) or slower are silent.")]
    [SerializeField] private float quietImpactSpeed = 0.5f;

    [Tooltip("Impacts at this speed (m/s) or faster play at full volume.")]
    [SerializeField] private float loudImpactSpeed = 7f;

    [Tooltip("Random pitch change for impacts so repeated bounces don't sound identical.")]
    [SerializeField, Range(0f, 0.3f)] private float pitchVariation = 0.08f;

    [Header("3D sound")]
    [Tooltip("How many impact sounds can play at the same time.")]
    [SerializeField, Min(1)] private int voices = 8;

    [Tooltip("Full volume closer than this (metres). 2 m keeps shots audible on a phone speaker.")]
    [SerializeField] private float minDistance = 2f;

    [Tooltip("Quiet beyond this (metres).")]
    [SerializeField] private float maxDistance = 20f;

    [SerializeField] private bool muted;

    public bool Muted => muted;

    private AudioSource[] pool;
    private AudioSource uiSource;
    private int nextVoice;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[AudioManager] There are two AudioManagers - removing this one.", this);
            Destroy(this);
            return;
        }
        Instance = this;

        if (spawner == null) spawner = FindFirstObjectByType<BallSpawner>();
        if (shooter == null) shooter = FindFirstObjectByType<BasketballShooter>();
        if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();

        if (floorBounce == null || rimHit == null || basketSwish == null)
        {
            Debug.LogWarning("[AudioManager] Some clips are not assigned - those sounds will be silent.", this);
        }

        CreateSources();
    }

    private void OnEnable()
    {
        if (spawner != null) spawner.BallBounced += OnBallBounced;
        if (shooter != null) shooter.ShotFired += OnShotFired;
        if (gameManager != null)
        {
            gameManager.BasketScored += OnBasketScored;
            gameManager.StateChanged += OnStateChanged;
            gameManager.CountdownTick += OnCountdownTick;
            gameManager.TimeUp += OnTimeUp;
        }
    }

    private void OnDisable()
    {
        if (spawner != null) spawner.BallBounced -= OnBallBounced;
        if (shooter != null) shooter.ShotFired -= OnShotFired;
        if (gameManager != null)
        {
            gameManager.BasketScored -= OnBasketScored;
            gameManager.StateChanged -= OnStateChanged;
            gameManager.CountdownTick -= OnCountdownTick;
            gameManager.TimeUp -= OnTimeUp;
        }
        AudioListener.pause = false;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---------------------------------------------------------------- public API

    /// <summary>Called by ButtonFeedback on every button press.</summary>
    public void PlayButtonClick()
    {
        Play2D(buttonClick, 0.7f);
    }

    public void SetMuted(bool value)
    {
        muted = value;
    }

    /// <summary>Can be wired to a "Sound On/Off" button.</summary>
    public void ToggleMute()
    {
        SetMuted(!muted);
    }

    /// <summary>Works out what kind of thing the ball hit from the hoop prefab's object names.</summary>
    public static ImpactSurface GetSurface(Collider hit)
    {
        for (Transform t = hit != null ? hit.transform : null; t != null; t = t.parent)
        {
            string n = t.name;
            if (n.StartsWith("RimSegment") || n == "RimColliders" || n == "Bracket" || n == "Rim") return ImpactSurface.Rim;
            if (n == "BoardPanel" || n == "Backboard" || n == "Stand") return ImpactSurface.Backboard;
            if (n == "BasketballHoop") break; // e.g. VirtualFloor: part of the hoop but it is the floor
        }
        return ImpactSurface.Floor;
    }

    /// <summary>Impact speed (m/s) -> volume 0..1. Silent at 'quiet' or below, full at 'loud' or above.</summary>
    public static float ImpactVolume(float speed, float quiet, float loud)
    {
        if (speed <= quiet) return 0f;
        return Mathf.Lerp(0.15f, 1f, Mathf.InverseLerp(quiet, loud, speed));
    }

    // ---------------------------------------------------------------- game events

    private void OnBallBounced(BasketballController ball, float speed, Collider hit)
    {
        AudioClip clip = GetSurface(hit) switch
        {
            ImpactSurface.Rim => rimHit,
            ImpactSurface.Backboard => backboardHit,
            _ => floorBounce,
        };
        Play3D(clip, ball != null ? ball.transform.position : Vector3.zero, ImpactVolume(speed, quietImpactSpeed, loudImpactSpeed));
    }

    private void OnShotFired(BasketballController ball, float power)
    {
        Play2D(shoot, Mathf.Lerp(0.3f, 0.7f, power));
    }

    private void OnBasketScored(BasketballController ball)
    {
        Play3D(basketSwish, ball != null ? ball.transform.position : Vector3.zero, 1f);
        Play2D(basketChime, 0.5f);
    }

    private void OnCountdownTick(int secondsLeft)
    {
        Play2D(countdownTick, secondsLeft <= 3 ? 0.8f : 0.5f); // the last three beeps are louder
    }

    private void OnTimeUp()
    {
        Play2D(buzzer, 0.9f);
    }

    private void OnStateChanged(GameManager.GameState state)
    {
        // Freeze game sounds with the game; button clicks use a source that ignores this.
        AudioListener.pause = state == GameManager.GameState.Paused;
    }

    // ---------------------------------------------------------------- playback

    private void CreateSources()
    {
        pool = new AudioSource[Mathf.Max(1, voices)];
        for (int i = 0; i < pool.Length; i++)
        {
            var go = new GameObject($"Voice{i}");
            go.transform.SetParent(transform, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f; // 3D
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.dopplerLevel = 0f;
            pool[i] = source;
        }

        uiSource = gameObject.AddComponent<AudioSource>();
        uiSource.playOnAwake = false;
        uiSource.spatialBlend = 0f; // 2D
        uiSource.ignoreListenerPause = true;
    }

    private void Play3D(AudioClip clip, Vector3 position, float volume)
    {
        if (clip == null || muted || volume <= 0f || pool == null) return;

        AudioSource source = pool[nextVoice];
        nextVoice = (nextVoice + 1) % pool.Length; // reuse the oldest voice
        source.transform.position = position;
        source.clip = clip;
        source.volume = volume * masterVolume;
        source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        source.Play();
    }

    private void Play2D(AudioClip clip, float volume)
    {
        if (clip == null || muted || uiSource == null) return;
        uiSource.PlayOneShot(clip, volume * masterVolume);
    }
}
