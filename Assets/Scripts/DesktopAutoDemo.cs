using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// STAGE 10 - self-test for the laptop build.
///
/// Run "ARBasketballShooter.exe -autodemo" and the game plays itself with a virtual mouse,
/// through the same input code as a real player: it clicks FREE PLAY, clicks the floor 3 m
/// ahead to place the hoop, then makes five upward mouse drags. Screenshots and
/// autodemo_result.txt are saved in an "AutoDemo" folder next to the .exe, then the app closes.
///
/// Without -autodemo (normal play) this does nothing.
/// </summary>
public class DesktopAutoDemo : MonoBehaviour
{
    public const string Argument = "-autodemo";
    private const int ShotCount = 5;

    private readonly StringBuilder report = new StringBuilder();
    private string folder;
    private Mouse mouse;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartIfRequested()
    {
        if (!DesktopMode.IsActive || !System.Environment.GetCommandLineArgs().Contains(Argument)) return;
        new GameObject("DesktopAutoDemo").AddComponent<DesktopAutoDemo>();
    }

    private IEnumerator Start()
    {
        folder = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "AutoDemo");
        Directory.CreateDirectory(folder);
        mouse = InputSystem.AddDevice<Mouse>("AutoDemoMouse");

        var game = FindFirstObjectByType<GameManager>();
        var placement = FindFirstObjectByType<ARPlanePlacement>();
        var spawner = FindFirstObjectByType<BallSpawner>();
        Camera cam = Camera.main;
        Note($"Screen {Screen.width}x{Screen.height}, desktop mode {DesktopMode.IsActive}, floor placement {placement.UsesDesktopFloor}");

        yield return new WaitForSeconds(2f);
        yield return Capture("01_title");

        // 1. FREE PLAY button, clicked like a player would.
        GameObject start = GameObject.Find("StartButton");
        if (start != null) yield return Click(RectTransformUtility.WorldToScreenPoint(null, start.transform.position));
        Note($"Clicked FREE PLAY: state {game.State}");
        if (game.State == GameManager.GameState.Title) game.StartGame();
        yield return new WaitForSeconds(1f);
        yield return Capture("02_place_hoop");

        // 2. Click the floor 3 m in front of the player.
        Vector3 forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
        Vector3 spot = cam.transform.position + forward * 3f;
        spot.y = 0f;
        yield return Click(cam.WorldToScreenPoint(spot));
        Note($"Clicked the floor: hoop placed {placement.IsHoopPlaced}, state {game.State}");
        if (placement.IsHoopPlaced)
        {
            Vector3 hoop = placement.HoopTransform.position;
            Note($"Hoop at {hoop}, {Vector3.Distance(new Vector3(hoop.x, 0f, hoop.z), new Vector3(cam.transform.position.x, 0f, cam.transform.position.z)):0.0} m from the player");
        }
        yield return new WaitForSeconds(1.5f);
        yield return Capture("03_hoop_placed");

        // 3. Five upward mouse drags (35% of the screen height in 0.18 s).
        int firstFrame = Time.frameCount;
        float firstTime = Time.realtimeSinceStartup;
        for (int i = 1; i <= ShotCount && placement.IsHoopPlaced; i++)
        {
            for (float w = 0f; spawner.HeldBall == null && w < 5f; w += Time.deltaTime) yield return null;
            var from = new Vector2(Screen.width * 0.5f, Screen.height * 0.2f);
            var to = new Vector2(Screen.width * 0.5f, Screen.height * 0.55f);
            yield return Drag(from, to, 0.18f);

            if (i == 1)
            {
                yield return new WaitForSeconds(0.35f);
                yield return Capture("04_ball_in_flight");
                yield return new WaitForSeconds(1.2f);
                yield return Capture("05_after_first_shot");
            }
            else
            {
                yield return new WaitForSeconds(1.8f);
            }
            Note($"Shot {i}: score {game.Score}, shots {game.Shots}");
        }
        float fps = (Time.frameCount - firstFrame) / Mathf.Max(0.001f, Time.realtimeSinceStartup - firstTime);

        yield return new WaitForSeconds(1.5f);
        yield return Capture("06_final");
        Note($"Window focused {Application.isFocused}, battery {SystemInfo.batteryStatus} {SystemInfo.batteryLevel:P0}");
        Note($"RESULT: hoop placed {placement.IsHoopPlaced}, shots {game.Shots}/{ShotCount}, baskets {game.Baskets}, {fps:0} fps while shooting");
        File.WriteAllText(Path.Combine(folder, "autodemo_result.txt"), report.ToString());
        Application.Quit();
    }

    private IEnumerator Click(Vector2 position)
    {
        Send(position, false);
        yield return null;
        Send(position, true);
        yield return null;
        yield return null;
        Send(position, false);
        yield return null;
    }

    private IEnumerator Drag(Vector2 from, Vector2 to, float seconds)
    {
        Send(from, false);
        yield return null;
        Send(from, true);
        yield return null;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            Send(Vector2.Lerp(from, to, t / seconds), true);
            yield return null;
        }
        Send(to, true);
        yield return null;
        Send(to, false);
        yield return null;
    }

    /// <summary>Moves the virtual mouse and sets its left button. The game reads it on the next frame.</summary>
    private void Send(Vector2 position, bool leftDown)
    {
        mouse.MakeCurrent();
        InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left, leftDown));
    }

    private IEnumerator Capture(string name)
    {
        ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
        yield return null;
        yield return null;
    }

    private void Note(string line)
    {
        report.AppendLine(line);
        Debug.Log("[AutoDemo] " + line);
    }
}
