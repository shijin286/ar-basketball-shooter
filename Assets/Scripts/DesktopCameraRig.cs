using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// STAGE 10 - the player's eyes in the laptop version (no AR, so no phone tracking).
///
/// Controls:
///  - W A S D or the arrow keys walk around the court
///  - hold the RIGHT mouse button and drag to look around
///  - the LEFT mouse button is left free for the game: click to place, drag up to shoot
///
/// The camera stays at eye height inside the court walls. Walking stops while the game is
/// paused and while a text box (the leaderboard name) is being typed in.
/// LookAt() turns it smoothly towards a point - DesktopMode uses it to face the hoop once it is placed.
///
/// Attach to: the Main Camera. DesktopMode adds it automatically on a laptop build.
/// </summary>
[DisallowMultipleComponent]
public class DesktopCameraRig : MonoBehaviour
{
    [Tooltip("Walking speed in metres per second.")]
    [SerializeField] private float moveSpeed = 2.5f;

    [Tooltip("Degrees turned per pixel of mouse movement.")]
    [SerializeField] private float lookSpeed = 0.2f;

    [Tooltip("Highest look-up angle (negative = up).")]
    [SerializeField] private float minPitch = -35f;

    [Tooltip("Lowest look-down angle.")]
    [SerializeField] private float maxPitch = 75f;

    [Tooltip("Camera height above the floor (metres), like a phone held in front of you.")]
    [SerializeField] private float eyeHeight = 1.5f;

    [Tooltip("The camera stays this far (metres) from the court centre in X and Z.")]
    [SerializeField] private float roomHalfSize = 6.5f;

    private float yaw;
    private float pitch;

    // Smooth automatic turn (LookAt), e.g. towards the hoop just after it is placed.
    private Vector2 turnFrom;
    private Vector2 turnTo;
    private float turnProgress = 1f;
    private float turnSeconds;

    /// <summary>Sets where the player stands and looks. Yaw: 0 = +Z. Pitch: positive = looking down.</summary>
    public void Place(Vector3 position, float yawDegrees, float pitchDegrees, float roomHalf)
    {
        roomHalfSize = roomHalf;
        yaw = yawDegrees;
        pitch = ClampPitch(pitchDegrees, minPitch, maxPitch);
        position = ClampToRoom(position, roomHalfSize);
        position.y = eyeHeight;
        transform.SetPositionAndRotation(position, Quaternion.Euler(pitch, yaw, 0f));
    }

    /// <summary>Turns smoothly to look at a point (the player can take over with the mouse at any time).</summary>
    public void LookAt(Vector3 point, float seconds)
    {
        Vector2 target = YawPitchTowards(transform.position, point);
        turnFrom = new Vector2(yaw, pitch);
        turnTo = new Vector2(yaw + Mathf.DeltaAngle(yaw, target.x), ClampPitch(target.y, minPitch, maxPitch));
        turnSeconds = Mathf.Max(0.01f, seconds);
        turnProgress = 0f;
    }

    private void Update()
    {
        if (Time.timeScale <= 0f) return; // paused

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.rightButton.isPressed)
        {
            turnProgress = 1f; // the player's own look wins over an automatic turn
            Vector2 delta = mouse.delta.ReadValue();
            yaw += delta.x * lookSpeed;
            pitch = ClampPitch(pitch - delta.y * lookSpeed, minPitch, maxPitch);
        }
        else if (turnProgress < 1f)
        {
            turnProgress = Mathf.Min(1f, turnProgress + Time.deltaTime / turnSeconds);
            Vector2 now = Vector2.Lerp(turnFrom, turnTo, Mathf.SmoothStep(0f, 1f, turnProgress));
            yaw = now.x;
            pitch = now.y;
        }

        Vector3 position = transform.position;
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && !IsTyping())
        {
            Vector2 input = ReadMoveKeys(keyboard);
            position += MoveDirection(yaw, input) * (moveSpeed * Time.deltaTime);
        }

        position = ClampToRoom(position, roomHalfSize);
        position.y = eyeHeight;
        transform.SetPositionAndRotation(position, Quaternion.Euler(pitch, yaw, 0f));
    }

    // ---------------------------------------------------------------- maths (public for the Stage 10 tests)

    /// <summary>Flat walking direction for the keys pressed (x = right, y = forward), turned by the look direction. Length 0..1.</summary>
    public static Vector3 MoveDirection(float yawDegrees, Vector2 input)
    {
        Vector3 local = Vector3.ClampMagnitude(new Vector3(input.x, 0f, input.y), 1f);
        return Quaternion.Euler(0f, yawDegrees, 0f) * local;
    }

    /// <summary>Keeps a position inside the square court (height unchanged).</summary>
    public static Vector3 ClampToRoom(Vector3 position, float halfSize)
    {
        halfSize = Mathf.Max(0f, halfSize);
        return new Vector3(Mathf.Clamp(position.x, -halfSize, halfSize), position.y, Mathf.Clamp(position.z, -halfSize, halfSize));
    }

    public static float ClampPitch(float pitch, float min, float max) => Mathf.Clamp(pitch, min, max);

    /// <summary>Yaw (x, 0 = +Z) and pitch (y, positive = down) that look from one point at another.</summary>
    public static Vector2 YawPitchTowards(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        float flat = new Vector2(d.x, d.z).magnitude;
        if (flat < 0.0001f && Mathf.Abs(d.y) < 0.0001f) return Vector2.zero;
        return new Vector2(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, -Mathf.Atan2(d.y, flat) * Mathf.Rad2Deg);
    }

    // ---------------------------------------------------------------- helpers

    private static Vector2 ReadMoveKeys(Keyboard keyboard)
    {
        Vector2 input = Vector2.zero;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
        return input;
    }

    /// <summary>True while a text box has the keyboard, so typing a name doesn't walk the player.</summary>
    private static bool IsTyping()
    {
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        return selected != null && selected.GetComponent<TMP_InputField>() != null;
    }
}
