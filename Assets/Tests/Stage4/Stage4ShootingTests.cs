using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// STAGE 4 automated checks for swipe shooting.
/// Run them from Window > General > Test Runner > EditMode > Run All.
///
///  - ShotMath: swipe validation, power, direction, clamping, backspin.
///  - SwipeGesture: one swipe can only ever give one shot; flick speed is measured.
///  - Physics: a perfect shot (100% aim assist) scores from 1.5 m, 2.5 m, 4 m and from the side,
///    using the real ball and hoop prefabs and BasketballController.Launch.
///
/// Game scripts live in Unity's default assembly, so they are called by name (reflection).
/// </summary>
public class Stage4ShootingTests
{
    private const string BallPrefabPath = "Assets/Prefabs/Basketball.prefab";
    private const string HoopPrefabPath = "Assets/Prefabs/BasketballHoop.prefab";
    private static readonly Vector3 TestOrigin = new Vector3(1000f, 0f, -1000f);

    // Same defaults as BasketballShooter.
    private const float MinSwipeLength = 0.06f;
    private const float MaxSwipeDuration = 1.2f;
    private const float MaxSwipeAngle = 60f;
    private const float MinShotForce = 1.5f;
    private const float MaxShotForce = 5.4f;
    private const float LaunchAngle = 55f;

    private readonly List<Object> created = new List<Object>();
    private SimulationMode previousMode;

    [SetUp]
    public void SetUp()
    {
        previousMode = Physics.simulationMode;
        Physics.simulationMode = SimulationMode.Script;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (Object o in created)
        {
            if (o != null) Object.DestroyImmediate(o);
        }
        created.Clear();
        Physics.simulationMode = previousMode;
    }

    // ---------------------------------------------------------------- ShotMath

    [TestCase(0f, 0.2f, 0.2f, true, TestName = "Swipe_StraightUp_IsValid")]
    [TestCase(0.1f, 0.2f, 0.2f, true, TestName = "Swipe_SlightlyRight_IsValid")]
    [TestCase(0f, -0.2f, 0.2f, false, TestName = "Swipe_Down_IsIgnored")]
    [TestCase(0f, 0.02f, 0.1f, false, TestName = "Swipe_TooShort_IsIgnored")]
    [TestCase(0f, 0.3f, 2.0f, false, TestName = "Swipe_TooSlow_IsIgnored")]
    [TestCase(0.4f, 0.1f, 0.2f, false, TestName = "Swipe_TooSideways_IsIgnored")]
    public void ShotMath_ValidatesSwipes(float dx, float dy, float duration, bool expected)
    {
        bool valid = (bool)CallStatic("ShotMath", "IsValidSwipe",
            new[] { typeof(Vector2), typeof(float), typeof(float), typeof(float), typeof(float) },
            new Vector2(dx, dy), duration, MinSwipeLength, MaxSwipeDuration, MaxSwipeAngle);
        Assert.AreEqual(expected, valid);
    }

    [Test]
    public void ShotMath_SwipeSpeedGivesPowerBetween0And1()
    {
        Type[] types = { typeof(float), typeof(float), typeof(float) };
        Assert.AreEqual(0f, (float)CallStatic("ShotMath", "SwipeSpeedToPower", types, 0.1f, 0.6f, 3f), 0.001f);
        Assert.AreEqual(0.5f, (float)CallStatic("ShotMath", "SwipeSpeedToPower", types, 1.8f, 0.6f, 3f), 0.001f);
        Assert.AreEqual(1f, (float)CallStatic("ShotMath", "SwipeSpeedToPower", types, 9f, 0.6f, 3f), 0.001f);
    }

    [Test]
    public void ShotMath_ClampsForceButKeepsDirection()
    {
        Type[] types = { typeof(Vector3), typeof(float), typeof(float) };
        Vector3 weak = (Vector3)CallStatic("ShotMath", "ClampImpulse", types, new Vector3(0f, 0.3f, 0.4f), MinShotForce, MaxShotForce);
        Vector3 strong = (Vector3)CallStatic("ShotMath", "ClampImpulse", types, new Vector3(0f, 0f, 20f), MinShotForce, MaxShotForce);
        Vector3 normal = (Vector3)CallStatic("ShotMath", "ClampImpulse", types, new Vector3(0f, 3f, 0f), MinShotForce, MaxShotForce);

        Assert.AreEqual(MinShotForce, weak.magnitude, 0.001f, "Weak shots must be raised to the minimum force.");
        Assert.AreEqual(0.8f, weak.normalized.z, 0.001f, "Clamping must not change the direction.");
        Assert.AreEqual(MaxShotForce, strong.magnitude, 0.001f, "Strong shots must be limited to the maximum force.");
        Assert.AreEqual(3f, normal.magnitude, 0.001f, "Forces inside the range must not change.");
    }

