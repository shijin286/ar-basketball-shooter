using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object = UnityEngine.Object;

/// <summary>
/// STAGE 5 automated checks in the PlayMode tab: real balls, real physics and real
/// trigger events against the real hoop prefab.
///
///  ✔ a clean shot scores 1          ✔ a ball dropped through the centre scores 1
///  ✘ a ball shot up from below scores 0, even when it falls back through the hoop
///  ✘ a ball that only hits the rim scores 0     ✘ a ball that only hits the backboard scores 0
///  ✘ the same ball can't score twice            ✘ a ball still in the hand can't score
///  ✔ two different balls score 2
///
/// Run them from Window > General > Test Runner > PlayMode > Run All (takes about 20 seconds).
/// </summary>
public class Stage5ScoringPlayTests
{
    private const string BallPrefabPath = "Assets/Prefabs/Basketball.prefab";
    private const string HoopPrefabPath = "Assets/Prefabs/BasketballHoop.prefab";
    private const float LaunchAngle = 55f;

    private readonly List<Object> created = new List<Object>();
    private GameObject ballPrefab;
    private GameObject hoop;
    private Transform rim;
    private Component scoreTrigger;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        // XR Simulation may log messages when Play mode starts; they are not what we are testing.
        LogAssert.ignoreFailingMessages = true;

        GameObject hoopPrefab = null;
#if UNITY_EDITOR
        ballPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BallPrefabPath);
        hoopPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HoopPrefabPath);
