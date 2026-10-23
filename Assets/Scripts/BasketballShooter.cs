using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// STAGE 4 - swipe up to shoot the ball in your hand.
///
/// How a swipe becomes a shot:
///  1. SwipeGesture records the finger from touch-down to release (one shot per swipe).
///  2. ShotMath.IsValidSwipe checks it went up far enough, fast enough and not too sideways.
///  3. Swipe speed -> power (0..1) -> force between Min and Max Shot Force (N·s).
///  4. Direction = where the camera faces, turned left/right by the swipe, tilted up by Launch Angle.
///  5. Optional aim assist blends the shot towards the perfect shot at the rim.
///  6. The impulse is clamped and sent to BallSpawner.LaunchHeldBall, which calls
///     BasketballController.Launch -> Rigidbody.AddForce(impulse, ForceMode.Impulse).
///
/// Attach to: GameSystems (next to ARPlanePlacement and BallSpawner).
/// GameManager (Stage 5/6) listens to ShotFired to count shots and calls SetShootingEnabled when paused.
/// </summary>
[DisallowMultipleComponent]
public class BasketballShooter : MonoBehaviour
{
    [Header("References (found automatically if empty)")]
    [SerializeField] private BallSpawner spawner;
    [SerializeField] private ARPlanePlacement placement;

    [Header("Swipe (measured in screen heights)")]
    [Tooltip("How far up the finger must move. 0.06 = 6% of the screen height.")]
    [SerializeField] private float minSwipeLength = 0.06f;

    [Tooltip("Swipes that take longer than this (seconds) are ignored.")]
    [SerializeField] private float maxSwipeDuration = 1.2f;

    [Tooltip("Swipes more sideways than this (degrees from straight up) are ignored.")]
    [SerializeField, Range(10f, 80f)] private float maxSwipeAngle = 60f;

    [Tooltip("Swipe speed (screen heights per second) that gives the weakest shot.")]
    [SerializeField] private float minSwipeSpeed = 0.6f;

    [Tooltip("Swipe speed that gives the strongest shot.")]
    [SerializeField] private float maxSwipeSpeed = 3.0f;

    [Tooltip("Minimum seconds between two shots.")]
    [SerializeField] private float shotCooldown = 0.35f;

    [Header("Shot")]
    [Tooltip("Weakest shot impulse in N·s (velocity = impulse / mass: 1.5 N·s on 0.6 kg = 2.5 m/s).")]
    [SerializeField] private float minShotForce = 1.5f;

    [Tooltip("Strongest shot impulse in N·s (5.4 N·s on 0.6 kg = 9 m/s).")]
    [SerializeField] private float maxShotForce = 5.4f;

    [Tooltip("Angle above horizontal. About 55 degrees gives a nice basketball arc.")]
    [SerializeField, Range(30f, 75f)] private float launchAngle = 55f;

    [Tooltip("How much the swipe's sideways angle turns the shot (1 = same angle).")]
    [SerializeField, Range(0f, 1f)] private float aimSensitivity = 0.5f;

    [Tooltip("Largest left/right turn in degrees.")]
    [SerializeField] private float maxAimAngle = 20f;

    [Tooltip("Backspin in radians per second (about 2.4 turns per second). 0 = no spin.")]
    [SerializeField] private float backspin = 15f;

    [Header("Aim assist")]
    [Tooltip("0 = pure skill, 1 = every valid swipe towards the hoop scores. 0.6 is a friendly middle.")]
    [SerializeField, Range(0f, 1f)] private float aimAssist = 0.6f;

    [Tooltip("Assist only helps when the shot already goes within this many degrees of the hoop.")]
    [SerializeField] private float assistMaxAngle = 30f;

    [Tooltip("Aim this far above the rim centre (metres) so the ball drops in from above.")]
    [SerializeField] private float rimTargetHeight = 0.05f;

    [Header("UI (optional)")]
    [Tooltip("A TextMeshPro text for hints such as 'Swipe up to shoot'.")]
    [SerializeField] private TMP_Text hintText;

