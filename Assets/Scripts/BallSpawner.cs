using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// STAGE 3 - gives the player a basketball.
///
/// What it does:
///  1. When ARPlanePlacement places the hoop, spawns a ball at a fixed spot in
///     front of the camera (the "hand") and adds an invisible floor collider
///     around the hoop so balls bounce even outside the detected AR plane.
///  2. LaunchHeldBall() releases the ball (Stage 4's BasketballShooter calls it),
///     and a new ball appears in the hand after a short delay.
///  3. ResetBall() (Reset Ball button) removes all balls and gives a fresh one.
///  4. When the hoop is repositioned, all balls are removed.
///
/// Attach to: the GameSystems object (next to ARPlanePlacement).
/// </summary>
[DisallowMultipleComponent]
public class BallSpawner : MonoBehaviour
{
    [Header("References")]
    [Tooltip("ARPlanePlacement on GameSystems. Found automatically if empty.")]
    [SerializeField] private ARPlanePlacement placement;

    [Tooltip("The AR camera (XR Origin > Camera Offset > Main Camera). Found automatically if empty.")]
    [SerializeField] private Camera arCamera;

    [Tooltip("Prefabs/Basketball (must have a BasketballController).")]
    [SerializeField] private BasketballController ballPrefab;

    [Header("Ball in hand (metres, relative to the camera)")]
    [Tooltip("x = right, y = up, z = forward. The default shows the ball at the bottom of the screen.")]
    [SerializeField] private Vector3 holdOffset = new Vector3(0f, -0.22f, 0.7f);

    [Header("Spawning")]
    [Tooltip("Give the player a ball as soon as the hoop is placed. The GameManager will control this later.")]
    [SerializeField] private bool spawnWhenHoopPlaced = true;

    [Tooltip("Seconds after a shot before the next ball appears in the hand.")]
    [SerializeField] private float respawnDelay = 0.75f;

    [Tooltip("Older flying balls are removed when there would be more than this.")]
    [SerializeField, Min(1)] private int maxBallsInScene = 4;

    [Header("Virtual floor")]
    [Tooltip("Adds a large invisible floor collider at the hoop's base so balls never fall through the real floor.")]
    [SerializeField] private bool createVirtualFloor = true;

    [SerializeField] private float virtualFloorSize = 20f;

    [Tooltip("Materials/Physics/Floor_Physics")]
    [SerializeField] private PhysicsMaterial floorMaterial;

    [Header("UI (optional)")]
    [Tooltip("Objects shown only while the hoop is placed, e.g. Reset Ball button.")]
    [SerializeField] private GameObject[] showWhenHoopPlaced;

    public event Action<BasketballController> BallSpawned;
    public event Action<BasketballController> BallLaunched;
    public event Action<BasketballController, float, Collider> BallBounced;

    public BasketballController HeldBall { get; private set; }
    public bool SpawningEnabled { get; private set; } = true;
    public IReadOnlyList<BasketballController> ActiveBalls => activeBalls;
    public Camera ARCamera => arCamera;

    private const string VirtualFloorName = "VirtualFloor";

    private readonly List<BasketballController> activeBalls = new List<BasketballController>();
    private Transform holdPoint;
    private Coroutine respawnRoutine;
    private float floorY;

    private void Awake()
    {
        if (placement == null) placement = FindFirstObjectByType<ARPlanePlacement>();
        if (arCamera == null) arCamera = Camera.main;

        if (placement == null || arCamera == null)
        {
            Debug.LogError("[BallSpawner] ARPlanePlacement or AR Camera not found.", this);
            enabled = false;
            return;
        }

        if (ballPrefab == null)
        {
            Debug.LogError("[BallSpawner] Ball Prefab is not assigned in the Inspector.", this);
            enabled = false;
            return;
        }

        if (createVirtualFloor && floorMaterial == null)
        {
            Debug.LogWarning("[BallSpawner] Floor Material is not assigned - floor bounces will be dead.", this);
        }

        holdPoint = new GameObject("BallHoldPoint").transform;
        holdPoint.SetParent(arCamera.transform, false);
        holdPoint.localPosition = holdOffset;

        SetHoopUiVisible(false);
    }

    private void OnEnable()
    {
        if (placement == null) return;
        placement.HoopPlaced += OnHoopPlaced;
        placement.HoopRemoved += OnHoopRemoved;
        if (placement.IsHoopPlaced) OnHoopPlaced(placement.HoopTransform);
    }

    private void OnDisable()
    {
        if (placement == null) return;
        placement.HoopPlaced -= OnHoopPlaced;
        placement.HoopRemoved -= OnHoopRemoved;
        CancelRespawn();
    }

    // ---------------------------------------------------------------- public API

    /// <summary>Puts a new ball in the player's hand (if there isn't one already). Returns the held ball or null.</summary>
    public BasketballController SpawnBallInHand()
    {
        if (!isActiveAndEnabled || !SpawningEnabled || placement == null || !placement.IsHoopPlaced) return null;
        if (HeldBall != null) return HeldBall;

        CancelRespawn();
        RemoveOldestBallsIfNeeded();

        BasketballController ball = Instantiate(ballPrefab, holdPoint.position, holdPoint.rotation);
        ball.name = "Basketball";
        ball.Initialize(arCamera.transform, floorY);
        ball.Hold(holdPoint);
        ball.Bounced += HandleBallBounced;
        ball.Finished += HandleBallFinished;

        activeBalls.Add(ball);
        HeldBall = ball;
        BallSpawned?.Invoke(ball);
        return ball;
    }