#endif
        if (ballPrefab == null || hoopPrefab == null) Assert.Fail("Ball or hoop prefab missing (Stages 2 and 3).");

        // A rotated hoop proves scoring works whichever way the hoop faces.
        hoop = Spawn(hoopPrefab, Vector3.zero, Quaternion.Euler(0f, 25f, 0f));
        rim = hoop.transform.Find("Rim");
        Transform zone = rim != null ? rim.Find("ScoreZone") : null;
        scoreTrigger = zone != null ? zone.GetComponent("HoopScoreTrigger") : null;
        if (scoreTrigger == null)
        {
            DestroyCreated();
            Assert.Fail("Rim/ScoreZone with HoopScoreTrigger not found. Rebuild the hoop prefab (Stage 5).");
        }

        yield return new WaitForFixedUpdate();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        DestroyCreated();
        yield return null;
    }

    // ---------------------------------------------------------------- should score

    [UnityTest]
    public IEnumerator CleanShot_ScoresOnePoint()
    {
        Vector3 start = hoop.transform.TransformPoint(new Vector3(0f, 1.35f, rim.localPosition.z + 2.5f));
        Component ball = LaunchBall(start, IdealVelocity(start, rim.position + Vector3.up * 0.05f));

        yield return new WaitForSeconds(2f);

        Assert.AreEqual(1, Baskets, "A clean shot through the hoop must score exactly 1.");
        Assert.IsTrue(HasScored(ball), "The ball must be marked as scored.");
    }

    [UnityTest]
    public IEnumerator BallDroppedThroughCentre_ScoresOnePoint()
    {
        Component ball = LaunchBall(rim.position + Vector3.up * 0.6f, Vector3.zero);

        yield return new WaitForSeconds(1.5f);

        Assert.AreEqual(1, Baskets, "A ball falling straight through the hoop must score 1.");
        Assert.IsTrue(HasScored(ball));
    }

    [UnityTest]
    public IEnumerator TwoBalls_ScoreTwoPoints()
    {
        LaunchBall(rim.position + Vector3.up * 0.6f, Vector3.zero);
        yield return new WaitForSeconds(0.4f);
        LaunchBall(rim.position + Vector3.up * 0.6f, Vector3.zero);

        yield return new WaitForSeconds(1.5f);

        Assert.AreEqual(2, Baskets, "Each new ball can score once.");
    }

    // ---------------------------------------------------------------- should NOT score

    [UnityTest]
    public IEnumerator BallFromBelow_NeverScores()
    {
        // Shot straight up through the net, out of the top, then it falls back down through the hoop.
        Component ball = LaunchBall(rim.position - Vector3.up * 0.7f, Vector3.up * 5f);

        yield return new WaitForSeconds(2f);

        Assert.AreEqual(0, Baskets, "A ball that came up from below must never score.");
        Assert.IsFalse(HasScored(ball));
    }

    [UnityTest]
    public IEnumerator RimTouch_BouncesOutAndDoesNotScore()
    {
        Vector3 start = rim.TransformPoint(new Vector3(0f, 0.5f, RingRadius() + 0.06f)); // just outside the rim tube
        Component ball = LaunchBall(start, Vector3.zero);
        Rigidbody body = ((Component)ball).GetComponent<Rigidbody>();

        yield return new WaitForSeconds(1.5f);

        Vector3 moved = body.position - start;
        moved.y = 0f;
        Assert.Greater(moved.magnitude, 0.05f, "The ball should have bounced off the rim (test set-up problem otherwise).");
        Assert.AreEqual(0, Baskets, "Touching the rim must not score.");
    }

    [UnityTest]
    public IEnumerator BackboardHit_DoesNotScore()
    {
        Transform board = hoop.transform.Find("Backboard");
        Assert.IsNotNull(board, "Hoop child 'Backboard' missing.");
        Vector3 forward = hoop.transform.forward;
        Vector3 start = board.position + forward * 1f + hoop.transform.right * 0.45f + Vector3.up * 0.35f;
        Component ball = LaunchBall(start, -forward * 5f);
        Rigidbody body = ball.GetComponent<Rigidbody>();

        yield return new WaitForSeconds(1.5f);

        Assert.Greater(Vector3.Dot(body.linearVelocity, forward), 0f, "The ball should have bounced off the board.");
        Assert.AreEqual(0, Baskets, "Hitting the backboard (and missing the hoop) must not score.");
    }

    [UnityTest]
    public IEnumerator SameBall_CannotScoreTwice()
    {
        Component ball = LaunchBall(rim.position + Vector3.up * 0.6f, Vector3.zero);
        yield return new WaitForSeconds(1.2f);
        Assert.AreEqual(1, Baskets, "First pass should score.");

        // Put the same ball back above the hoop and let it fall through again.
        Rigidbody body = ball.GetComponent<Rigidbody>();
        body.linearVelocity = Vector3.zero;
        body.position = rim.position + Vector3.up * 0.6f;
        ball.transform.position = body.position;

        yield return new WaitForSeconds(1.2f);

        Assert.AreEqual(1, Baskets, "The same ball must never score a second time.");
    }

    [UnityTest]
    public IEnumerator HeldBall_InsideHoop_DoesNotScore()
    {
        Component ball = SpawnHeldBall(rim.position);

        yield return new WaitForSeconds(0.5f);

        Assert.AreEqual(0, Baskets, "A ball in the player's hand (kinematic, collider off) must never score.");
        Assert.AreEqual("Held", Get(ball, "State").ToString());
    }

    // ---------------------------------------------------------------- helpers

    private int Baskets => (int)Get(scoreTrigger, "BasketsCount");

    private static bool HasScored(Component ball) => (bool)Get(ball, "HasScored");

    private Component SpawnHeldBall(Vector3 position)
    {
        GameObject ball = Spawn(ballPrefab, position, Quaternion.identity);
        Component controller = ball.GetComponent("BasketballController");
        Assert.IsNotNull(controller, "BasketballController missing on the ball prefab.");

        GameObject holdPoint = new GameObject("TestHoldPoint");
        created.Add(holdPoint);
        holdPoint.transform.position = position;
        Call(controller, "Hold", new[] { typeof(Transform) }, holdPoint.transform);
        return controller;
    }

    private Component LaunchBall(Vector3 start, Vector3 velocity)
    {
        Component controller = SpawnHeldBall(start);
        Rigidbody body = controller.GetComponent<Rigidbody>();
        Call(controller, "Launch", new[] { typeof(Vector3), typeof(Vector3) }, velocity * body.mass, Vector3.zero);
        return controller;
    }

    private Vector3 IdealVelocity(Vector3 start, Vector3 target)
    {
        Type shotMath = FindGameType("ShotMath");
        MethodInfo method = shotMath.GetMethod("TryGetIdealVelocity", BindingFlags.Public | BindingFlags.Static, null,
            new[] { typeof(Vector3), typeof(Vector3), typeof(float), typeof(float), typeof(Vector3).MakeByRefType() }, null);
        Assert.IsNotNull(method, "ShotMath.TryGetIdealVelocity not found (Stage 4).");

        float damping = ballPrefab.GetComponent<Rigidbody>().linearDamping;
        object[] args = { start, target, LaunchAngle, damping, null };
        Assert.IsTrue((bool)method.Invoke(null, args), "No ideal shot found.");
        return (Vector3)args[4];
    }

    private float RingRadius()
    {
        return rim.GetComponentsInChildren<CapsuleCollider>()
            .Select(c =>
            {
                Vector3 p = rim.InverseTransformPoint(c.transform.TransformPoint(c.center));
                p.y = 0f;
                return p.magnitude;
            })
            .Average();
    }

    private GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        GameObject go = Object.Instantiate(prefab, position, rotation);
        created.Add(go);
        return go;
    }

    private void DestroyCreated()
    {
        foreach (Object o in created)
        {
            if (o != null) Object.Destroy(o);
        }
        created.Clear();
    }

    private static Type FindGameType(string name)
    {
        Type type = Type.GetType(name + ", Assembly-CSharp");
        Assert.IsNotNull(type, $"Class {name} not found in Assets/Scripts.");
        return type;
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