    /// <summary>Raised after every shot. Passes the ball and the swipe power (0..1).</summary>
    public event Action<BasketballController, float> ShotFired;

    public int ShotsFired { get; private set; }
    public bool ShootingEnabled { get; private set; } = true;

    /// <summary>Laptop version (Stage 10): hints say "Drag" (the mouse) instead of "Swipe".</summary>
    public bool MouseWording { get; set; }
    private string Verb => MouseWording ? "Drag" : "Swipe";
    public float AimAssist
    {
        get => aimAssist;
        set => aimAssist = Mathf.Clamp01(value);
    }

    private readonly SwipeGesture gesture = new SwipeGesture();
    private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
    private Transform rimTarget;
    private Camera arCamera;
    private float nextShotTime;
    private int hoopPlacedFrame = -1;

    private void Awake()
    {
        if (spawner == null) spawner = FindFirstObjectByType<BallSpawner>();
        if (placement == null) placement = FindFirstObjectByType<ARPlanePlacement>();

        if (spawner == null || placement == null)
        {
            Debug.LogError("[BasketballShooter] BallSpawner or ARPlanePlacement not found in the scene.", this);
            enabled = false;
            return;
        }

        if (minShotForce <= 0f || maxShotForce < minShotForce)
        {
            Debug.LogWarning("[BasketballShooter] Shot forces are invalid - using 1.5 and 5.4 N·s.", this);
            minShotForce = 1.5f;
            maxShotForce = 5.4f;
        }
    }

    private void OnEnable()
    {
        if (spawner == null || placement == null) return;
        placement.HoopPlaced += OnHoopPlaced;
        placement.HoopRemoved += OnHoopRemoved;
        spawner.BallSpawned += OnBallSpawned;
        if (placement.IsHoopPlaced) CacheRim(placement.HoopTransform);
    }

    private void OnDisable()
    {
        gesture.Cancel();
        if (spawner == null || placement == null) return;
        placement.HoopPlaced -= OnHoopPlaced;
        placement.HoopRemoved -= OnHoopRemoved;
        spawner.BallSpawned -= OnBallSpawned;
    }

    private void Update()
    {
        if (!ShootingEnabled || !placement.IsHoopPlaced)
        {
            gesture.Cancel();
            return;
        }

        Pointer pointer = Pointer.current;
        if (pointer == null) return;

        Vector2 pixels = pointer.position.ReadValue();
        Vector2 position = pixels / Mathf.Max(1, Screen.height); // screen heights
        float now = Time.unscaledTime;

        if (pointer.press.wasPressedThisFrame)
        {
            // Ignore the tap that placed the hoop, and touches that start on a button.
            if (Time.frameCount == hoopPlacedFrame || IsPointerOverButton(pixels)) gesture.Cancel();
            else gesture.Begin(position, now);
        }
        else if (pointer.press.isPressed && gesture.IsActive)
        {
            gesture.Move(position, now);
            if (position.y - gesture.StartPosition.y > minSwipeLength && spawner.HeldBall != null) SetHint("Release to shoot!");
        }

        if (pointer.press.wasReleasedThisFrame && gesture.End(position, now))
        {
            HandleSwipe(gesture.LastDelta, gesture.LastDuration, gesture.LastSpeed);
        }
    }

    // ---------------------------------------------------------------- public API

    /// <summary>Lets the GameManager turn shooting off (start screen, pause, game over).</summary>
    public void SetShootingEnabled(bool value)
    {
        ShootingEnabled = value;
        if (!value) gesture.Cancel();
    }

    /// <summary>Sets the shot counter back to zero (Restart Game).</summary>
    public void ResetShotCount()
    {
        ShotsFired = 0;
    }

    // ---------------------------------------------------------------- shooting

