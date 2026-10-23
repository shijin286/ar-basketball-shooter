using System;
using UnityEngine;

/// <summary>
/// STAGE 3 - controls ONE basketball.
///
/// A ball has three states:
///  Held     - sits in front of the camera, kinematic, collider off (it can't hit anything).
///  Flying   - shot or dropped; normal physics with gravity.
///  Finished - removed (rested on the floor, flew too far, fell below the floor or timed out).
///
/// Attach to: the root of Prefabs/Basketball (the Basketball prefab builder adds it for you).
///
/// Talks to:
///  - BallSpawner creates the ball, calls Initialize() + Hold(), and later Launch().
///  - BallSpawner listens to Bounced / Finished (Stage 7 plays bounce sounds from Bounced).
///  - HoopScoreTrigger (Stage 5) calls TryMarkScored() so each ball scores at most once.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
[DisallowMultipleComponent]
public class BasketballController : MonoBehaviour
{
    public enum BallState { Held, Flying, Finished }

    [Header("Removal after a shot")]
    [Tooltip("The ball is removed this many seconds after it was shot.")]
    [SerializeField] private float maxFlightTime = 8f;

    [Tooltip("The ball is removed when it is this many metres below the floor.")]
    [SerializeField] private float removeBelowFloor = 2f;

    [Tooltip("The ball is removed when it is further than this from the player (metres).")]
    [SerializeField] private float maxDistanceFromPlayer = 25f;

    [Tooltip("Below this speed (m/s) the ball counts as resting.")]
    [SerializeField] private float restSpeed = 0.15f;

    [Tooltip("Seconds the ball must rest before it is removed.")]
    [SerializeField] private float restTimeBeforeRemove = 1.5f;

    [Header("Physics")]
    [Tooltip("Collisions slower than this (m/s) don't raise the Bounced event.")]
    [SerializeField] private float minBounceSpeed = 0.5f;

    [Tooltip("Maximum spin in radians per second.")]
    [SerializeField] private float maxSpinSpeed = 50f;

    /// <summary>Raised when the ball is shot or dropped.</summary>
    public event Action<BasketballController> Launched;

    /// <summary>Raised when the ball hits something. Passes the impact speed and the collider it hit.</summary>
    public event Action<BasketballController, float, Collider> Bounced;

    /// <summary>Raised once, just before the ball is destroyed.</summary>
    public event Action<BasketballController> Finished;

    public BallState State { get; private set; } = BallState.Held;
    public bool HasScored { get; private set; }
    public float TimeSinceLaunch { get; private set; }

    // Components are looked up on first use so the public methods also work in Edit Mode tests.
    public Rigidbody Body => body != null ? body : (body = GetComponent<Rigidbody>());
    private SphereCollider Sphere => sphere != null ? sphere : (sphere = GetComponent<SphereCollider>());

    private Rigidbody body;
    private SphereCollider sphere;
    private Transform player;
    private float floorY = float.NegativeInfinity;
    private float restTimer;
    private float lastBounceTime = -1f;
    private bool isFinishing;

    /// <summary>Called by BallSpawner right after creating the ball.</summary>
    public void Initialize(Transform playerCamera, float floorHeight)
    {
        player = playerCamera;
        floorY = floorHeight;
    }

    /// <summary>Puts the ball in the player's "hand": it follows holdPoint and ignores physics.</summary>
    public void Hold(Transform holdPoint)
    {
        if (holdPoint == null)
        {
            Debug.LogError("[BasketballController] Hold() needs a hold point.", this);
            return;
        }

        State = BallState.Held;
        Body.isKinematic = true;
        Body.interpolation = RigidbodyInterpolation.None; // it is moved by its parent, not by physics
        Sphere.enabled = false;                           // can't knock anything over or trigger a score

        transform.SetParent(holdPoint, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }

    /// <summary>
    /// Releases the ball and pushes it with an impulse (N·s; velocity change = impulse / mass).
    /// Returns false if the ball is not being held, so one ball can only be shot once.
    /// </summary>
    public bool Launch(Vector3 impulse, Vector3 spinImpulse)
    {
        if (State != BallState.Held || isFinishing) return false;

        transform.SetParent(null, true);

        Rigidbody rb = Body;
        rb.isKinematic = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate; // smooth movement while the AR camera moves
        rb.maxAngularVelocity = maxSpinSpeed;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        Sphere.enabled = true;

        rb.AddForce(impulse, ForceMode.Impulse);
        if (spinImpulse != Vector3.zero) rb.AddTorque(spinImpulse, ForceMode.Impulse);

        State = BallState.Flying;
        TimeSinceLaunch = 0f;
        restTimer = 0f;
        Launched?.Invoke(this);
        return true;
    }

    /// <summary>
    /// Used by the scoring trigger (Stage 5). Returns true only the first time it is
    /// called on a flying ball, so a ball can never score twice.
    /// </summary>
    public bool TryMarkScored()
    {
        if (HasScored || State != BallState.Flying) return false;
        HasScored = true;
        return true;
    }

    /// <summary>Removes the ball. Finished is raised from OnDestroy.</summary>
    public void Finish()
    {
        if (isFinishing) return;
        isFinishing = true;
        Destroy(gameObject);
    }

    private void FixedUpdate()
    {
        if (State != BallState.Flying || isFinishing) return;

        TimeSinceLaunch += Time.fixedDeltaTime;
        Vector3 position = Body.position;

        bool timedOut = TimeSinceLaunch > maxFlightTime;
        bool belowFloor = position.y < floorY - removeBelowFloor;
        bool tooFar = player != null &&
                      (position - player.position).sqrMagnitude > maxDistanceFromPlayer * maxDistanceFromPlayer;
        if (timedOut || belowFloor || tooFar)
        {
            Finish();
            return;
        }

        if (Body.linearVelocity.sqrMagnitude < restSpeed * restSpeed)
        {
            restTimer += Time.fixedDeltaTime;
            if (restTimer >= restTimeBeforeRemove) Finish();
        }
        else
        {
            restTimer = 0f;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (State != BallState.Flying) return;

        float speed = collision.relativeVelocity.magnitude;
        if (speed < minBounceSpeed || Time.time - lastBounceTime < 0.05f) return;

        lastBounceTime = Time.time;
        Bounced?.Invoke(this, speed, collision.collider);
    }

    private void OnDestroy()
    {
        State = BallState.Finished;
        Finished?.Invoke(this);
    }
}
