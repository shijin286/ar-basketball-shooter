#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Editor-only demo: plays a 60s Challenge by itself in XR Simulation and saves
/// screenshots to DemoShots/. Started by DemoRunner.Begin (Tools > AR Basketball > Run Demo).
/// Compiled out of player builds (#if UNITY_EDITOR), so the APK is unaffected.
/// </summary>
public class DemoDriver : MonoBehaviour
{
    public const string RequestFile = "Temp/arb_demo_request";
    public const string DoneFile = "Temp/arb_demo_done";
    private const string ShotFolder = "DemoShots";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    private readonly StringBuilder log = new StringBuilder();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (!File.Exists(RequestFile)) return;
        File.Delete(RequestFile);
        new GameObject("DemoDriver").AddComponent<DemoDriver>();
    }

    private IEnumerator Start()
    {
        Directory.CreateDirectory(ShotFolder);
        var game = FindFirstObjectByType<GameManager>();
        var placement = FindFirstObjectByType<ARPlanePlacement>();
        var spawner = FindFirstObjectByType<BallSpawner>();
        var shooter = FindFirstObjectByType<BasketballShooter>();
        var ui = FindFirstObjectByType<UIManager>();
        Camera cam = Camera.main;

        yield return new WaitForSeconds(4f);
        yield return Shot("01_title");

        game.StartChallenge();
        Note("Pressed 60s CHALLENGE");

        // XR Simulation only discovers planes while the camera moves, like a real phone:
        // tilt the simulated camera towards the floor and sweep it slowly left and right.
        Pose pose = default;
        bool found = false;
        float bestError = float.MaxValue;
        int step = 0;
        for (float t = 0f; t < 30f && !(found && bestError < 0.4f); t += 0.25f, step++)
        {
            MoveSimulatedCamera(18f, Mathf.Sin(step * 0.12f) * 60f);
            yield return new WaitForSeconds(0.25f);
            if (step % 2 == 0 && FindFloor(placement, cam, out Pose candidate, out float error) && error < bestError)
            {
                bestError = error;
                pose = candidate;
                found = true;
            }
        }
        if (found) Note($"Floor point chosen {FlatDistance(cam.transform.position, pose.position):0.0} m away");
        yield return Shot("02_scanning_floor");
        if (!found)
        {
            Note("No floor found - stopping the demo.");
            Finish();
            yield break;
        }

        placement.GetType().GetMethod("PlaceHoop", Private).Invoke(placement, new object[] { pose });
        Note("Hoop placed");
        StepBackTo(pose.position, 3.2f); // stand about 3 m from the hoop like a player would
        for (int f = 0; f < 5; f++) yield return null;
        LookAt(pose.position + Vector3.up * 1.2f);
        for (int f = 0; f < 5; f++) yield return null;
        Note($"Player now {FlatDistance(cam.transform.position, pose.position):0.0} m from the hoop");
        yield return new WaitForSeconds(1.5f);
        yield return Shot("03_hoop_placed");

        shooter.AimAssist = 1f; // demo: perfect aim, so every shot shows a basket
        MethodInfo swipe = shooter.GetType().GetMethod("HandleSwipe", Private);
        for (int i = 1; i <= 5; i++)
        {
            for (float w = 0f; spawner.HeldBall == null && w < 5f; w += Time.deltaTime) yield return null;
            BasketballController thrown = spawner.HeldBall;
            swipe.Invoke(shooter, new object[] { new Vector2(0f, 0.3f), 0.2f, 2.0f }); // an upward flick
            if (thrown != null) StartCoroutine(TrackBall(i, thrown, placement.HoopTransform, shooter));
            if (i == 1)
            {
                yield return new WaitForSeconds(0.35f);
                yield return Shot("04_ball_in_flight");
                yield return new WaitForSeconds(0.85f);
                yield return Shot("05_basket");
            }
            else
            {
                yield return new WaitForSeconds(1.5f);
            }
            Note($"Shot {i}: score {game.Score}, shots {game.Shots}");
        }

        game.Tick(Mathf.Max(0f, game.TimeRemaining - 6f)); // skip ahead to the last seconds
        yield return new WaitForSeconds(2.5f);
        yield return Shot("06_last_seconds");

        for (float w = 0f; !game.ResultsReady && w < 15f; w += Time.deltaTime) yield return null;
        yield return new WaitForSeconds(0.5f);
        Note($"Results: score {game.Score}, shots {game.Shots}, accuracy {Mathf.RoundToInt(game.Accuracy * 100f)}%");
        yield return Shot("07_results");

        if (ui.GetType().GetField("nameInput", Private)?.GetValue(ui) is TMP_InputField input) input.text = "Claude Demo";
        ui.SaveScore();
        yield return new WaitForSeconds(1f);
        Note($"Saved to leaderboard, rank {game.LastRank}");
        yield return Shot("08_leaderboard");

        Finish();
    }

    /// <summary>Diagnostics: follows one shot and logs how it passed the rim.</summary>
    private IEnumerator TrackBall(int shot, BasketballController ball, Transform hoop, BasketballShooter shooter)
    {
        Transform rim = hoop != null ? hoop.Find("Rim") : null;
        var trigger = hoop != null ? hoop.GetComponentInChildren<HoopScoreTrigger>(true) : null;
        object rimTarget = shooter.GetType().GetField("rimTarget", Private)?.GetValue(shooter);
        if (rim == null) { Note($"  shot {shot}: no Rim found"); yield break; }

        Vector3 start = rim.InverseTransformPoint(ball.Body.position);
        Note($"  shot {shot}: aim assist rim target {(rimTarget is Object o && o != null ? "found" : "MISSING")}, " +
             $"start (rim space) x={start.x:0.00} y={start.y:0.00} z={start.z:0.00}, launch v={ball.Body.linearVelocity.magnitude:0.0} m/s");

        ShotMath.TryGetIdealVelocity(ball.transform.position, rim.position + Vector3.up * 0.05f, 55f, ball.Body.linearDamping, out Vector3 ideal);
        System.Action<BasketballController, float, Collider> onHit = (b, speed, hit) =>
            Note($"  shot {shot}: HIT '{hit.name}' (parent '{(hit.transform.parent != null ? hit.transform.parent.name : "-")}', layer {LayerMask.LayerToName(hit.gameObject.layer)}) at {speed:0.0} m/s");
        ball.Bounced += onHit;

        float previousY = start.y;
        float apex = start.y;
        string crossing = "never came down through rim height";
        bool loggedLaunch = false;
        for (float t = 0f; t < 2f && ball != null; t += Time.fixedDeltaTime)
        {
            yield return new WaitForFixedUpdate();
            if (ball == null) break;
            if (!loggedLaunch)
            {
                loggedLaunch = true;
                Vector3 v = ball.Body.linearVelocity;
                Note($"  shot {shot}: launch velocity {v.magnitude:0.00} m/s (fwd {new Vector2(v.x, v.z).magnitude:0.00}, up {v.y:0.00}); " +
                     $"ideal {ideal.magnitude:0.00} m/s (fwd {new Vector2(ideal.x, ideal.z).magnitude:0.00}, up {ideal.y:0.00}); gravity {Physics.gravity.y:0.00}");
            }
            Vector3 p = rim.InverseTransformPoint(ball.Body.position);
            apex = Mathf.Max(apex, p.y);
            if (previousY > 0f && p.y <= 0f && crossing.StartsWith("never"))
            {
                crossing = $"crossed rim height {new Vector2(p.x, p.z).magnitude:0.00} m from centre (x={p.x:0.00}, z={p.z:0.00}) at {t:0.00} s";
            }
            previousY = p.y;
        }
        if (ball != null) ball.Bounced -= onHit;
        Note($"  shot {shot}: apex {apex:0.00} m above rim, {crossing}; trigger count {(trigger != null ? trigger.BasketsCount.ToString() : "NO TRIGGER")}");
    }

    // ---------------------------------------------------------------- simulated camera

    private Component simulationCamera;
    private MethodInfo updatePose;
    private Vector3 startPosition;
    private float startYaw;

    /// <summary>Finds XR Simulation's camera pose provider (internal type) the first time.</summary>
    private bool FindSimulationCamera()
    {
        if (simulationCamera != null) return true;
        System.Type type = System.AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("UnityEngine.XR.Simulation.SimulationCameraPoseProvider"))
            .FirstOrDefault(t => t != null);
        if (type == null) return false;
        simulationCamera = FindAnyObjectByType(type) as Component;
        updatePose = type.GetMethod("UpdatePose", Private);
        if (simulationCamera == null || updatePose == null) return false;
        startPosition = simulationCamera.transform.position;
        startYaw = simulationCamera.transform.eulerAngles.y;
        Note("Driving the XR Simulation camera");
        return true;
    }

    /// <summary>Points the simulated phone camera: pitch down (degrees) and yaw relative to where it started.</summary>
    private void MoveSimulatedCamera(float pitch, float yawOffset)
    {
        if (!FindSimulationCamera()) return;
        var rotation = Quaternion.Euler(pitch, startYaw + yawOffset, 0f);
        updatePose.Invoke(simulationCamera, new object[] { new Pose(startPosition, rotation) });
    }

    /// <summary>Turns the simulated camera to look at a world point.</summary>
    private void LookAt(Vector3 target)
    {
        if (!FindSimulationCamera()) return;
        Vector3 from = Camera.main.transform.position;
        Vector3 direction = target - from;
        float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        float pitch = -Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg;
        // The AR camera and the simulated camera share the same yaw frame up to the start offset.
        float arYawNow = Camera.main.transform.eulerAngles.y;
        float simYawNow = simulationCamera.transform.eulerAngles.y;
        var rotation = Quaternion.Euler(pitch, simYawNow + Mathf.DeltaAngle(arYawNow, yaw), 0f);
        updatePose.Invoke(simulationCamera, new object[] { new Pose(simulationCamera.transform.position, rotation) });
    }

    /// <summary>Moves the simulated camera straight back (or forward) so it stands 'distance' metres from the hoop.</summary>
    private void StepBackTo(Vector3 hoop, float distance)
    {
        if (!FindSimulationCamera()) return;
        Vector3 player = Camera.main.transform.position;
        Vector3 away = player - hoop;
        away.y = 0f;
        if (away.sqrMagnitude < 0.0001f) away = -Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up);
        away.Normalize();
        Vector3 target = hoop + away * distance;
        target.y = player.y;
        Transform sim = simulationCamera.transform;
        updatePose.Invoke(simulationCamera, new object[] { new Pose(sim.position + (target - player), sim.rotation) });
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    /// <summary>Raycasts down the middle of the screen and picks the floor point closest to 3.2 m away.</summary>
    private static bool FindFloor(ARPlanePlacement placement, Camera cam, out Pose best, out float bestError)
    {
        best = default;
        bool any = false;
        bestError = float.MaxValue;
        MethodInfo raycast = typeof(ARPlanePlacement).GetMethod("TryRaycastHorizontalPlane", Private);
        for (float y = 0.5f; y >= 0.08f; y -= 0.04f)
        {
            object[] args = { new Vector2(Screen.width * 0.5f, Screen.height * y), null };
            if (!(bool)raycast.Invoke(placement, args)) continue;
            var pose = (Pose)args[1];
            float error = Mathf.Abs(FlatDistance(pose.position, cam.transform.position) - 3.2f);
            if (error < bestError)
            {
                bestError = error;
                best = pose;
                any = true;
            }
        }
        return any;
    }

    private IEnumerator Shot(string name)
    {
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(ShotFolder, name + ".png"));
        Note("Screenshot " + name);
        yield return null;
        yield return null;
    }

    private void Note(string message)
    {
        Debug.Log("[Demo] " + message);
        log.AppendLine($"{Time.realtimeSinceStartup:0.0}s  {message}");
    }

    private void Finish()
    {
        File.WriteAllText(Path.Combine(ShotFolder, "demo_log.txt"), log.ToString());
        File.WriteAllText(DoneFile, "done");
    }
}
#endif