    [Test]
    public void ShotMath_StraightSwipeShootsForwardAndUp()
    {
        Vector3 direction = ShotDirection(Vector3.forward, Vector3.up, new Vector2(0f, 0.2f));
        float pitch = LaunchAngle * Mathf.Deg2Rad;
        Assert.AreEqual(0f, direction.x, 0.001f, "A straight swipe must not turn the shot.");
        Assert.AreEqual(Mathf.Sin(pitch), direction.y, 0.001f, "The shot must rise at the launch angle.");
        Assert.AreEqual(Mathf.Cos(pitch), direction.z, 0.001f, "The shot must go where the camera faces.");
    }

    [Test]
    public void ShotMath_SidewaysSwipeTurnsShotButNotTooMuch()
    {
        Vector3 right = ShotDirection(Vector3.forward, Vector3.up, new Vector2(0.15f, 0.2f));
        Vector3 farRight = ShotDirection(Vector3.forward, Vector3.up, new Vector2(1f, 0.2f));

        Assert.Greater(right.x, 0.01f, "Swiping to the right must aim to the right.");
        float yaw = Vector3.SignedAngle(Vector3.forward, Vector3.ProjectOnPlane(farRight, Vector3.up), Vector3.up);
        Assert.LessOrEqual(yaw, 20.01f, "Aim must be limited to Max Aim Angle (20 degrees).");
    }

    [Test]
    public void ShotMath_PhoneFacingFloorStillShootsForward()
    {
        Vector3 direction = ShotDirection(Vector3.down, Vector3.forward, new Vector2(0f, 0.2f));
        Assert.IsFalse(float.IsNaN(direction.x), "Direction must never be NaN.");
        Assert.Greater(direction.z, 0.1f, "When the phone faces the floor, shoot towards the top of the phone.");
    }

    [Test]
    public void ShotMath_ImpossibleTargetHasNoIdealShot()
    {
        object[] args = { Vector3.zero, new Vector3(0f, 3f, 0.1f), LaunchAngle, 0.05f, null };
        bool possible = (bool)CallStatic("ShotMath", "TryGetIdealVelocity", IdealVelocityTypes, args);
        Assert.IsFalse(possible, "A target straight overhead can't be reached at 55 degrees.");
    }

    [Test]
    public void ShotMath_CloseLowShotComesDownSteeply()
    {
        // Phone held low and close: the ball starts 0.88 m below and 1.78 m in front of the target.
        // A fixed 55° throw would come down at only ~22° and clip the front of the rim.
        object[] args = { Vector3.zero, new Vector3(0f, 0.88f, 1.78f), LaunchAngle, 0f, null };
        Assert.IsTrue((bool)CallStatic("ShotMath", "TryGetIdealVelocity", IdealVelocityTypes, args), "No ideal shot found.");

        var v = (Vector3)args[4];
        float forward = new Vector2(v.x, v.z).magnitude;
        float timeToTarget = 1.78f / forward;
        float verticalAtTarget = v.y + Physics.gravity.y * timeToTarget;
        float entryAngle = Mathf.Atan2(-verticalAtTarget, forward) * Mathf.Rad2Deg;

        Assert.GreaterOrEqual(entryAngle, 44.9f, $"The ball must drop in at 45° or steeper (was {entryAngle:0}°).");
        Assert.That(v.magnitude * 0.6f, Is.InRange(MinShotForce, MaxShotForce), "The steeper shot must stay inside the force clamp.");
    }

    [Test]
    public void ShotMath_BackspinSpinsTopTowardsPlayer()
    {
        Type[] types = { typeof(Vector3), typeof(float), typeof(float), typeof(float) };
        Vector3 spin = (Vector3)CallStatic("ShotMath", "BackspinImpulse", types, new Vector3(0f, 1f, 1f), 0.6f, 0.12f, 15f);

        float expected = 0.4f * 0.6f * 0.12f * 0.12f * 15f;
        Assert.AreEqual(-expected, spin.x, 0.0001f, "Backspin is a negative rotation around the shot's right axis.");
        Assert.AreEqual(0f, spin.y, 0.0001f);
        Assert.AreEqual(0f, spin.z, 0.0001f);
    }

