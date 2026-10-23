using UnityEngine;

/// <summary>
/// STAGE 8 - phone settings for an AR game.
///
///  - Keeps the screen on: the player holds the phone still while aiming, and
///    Android would otherwise dim and lock it, stopping the AR session.
///  - Optional frame-rate cap. Leave it at 0 when AR Session "Match Frame Rate" is
///    on (recommended): the game then runs at the camera's rate (usually 30 fps),
///    which looks smooth and saves battery and heat.
///
/// Attach to: GameSystems.
/// </summary>
[DisallowMultipleComponent]
public class MobileSettings : MonoBehaviour
{
    [Tooltip("Stop the screen from dimming and locking while the game is open.")]
    [SerializeField] private bool keepScreenOn = true;

    [Tooltip("0 = let AR Session decide (recommended). 30 or 60 = fixed frame rate.")]
    [SerializeField, Min(0)] private int targetFrameRate = 0;

    private void Awake()
    {
        if (keepScreenOn) Screen.sleepTimeout = SleepTimeout.NeverSleep;
        if (targetFrameRate > 0) Application.targetFrameRate = targetFrameRate;
    }

    private void OnDestroy()
    {
        if (keepScreenOn) Screen.sleepTimeout = SleepTimeout.SystemSetting;
    }
}