    /// <summary>Shoots the held ball. Stage 4's BasketballShooter calls this. Returns false if there is no ball in hand.</summary>
    public bool LaunchHeldBall(Vector3 impulse, Vector3 spinImpulse)
    {
        BasketballController ball = HeldBall;
        if (ball == null || !ball.Launch(impulse, spinImpulse)) return false;

        HeldBall = null;
        BallLaunched?.Invoke(ball);
        ScheduleRespawn();
        return true;
    }

    /// <summary>Stage 3 test button: lets the held ball fall gently forward onto the floor.</summary>
    public void TestDropBall()
    {
        if (arCamera == null) return;
        Vector3 forward = Vector3.ProjectOnPlane(arCamera.transform.forward, Vector3.up).normalized;
        LaunchHeldBall(forward * 0.4f, Vector3.zero);
    }

    /// <summary>Reset Ball button: removes every ball and gives the player a fresh one.</summary>
    public void ResetBall()
    {
        ClearAllBalls();
        SpawnBallInHand();
    }

    /// <summary>Removes every ball (held and flying).</summary>
    public void ClearAllBalls()
    {
        CancelRespawn();
        foreach (BasketballController ball in activeBalls.ToArray())
        {
            if (ball == null) continue;
            Unsubscribe(ball);
            Destroy(ball.gameObject);
        }
        activeBalls.Clear();
        HeldBall = null;
    }

    /// <summary>
    /// Stage 9: removes only the ball in the player's hand (when the challenge time runs out).
    /// Balls already flying keep going, so a shot released before the buzzer can still score.
    /// </summary>
    public void DiscardHeldBall()
    {
        CancelRespawn();
        BasketballController ball = HeldBall;
        if (ball == null) return;

        HeldBall = null;
        Unsubscribe(ball);
        activeBalls.Remove(ball);
        Destroy(ball.gameObject);
    }

    /// <summary>Lets the GameManager stop new balls from appearing (for example on the start screen).</summary>
    public void SetSpawningEnabled(bool value)
    {
        SpawningEnabled = value;
        if (!value) CancelRespawn();
    }

    // ---------------------------------------------------------------- hoop events

    private void OnHoopPlaced(Transform hoop)
    {
        if (hoop == null) return;

        floorY = hoop.position.y;
        if (createVirtualFloor) EnsureVirtualFloor(hoop);
        SetHoopUiVisible(true);
        if (spawnWhenHoopPlaced) SpawnBallInHand();
    }

    private void OnHoopRemoved()
    {
        ClearAllBalls();
        SetHoopUiVisible(false);
    }

    /// <summary>
    /// The floor is a child of the hoop, so it moves with the hoop's AR anchor
    /// and is hidden together with the hoop while repositioning.
    /// </summary>
    private void EnsureVirtualFloor(Transform hoop)
    {
        if (hoop.Find(VirtualFloorName) != null) return;

        GameObject floor = new GameObject(VirtualFloorName);
        floor.layer = hoop.gameObject.layer;
        floor.transform.SetParent(hoop, false);
        floor.transform.localPosition = new Vector3(0f, -0.1f, 0f); // top surface at the hoop's base (y = 0)

        BoxCollider box = floor.AddComponent<BoxCollider>();
        box.size = new Vector3(virtualFloorSize, 0.2f, virtualFloorSize);
        box.sharedMaterial = floorMaterial;
    }

    // ---------------------------------------------------------------- ball events

    private void HandleBallBounced(BasketballController ball, float speed, Collider other)
    {
        BallBounced?.Invoke(ball, speed, other);
    }

    private void HandleBallFinished(BasketballController ball)
    {
        Unsubscribe(ball);
        activeBalls.Remove(ball);
        if (ball == HeldBall)
        {
            HeldBall = null;
            ScheduleRespawn();
        }
    }

    private void Unsubscribe(BasketballController ball)
    {
        ball.Bounced -= HandleBallBounced;
        ball.Finished -= HandleBallFinished;
    }

    // ---------------------------------------------------------------- helpers

    private void RemoveOldestBallsIfNeeded()
    {
        activeBalls.RemoveAll(b => b == null);
        while (activeBalls.Count >= maxBallsInScene)
        {
            BasketballController oldest = activeBalls.Find(b => b != HeldBall);
            if (oldest == null) break;
            Unsubscribe(oldest);
            activeBalls.Remove(oldest);
            Destroy(oldest.gameObject);
        }
    }

    private void ScheduleRespawn()
    {
        CancelRespawn();
        if (isActiveAndEnabled) respawnRoutine = StartCoroutine(RespawnAfterDelay());
    }

    private void CancelRespawn()
    {
        if (respawnRoutine == null) return;
        StopCoroutine(respawnRoutine);
        respawnRoutine = null;
    }

    private IEnumerator RespawnAfterDelay()
    {
        // WaitForSeconds uses scaled time, so pausing the game (timeScale = 0) also pauses respawning.
        yield return new WaitForSeconds(respawnDelay);
        respawnRoutine = null;
        SpawnBallInHand();
    }

    private void SetHoopUiVisible(bool visible)
    {
        if (showWhenHoopPlaced == null) return;
        foreach (GameObject go in showWhenHoopPlaced)
        {
            if (go != null) go.SetActive(visible);
        }
    }
}