    private void HandleSwipe(Vector2 delta, float duration, float speed)
    {
        BasketballController ball = spawner.HeldBall;
        if (ball == null || Time.unscaledTime < nextShotTime) return;

        if (!ShotMath.IsValidSwipe(delta, duration, minSwipeLength, maxSwipeDuration, maxSwipeAngle))
        {
            SetHint(delta.y <= 0f ? $"{Verb} UP to shoot" : $"{Verb} up a bit longer and quicker");
            return;
        }

        float power = ShotMath.SwipeSpeedToPower(speed, minSwipeSpeed, maxSwipeSpeed);
        Vector3 impulse = CalculateImpulse(ball, delta, power);
        Vector3 spin = ShotMath.BackspinImpulse(impulse, ball.Body.mass, GetBallRadius(ball), backspin);

        if (!spawner.LaunchHeldBall(impulse, spin)) return;

        nextShotTime = Time.unscaledTime + shotCooldown;
        ShotsFired++;
        SetHint($"Shot! Power {Mathf.RoundToInt(power * 100f)}%");
        ShotFired?.Invoke(ball, power);
    }

    private Vector3 CalculateImpulse(BasketballController ball, Vector2 swipeDelta, float power)
    {
        Camera cam = arCamera != null ? arCamera : (arCamera = spawner.ARCamera);
        Transform view = cam != null ? cam.transform : ball.transform;
        float mass = Mathf.Max(0.01f, ball.Body.mass);

        Vector3 direction = ShotMath.ShotDirection(view.forward, view.up, swipeDelta, aimSensitivity, maxAimAngle, launchAngle);
        float force = Mathf.Lerp(minShotForce, maxShotForce, power);
        Vector3 velocity = direction * (force / mass);

        if (aimAssist > 0f && rimTarget != null &&
            ShotMath.TryGetIdealVelocity(ball.transform.position, rimTarget.position + Vector3.up * rimTargetHeight,
                launchAngle, ball.Body.linearDamping, out Vector3 ideal))
        {
            Vector3 flatShot = Vector3.ProjectOnPlane(velocity, Vector3.up);
            Vector3 flatIdeal = Vector3.ProjectOnPlane(ideal, Vector3.up);
            if (Vector3.Angle(flatShot, flatIdeal) <= assistMaxAngle)
            {
                velocity = Vector3.Lerp(velocity, ideal, aimAssist);
            }
        }

        return ShotMath.ClampImpulse(velocity * mass, minShotForce, maxShotForce);
    }

    private static float GetBallRadius(BasketballController ball)
    {
        SphereCollider sphere = ball.GetComponent<SphereCollider>();
        if (sphere == null) return 0.12f;
        Vector3 s = ball.transform.lossyScale;
        return sphere.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
    }

    // ---------------------------------------------------------------- events & helpers

    private void OnHoopPlaced(Transform hoop)
    {
        hoopPlacedFrame = Time.frameCount;
        gesture.Cancel();
        CacheRim(hoop);
    }

    private void OnHoopRemoved()
    {
        gesture.Cancel();
        rimTarget = null;
        SetHint("");
    }

    private void OnBallSpawned(BasketballController ball)
    {
        SetHint($"{Verb} up to shoot");
    }

    private void CacheRim(Transform hoop)
    {
        rimTarget = hoop != null ? hoop.Find("Rim") : null;
        if (hoop != null && rimTarget == null)
        {
            Debug.LogWarning("[BasketballShooter] The hoop has no child called 'Rim' - aim assist is off.", this);
        }
    }

    /// <summary>True if the screen position is over a UI button, so touches on buttons never shoot.</summary>
    private bool IsPointerOverButton(Vector2 screenPosition)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) return false;

        PointerEventData data = new PointerEventData(eventSystem) { position = screenPosition };
        uiHits.Clear();
        eventSystem.RaycastAll(data, uiHits);

        foreach (RaycastResult result in uiHits)
        {
            if (result.gameObject != null && result.gameObject.GetComponentInParent<Selectable>() != null) return true;
        }
        return false;
    }

    private void SetHint(string message)
    {
        if (hintText != null && hintText.text != message) hintText.text = message;
    }
}