    // ---------------------------------------------------------------- SwipeGesture

    [Test]
    public void SwipeGesture_OneSwipeGivesOnlyOneShot()
    {
        object gesture = CreateGesture();
        Call(gesture, "Begin", new Vector2(0.5f, 0.2f), 0f);
        Call(gesture, "Move", new Vector2(0.5f, 0.3f), 0.05f);

        Assert.IsTrue((bool)Call(gesture, "End", new Vector2(0.5f, 0.45f), 0.1f), "Releasing should finish the swipe.");
        Assert.IsFalse((bool)Call(gesture, "End", new Vector2(0.5f, 0.6f), 0.15f), "A second release must not shoot again.");

        Call(gesture, "Move", new Vector2(0.5f, 0.8f), 0.2f);
        Assert.IsFalse((bool)Call(gesture, "End", new Vector2(0.5f, 0.9f), 0.25f), "Moving after release must not shoot.");
    }

    [Test]
    public void SwipeGesture_CancelledSwipeGivesNoShot()
    {
        object gesture = CreateGesture();
        Call(gesture, "Begin", new Vector2(0.5f, 0.2f), 0f);
        Call(gesture, "Cancel");
        Assert.IsFalse((bool)Call(gesture, "End", new Vector2(0.5f, 0.6f), 0.1f), "A cancelled swipe (e.g. on a button) must not shoot.");
    }

    [Test]
    public void SwipeGesture_MeasuresFinalFlickSpeed()
    {
        object gesture = CreateGesture();
        Call(gesture, "Begin", new Vector2(0.5f, 0.2f), 0f);
        for (int i = 1; i <= 4; i++) Call(gesture, "Move", new Vector2(0.5f, 0.2f), i * 0.1f); // finger rests
        Call(gesture, "Move", new Vector2(0.5f, 0.2f), 0.45f);
        Call(gesture, "Move", new Vector2(0.5f, 0.3f), 0.5f);  // then flicks up quickly
        Call(gesture, "Move", new Vector2(0.5f, 0.4f), 0.55f);
        Call(gesture, "End", new Vector2(0.5f, 0.5f), 0.6f);

        Vector2 delta = (Vector2)Get(gesture, "LastDelta");
        float duration = (float)Get(gesture, "LastDuration");
        float speed = (float)Get(gesture, "LastSpeed");

        Assert.AreEqual(0.3f, delta.y, 0.001f);
        Assert.AreEqual(0.6f, duration, 0.001f);
        Assert.Greater(speed, 1.5f, "The flick at the end (about 2 screen heights/s) should count, not the slow average (0.5).");
    }

    // ---------------------------------------------------------------- physics

