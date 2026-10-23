using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// STAGE 4 - remembers ONE finger swipe from touch-down to release.
///
/// Begin() on touch-down, Move() while dragging, End() on release.
/// End() returns true only once per Begin(), so one swipe can never fire
/// two shots. Cancel() throws the swipe away (e.g. finger started on a button).
///
/// Positions are in "screen heights" (pixels / Screen.height), so the same
/// swipe feels the same on a small and a large phone.
///
/// Plain C# class (not a MonoBehaviour): BasketballShooter creates it with "new".
/// </summary>
public class SwipeGesture
{
    private readonly List<Vector2> positions = new List<Vector2>();
    private readonly List<float> times = new List<float>();
    private Vector2 startPosition;
    private float startTime;

    /// <summary>The release speed is measured over this last part of the swipe (seconds).</summary>
    public float VelocityWindow { get; set; } = 0.1f;

    public bool IsActive { get; private set; }
    public Vector2 StartPosition => startPosition;

    /// <summary>End position minus start position of the last finished swipe.</summary>
    public Vector2 LastDelta { get; private set; }

    /// <summary>Seconds from touch-down to release of the last finished swipe.</summary>
    public float LastDuration { get; private set; }

    /// <summary>Speed in screen heights per second: the faster of the average speed and the final flick speed.</summary>
    public float LastSpeed { get; private set; }

    public void Begin(Vector2 position, float time)
    {
        IsActive = true;
        startPosition = position;
        startTime = time;
        positions.Clear();
        times.Clear();
        AddSample(position, time);
    }

    public void Move(Vector2 position, float time)
    {
        if (!IsActive) return;
        AddSample(position, time);
    }

    /// <summary>Finishes the swipe. Returns false if there was no active swipe (so it can't fire twice).</summary>
    public bool End(Vector2 position, float time)
    {
        if (!IsActive) return false;
        IsActive = false;
        AddSample(position, time);

        LastDelta = position - startPosition;
        LastDuration = Mathf.Max(0f, time - startTime);

        float average = LastDuration > 0.0001f ? LastDelta.magnitude / LastDuration : 0f;
        LastSpeed = Mathf.Max(average, ReleaseSpeed(position, time));
        return true;
    }

    public void Cancel()
    {
        IsActive = false;
    }

    private void AddSample(Vector2 position, float time)
    {
        positions.Add(position);
        times.Add(time);

        // Keep only what the velocity window needs: the newest samples plus one older sample.
        while (times.Count > 2 && times[1] < time - VelocityWindow)
        {
            positions.RemoveAt(0);
            times.RemoveAt(0);
        }
    }

    private float ReleaseSpeed(Vector2 end, float time)
    {
        if (times.Count < 2) return 0f;

        int first = 0;
        for (int i = times.Count - 1; i >= 0; i--)
        {
            if (times[i] <= time - VelocityWindow)
            {
                first = i;
                break;
            }
        }

        float dt = time - times[first];
        return dt > 0.001f ? (end - positions[first]).magnitude / dt : 0f;
    }
}
