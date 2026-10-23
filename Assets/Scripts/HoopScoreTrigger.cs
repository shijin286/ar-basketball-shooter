using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// STAGE 5 - detects a real basket.
///
/// Sits on the "ScoreZone" object: a narrow trigger box inside the rim that
/// reaches from just above the rim down into the net. A basket counts only when a ball:
///  1. ENTERS the zone moving DOWN with its centre ABOVE the rim, and
///  2. LEAVES the zone with its centre BELOW the rim, still moving down, within Max Pass Time, and
///  3. has never scored before (BasketballController.TryMarkScored).
///
/// The zone sits inside the ring, so a ball that is below the rim and inside the zone
/// has gone through the hoop - whether it leaves through the bottom or the side of the box.
///
/// So these never score:
///  - touching the rim or backboard (those colliders are solid, not the trigger);
///  - bouncing around on the rim and popping out of the top of the zone;
///  - a ball that came UP through the net from below (it is remembered and blocked);
///  - the same ball a second time.
///
/// Attach to: BasketballHoop/Rim/ScoreZone (the Hoop prefab builder adds it for you).
/// GameManager listens to BasketScored.
/// </summary>
[RequireComponent(typeof(Collider))]
[DisallowMultipleComponent]
public class HoopScoreTrigger : MonoBehaviour
{
    [Tooltip("The ball centre must leave the zone at least this far below the rim (metres).")]
    [SerializeField] private float exitBelowRim = 0.02f;

    [Tooltip("A ball must pass through within this many seconds after entering.")]
    [SerializeField] private float maxPassTime = 2f;

    /// <summary>Raised once for every valid basket.</summary>
    public event Action<BasketballController> BasketScored;

    /// <summary>Number of baskets this hoop has counted (handy for tests and debugging).</summary>
    public int BasketsCount { get; private set; }

    private readonly Dictionary<BasketballController, float> entriesFromAbove = new Dictionary<BasketballController, float>();
    private readonly HashSet<BasketballController> enteredFromBelow = new HashSet<BasketballController>();
    private readonly List<BasketballController> destroyedBalls = new List<BasketballController>();

    private void Awake()
    {
        Collider zone = GetComponent<Collider>();
        if (!zone.isTrigger)
        {
            Debug.LogWarning("[HoopScoreTrigger] The ScoreZone collider must be a trigger - fixing it now.", this);
            zone.isTrigger = true;
        }
    }

    private void OnDisable()
    {
        // The hoop is hidden while repositioning; forget half-finished passes.
        entriesFromAbove.Clear();
        enteredFromBelow.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        BasketballController ball = GetBall(other);
        if (ball == null || ball.State != BasketballController.BallState.Flying || ball.HasScored) return;

        RemoveDestroyedBalls();

        float height = LocalHeight(ball);
        float verticalSpeed = LocalVerticalSpeed(ball);

        if (verticalSpeed > 0f && height < 0f)
        {
            // Came up through the net: this ball may never score.
            enteredFromBelow.Add(ball);
            entriesFromAbove.Remove(ball);
            return;
        }

        // Only balls coming down from above the rim can start a basket. (A ball entering
        // below the rim from the side would have come through the net, which has no collider.)
        if (verticalSpeed <= 0f && height > 0f && !enteredFromBelow.Contains(ball))
        {
            entriesFromAbove[ball] = Time.time;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        BasketballController ball = GetBall(other);
        if (ball == null || !entriesFromAbove.TryGetValue(ball, out float entryTime)) return;

        entriesFromAbove.Remove(ball);

        bool leftBelowRim = LocalHeight(ball) < -exitBelowRim && LocalVerticalSpeed(ball) < 0f;
        bool quickEnough = Time.time - entryTime <= maxPassTime;

        if (leftBelowRim && quickEnough && ball.TryMarkScored())
        {
            BasketsCount++;
            BasketScored?.Invoke(ball);
        }
    }

    /// <summary>Called by GameManager when the game restarts.</summary>
    public void ResetCount()
    {
        BasketsCount = 0;
        entriesFromAbove.Clear();
        enteredFromBelow.Clear();
    }

    // ---------------------------------------------------------------- helpers

    private static BasketballController GetBall(Collider other)
    {
        Rigidbody body = other.attachedRigidbody;
        return body != null ? body.GetComponent<BasketballController>() : null;
    }

    /// <summary>Height of the ball centre above (+) or below (-) the rim, in metres.</summary>
    private float LocalHeight(BasketballController ball)
    {
        return transform.InverseTransformPoint(ball.Body.position).y;
    }

    /// <summary>Up (+) or down (-) speed of the ball relative to the hoop.</summary>
    private float LocalVerticalSpeed(BasketballController ball)
    {
        return transform.InverseTransformDirection(ball.Body.linearVelocity).y;
    }

    private void RemoveDestroyedBalls()
    {
        destroyedBalls.Clear();
        foreach (BasketballController ball in entriesFromAbove.Keys)
        {
            if (ball == null) destroyedBalls.Add(ball);
        }
        foreach (BasketballController ball in destroyedBalls) entriesFromAbove.Remove(ball);
        enteredFromBelow.RemoveWhere(ball => ball == null);
    }
}