    [TestCase(1.5f, 0f, 1.35f, TestName = "PerfectShot_Scores_From1_5m")]
    [TestCase(2.5f, 0f, 1.35f, TestName = "PerfectShot_Scores_From2_5m")]
    [TestCase(4f, 0f, 1.35f, TestName = "PerfectShot_Scores_From4m")]
    [TestCase(3f, 0.8f, 1.35f, TestName = "PerfectShot_Scores_FromTheSide")]
    [TestCase(1.78f, 0f, 0.77f, TestName = "PerfectShot_Scores_FromLowAndClose")]
    public void PerfectShot_GoesThroughHoop(float distance, float sideOffset, float startHeight)
    {
        GameObject hoopPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HoopPrefabPath);
        GameObject ballPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BallPrefabPath);
        Assert.IsNotNull(hoopPrefab, "Hoop prefab missing (Stage 2).");
        Assert.IsNotNull(ballPrefab, "Ball prefab missing (Stage 3).");

        // A rotated hoop proves the maths works in any direction.
        GameObject hoop = Spawn(hoopPrefab, TestOrigin, Quaternion.Euler(0f, 37f, 0f));
        Transform rim = hoop.transform.Find("Rim");
        Assert.IsNotNull(rim, "Hoop child 'Rim' missing.");

        Vector3 start = hoop.transform.TransformPoint(new Vector3(sideOffset, startHeight, rim.localPosition.z + distance));
        GameObject ball = Spawn(ballPrefab, start, Quaternion.identity);
        Rigidbody body = ball.GetComponent<Rigidbody>();
        Component controller = ball.GetComponent("BasketballController");
        Assert.IsNotNull(controller, "BasketballController missing on the ball.");

        GameObject holdPoint = new GameObject("TestHoldPoint");
        created.Add(holdPoint);
        holdPoint.transform.position = start;
        Call(controller, "Hold", new[] { typeof(Transform) }, holdPoint.transform);

        object[] args = { start, rim.position + Vector3.up * 0.05f, LaunchAngle, body.linearDamping, null };
        Assert.IsTrue((bool)CallStatic("ShotMath", "TryGetIdealVelocity", IdealVelocityTypes, args), "No ideal shot found.");
        Vector3 impulse = (Vector3)args[4] * body.mass;
        Assert.That(impulse.magnitude, Is.InRange(MinShotForce, MaxShotForce),
            $"The perfect shot needs {impulse.magnitude:0.00} N·s, outside the default Min/Max Shot Force.");

        Vector3 spin = (Vector3)CallStatic("ShotMath", "BackspinImpulse",
            new[] { typeof(Vector3), typeof(float), typeof(float), typeof(float) }, impulse, body.mass, 0.12f, 15f);
        Call(controller, "Launch", new[] { typeof(Vector3), typeof(Vector3) }, impulse, spin);
        Physics.SyncTransforms();

        bool crossed = false;
        float offset = float.MaxValue;
        float previousY = body.position.y;
        float step = Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : 0.02f;
        for (int i = 0; i < Mathf.CeilToInt(2.5f / step) && !crossed; i++)
        {
            Physics.Simulate(step);
            float y = body.position.y;
            if (previousY > rim.position.y && y <= rim.position.y)
            {
                crossed = true;
                Vector3 d = body.position - rim.position;
                d.y = 0f;
                offset = d.magnitude;
            }
            previousY = y;
        }

        Assert.IsTrue(crossed, "The ball never came down through rim height.");
        Assert.Less(offset, 0.2f, $"The ball came down {offset:0.00} m from the rim centre (missed).");
    }

    // ---------------------------------------------------------------- helpers

    private static readonly Type[] IdealVelocityTypes =
    {
        typeof(Vector3), typeof(Vector3), typeof(float), typeof(float), typeof(Vector3).MakeByRefType()
    };

    private static Vector3 ShotDirection(Vector3 forward, Vector3 up, Vector2 swipe)
    {
        return (Vector3)CallStatic("ShotMath", "ShotDirection",
            new[] { typeof(Vector3), typeof(Vector3), typeof(Vector2), typeof(float), typeof(float), typeof(float) },
            forward, up, swipe, 0.5f, 20f, LaunchAngle);
    }

    private GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        GameObject go = (GameObject)Object.Instantiate(prefab, position, rotation);
        created.Add(go);
        Physics.SyncTransforms();
        return go;
    }

    private static Type FindGameType(string name)
    {
        Type type = Type.GetType(name + ", Assembly-CSharp");
        Assert.IsNotNull(type, $"Class {name} not found. Copy {name}.cs into Assets/Scripts and fix any Console errors.");
        return type;
    }

    private static object CallStatic(string typeName, string method, Type[] types, params object[] args)
    {
        MethodInfo info = FindGameType(typeName).GetMethod(method, BindingFlags.Public | BindingFlags.Static, null, types, null);
        Assert.IsNotNull(info, $"{typeName}.{method} not found - is the script up to date?");
        return info.Invoke(null, args);
    }

    private static object CreateGesture() => Activator.CreateInstance(FindGameType("SwipeGesture"));

    private static object Call(object target, string method, params object[] args)
    {
        Type[] types = Array.ConvertAll(args, a => a.GetType());
        return Call(target, method, types, args);
    }

    private static object Call(object target, string method, Type[] types, params object[] args)
    {
        MethodInfo info = target.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.Instance, null, types, null);
        Assert.IsNotNull(info, $"{target.GetType().Name}.{method} not found - is the script up to date?");
        return info.Invoke(target, args);
    }

    private static object Get(object target, string property)
    {
        PropertyInfo info = target.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(info, $"{target.GetType().Name}.{property} not found - is the script up to date?");
        return info.GetValue(target);
    }
}
