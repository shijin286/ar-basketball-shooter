using UnityEngine;

/// <summary>
/// STAGE 4 - the maths behind a shot. No MonoBehaviour, no state: every
/// method takes numbers in and gives numbers back, so it is easy to test.
///
/// Used by BasketballShooter. Units: metres, seconds, newton-seconds (N·s)
/// for impulses. Swipe values are measured in "screen heights" so the
/// controls feel the same on every phone size.
/// </summary>
public static class ShotMath
{
    /// <summary>
    /// A swipe counts as a shot if it goes up far enough, isn't too slow, and
    /// isn't too sideways (angle measured from straight up).
    /// </summary>
    public static bool IsValidSwipe(Vector2 delta, float duration, float minLength, float maxDuration, float maxAngleFromVertical)
    {
        if (delta.y < minLength || duration <= 0f || duration > maxDuration) return false;
        float angle = Mathf.Atan2(Mathf.Abs(delta.x), delta.y) * Mathf.Rad2Deg;
        return angle <= maxAngleFromVertical;
    }

    /// <summary>Turns swipe speed (screen heights per second) into power from 0 to 1.</summary>
    public static float SwipeSpeedToPower(float swipeSpeed, float minSpeed, float maxSpeed)
    {
        return Mathf.InverseLerp(minSpeed, maxSpeed, swipeSpeed);
    }

    /// <summary>
    /// Shot direction: the camera's flat forward direction, turned left/right by
    /// the swipe's sideways angle, then tilted up by the launch angle.
    /// </summary>
    public static Vector3 ShotDirection(Vector3 cameraForward, Vector3 cameraUp, Vector2 swipeDelta,
        float aimSensitivity, float maxAimAngle, float launchAngle)
    {
        Vector3 flatForward = Vector3.ProjectOnPlane(cameraForward, Vector3.up);
        if (flatForward.sqrMagnitude < 0.0001f) flatForward = Vector3.ProjectOnPlane(cameraUp, Vector3.up); // phone pointing at the floor
        if (flatForward.sqrMagnitude < 0.0001f) flatForward = Vector3.forward;
        flatForward.Normalize();

        float swipeAngle = Mathf.Atan2(swipeDelta.x, Mathf.Max(swipeDelta.y, 0.0001f)) * Mathf.Rad2Deg;
        float yaw = Mathf.Clamp(swipeAngle * aimSensitivity, -maxAimAngle, maxAimAngle);
        Vector3 horizontal = Quaternion.AngleAxis(yaw, Vector3.up) * flatForward; // positive yaw = to the right

        float pitch = launchAngle * Mathf.Deg2Rad;
        return (horizontal * Mathf.Cos(pitch) + Vector3.up * Mathf.Sin(pitch)).normalized;
    }

    /// <summary>Keeps the impulse strength between min and max without changing its direction.</summary>
    public static Vector3 ClampImpulse(Vector3 impulse, float min, float max)
    {
        float magnitude = impulse.magnitude;
        if (magnitude < 0.00001f) return impulse;
        return impulse * (Mathf.Clamp(magnitude, min, max) / magnitude);
    }

    /// <summary>The ball should drop into the rim at least this steeply (degrees below horizontal).</summary>
    public const float MinEntryAngle = 45f;

    /// <summary>Aim assist never throws steeper than this (degrees above horizontal).</summary>
    public const float MaxLaunchAngle = 75f;

    /// <summary>
    /// The start velocity that lands the ball on target. It starts at launchAngle and, when
    /// that arc would come down too flat (below MinEntryAngle - a flat ball clips the front of
    /// the rim, e.g. when the phone is held low and close), steepens the throw up to
    /// MaxLaunchAngle. It also corrects for the Rigidbody's linear damping (air drag).
    /// Returns false when the target can't be reached (too close or too high).
    /// </summary>
    public static bool TryGetIdealVelocity(Vector3 start, Vector3 target, float launchAngle, float damping, out Vector3 velocity)
    {
        velocity = Vector3.zero;

        Vector3 flat = target - start;
        float height = flat.y;
        flat.y = 0f;
        float distance = flat.magnitude;
        float g = -Physics.gravity.y;
        if (distance < 0.3f || g <= 0f) return false;

        for (float angle = launchAngle; angle <= MaxLaunchAngle + 0.01f; angle += 2.5f)
        {
            // Classic projectile formula (no drag): v² = g d² / (2 cos²θ (d tanθ - h))
            float radians = angle * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float denominator = 2f * cos * cos * (distance * Mathf.Tan(radians) - height);
            if (denominator <= 0.001f) continue;

            float speed = Mathf.Sqrt(g * distance * distance / denominator);
            float flightTime = distance / (speed * cos);
            float verticalAtTarget = speed * Mathf.Sin(radians) - g * flightTime;
            float entryAngle = Mathf.Atan2(-verticalAtTarget, speed * cos) * Mathf.Rad2Deg;

            bool lastTry = angle + 2.5f > MaxLaunchAngle + 0.01f;
            if (entryAngle >= MinEntryAngle || lastTry)
            {
                velocity = VelocityForFlightTime(start, target, flightTime, Physics.gravity, damping);
                return true;
            }
        }
        return false;
    }

    /// <summary>Start velocity that reaches target after 'time' seconds with gravity and linear damping k.</summary>
    public static Vector3 VelocityForFlightTime(Vector3 start, Vector3 target, float time, Vector3 gravity, float damping)
    {
        Vector3 delta = target - start;
        if (time <= 0f) return Vector3.zero;
        if (damping < 0.0001f) return (delta - 0.5f * time * time * gravity) / time;

        // With drag: x(t) = g t / k + (v0 - g / k)(1 - e^(-k t)) / k, solved for v0.
        float e = 1f - Mathf.Exp(-damping * time);
        return gravity / damping + (delta - gravity * time / damping) * damping / e;
    }

    /// <summary>Angular impulse (N·m·s) for backspin of 'spinSpeed' rad/s on a solid ball moving along 'direction'.</summary>
    public static Vector3 BackspinImpulse(Vector3 direction, float mass, float radius, float spinSpeed)
    {
        Vector3 flat = Vector3.ProjectOnPlane(direction, Vector3.up);
        if (flat.sqrMagnitude < 0.0001f || spinSpeed <= 0f) return Vector3.zero;

        Vector3 right = Vector3.Cross(Vector3.up, flat.normalized);
        float inertia = 0.4f * mass * radius * radius; // solid sphere: I = 2/5 m r²
        return -right * (inertia * spinSpeed);         // negative around "right" = top spins back towards the player
    }
}
