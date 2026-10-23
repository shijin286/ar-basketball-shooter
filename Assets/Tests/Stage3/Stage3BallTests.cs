using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// STAGE 3 automated checks for Assets/Prefabs/Basketball.prefab.
/// Run them from Window > General > Test Runner > EditMode > Run All.
///
/// Checks the prefab settings, runs real physics in Edit Mode (bounce height,
/// resting on the floor, no tunnelling through the backboard, a good shot
/// goes through the hoop), and calls BasketballController's Hold / Launch /
/// TryMarkScored methods to check its rules.
///
/// Game scripts are in Unity's default assembly, which test assemblies can't
/// reference directly, so the controller is reached by name (reflection).
/// </summary>
public class Stage3BallTests
{
    private const string BallPrefabPath = "Assets/Prefabs/Basketball.prefab";
    private const string HoopPrefabPath = "Assets/Prefabs/BasketballHoop.prefab";
    private const string PlanePrefabPath = "Assets/Prefabs/ARPlane.prefab";
    private const string FloorMaterialPath = "Assets/Materials/Physics/Floor_Physics.asset";
    private const float BallRadius = 0.12f;
    private static readonly Vector3 TestOrigin = new Vector3(-1000f, 0f, 1000f);

    private readonly List<Object> created = new List<Object>();
    private GameObject ballPrefab;
    private SimulationMode previousMode;

    [SetUp]
    public void SetUp()
    {
        ballPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BallPrefabPath);
        if (ballPrefab == null)
        {
            Assert.Fail("Ball prefab not found. Run Tools > AR Basketball > Create Basketball Prefab first.");
        }

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

    // ---------------------------------------------------------------- prefab

    [Test]
    public void Prefab_RigidbodyIsConfigured()
    {
        Rigidbody body = ballPrefab.GetComponent<Rigidbody>();
        Assert.IsNotNull(body, "The ball root needs a Rigidbody.");
        Assert.That(body.mass, Is.InRange(0.55f, 0.65f), "Mass should be about 0.6 kg.");
        Assert.IsTrue(body.useGravity, "Use Gravity must be on.");
        Assert.IsFalse(body.isKinematic, "The prefab must not be kinematic (the controller does that while held).");
        Assert.AreEqual(CollisionDetectionMode.ContinuousDynamic, body.collisionDetectionMode,
            "Collision Detection must be Continuous Dynamic so fast shots don't pass through the rim.");
        Assert.AreEqual(RigidbodyInterpolation.Interpolate, body.interpolation, "Interpolate must be on.");
        Assert.That(body.linearDamping, Is.InRange(0f, 0.2f), "Linear Damping (drag) should be small.");
    }

    [Test]
    public void Prefab_IsRealBasketballSize()
    {
        SphereCollider sphere = ballPrefab.GetComponent<SphereCollider>();
        Assert.IsNotNull(sphere, "The ball root needs a Sphere Collider.");

        Vector3 s = ballPrefab.transform.localScale;
        float worldRadius = sphere.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
        Assert.That(worldRadius, Is.InRange(0.115f, 0.125f), "Collider radius should be 0.12 m (0.24 m ball).");

        Assert.IsNotNull(sphere.sharedMaterial, "The Sphere Collider needs the Ball_Physics material.");
        Assert.That(sphere.sharedMaterial.bounciness, Is.InRange(0.6f, 0.9f), "Ball bounciness should be about 0.8.");

        GameObject ball = Spawn(ballPrefab, TestOrigin);
        Renderer visual = ball.GetComponentInChildren<Renderer>();
        Assert.IsNotNull(visual, "The ball needs a visible mesh (Visual child).");
        Assert.That(visual.bounds.size.x, Is.InRange(0.23f, 0.25f), "The visual should match the collider size.");
    }

    [Test]
    public void Prefab_HasControllerAndTexture()
    {
        Assert.IsNotNull(ballPrefab.GetComponent("BasketballController"), "Add BasketballController to the ball root.");

        Renderer visual = ballPrefab.GetComponentInChildren<Renderer>();
        Assert.IsNotNull(visual, "The ball needs a Visual child with a renderer.");
        Assert.IsNotNull(visual.sharedMaterial, "The ball needs a material.");
        Assert.IsNotNull(visual.sharedMaterial.mainTexture, "The ball material needs the basketball texture.");
    }

    [Test]
    public void ARPlanePrefab_UsesFloorPhysics()
    {
        GameObject plane = AssetDatabase.LoadAssetAtPath<GameObject>(PlanePrefabPath);
        Assert.IsNotNull(plane, $"{PlanePrefabPath} not found.");
        PhysicsMaterial floor = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(FloorMaterialPath);
        Assert.IsNotNull(floor, $"{FloorMaterialPath} not found.");

        MeshCollider[] colliders = plane.GetComponentsInChildren<MeshCollider>(true);
        Assert.IsNotEmpty(colliders, "The ARPlane prefab needs its Mesh Collider (the ball lands on it).");
        Assert.IsTrue(colliders.All(c => c.sharedMaterial == floor), "ARPlane's Mesh Collider must use Floor_Physics.");
    }

    // ---------------------------------------------------------------- physics

    [Test]
    public void Ball_BouncesRealisticallyOnFloor()
    {
        CreateFloor(TestOrigin);
        const float dropHeight = 1.5f;
        Rigidbody ball = SpawnBall(TestOrigin + Vector3.up * (dropHeight + BallRadius));

        bool bounced = false;
        float highestAfterBounce = 0f;
        Simulate(2f, () =>
        {
            if (ball.linearVelocity.y > 0.1f) bounced = true;
            if (bounced) highestAfterBounce = Mathf.Max(highestAfterBounce, ball.position.y - TestOrigin.y - BallRadius);
        });

        Assert.IsTrue(bounced, "The ball did not bounce at all.");
        float ratio = highestAfterBounce / dropHeight;
        Assert.That(ratio, Is.InRange(0.35f, 0.85f),
            $"Bounce height was {ratio:P0} of the drop height. Real basketballs bounce to about 55-65%.");
    }

    [Test]
    public void Ball_ComesToRestOnFloor()
    {
        CreateFloor(TestOrigin);
        Rigidbody ball = SpawnBall(TestOrigin + Vector3.up * 1f);

        Simulate(6f, null);

        float height = ball.position.y - TestOrigin.y;
        Assert.That(height, Is.InRange(BallRadius - 0.01f, BallRadius + 0.03f), "The ball should rest on top of the floor.");
        Assert.Less(ball.linearVelocity.magnitude, 0.3f, "The ball should have stopped bouncing.");
    }

    [Test]
    public void Ball_FastShotDoesNotPassThroughBackboard()
    {
        GameObject hoop = SpawnHoop();
        Transform board = hoop.transform.Find("Backboard");
        Assert.IsNotNull(board, "Hoop child 'Backboard' not found.");

        Vector3 forward = hoop.transform.forward;
        Rigidbody ball = SpawnBall(board.position + forward * 1.5f + Vector3.up * 0.15f);
        ball.useGravity = false;
        ball.linearVelocity = -forward * 15f;

        Simulate(0.5f, null);

        Assert.Greater(Vector3.Dot(ball.position - board.position, forward), 0f,
            "A fast ball passed through the backboard. Collision Detection must be Continuous Dynamic.");
    }

    [Test]
    public void Ball_GoodShotGoesThroughHoop()
    {
        GameObject hoop = SpawnHoop();
        Transform rim = hoop.transform.Find("Rim");
        Assert.IsNotNull(rim, "Hoop child 'Rim' not found.");

        // A typical shot: 2.5 m in front of the rim, released at 1.2 m height, 0.9 s flight.
        Vector3 start = hoop.transform.TransformPoint(new Vector3(0f, 1.2f, rim.localPosition.z + 2.5f));
        Vector3 target = rim.position + Vector3.up * 0.05f;
        Rigidbody ball = SpawnBall(start);
        ball.linearVelocity = BallisticVelocity(start, target, 0.9f, ball.linearDamping);

        bool crossed = false;
        float offset = float.MaxValue;
        float previousY = ball.position.y;
        Simulate(1.6f, () =>
        {
            float y = ball.position.y;
            if (!crossed && previousY > rim.position.y && y <= rim.position.y)
            {
                crossed = true;
                Vector3 d = ball.position - rim.position;
                d.y = 0f;
                offset = d.magnitude;
            }
            previousY = y;
        });

        Assert.IsTrue(crossed, "The ball never came down through rim height.");
        Assert.Less(offset, 0.2f, $"The ball came down {offset:0.00} m from the rim centre (outside the hoop).");
    }

    // ---------------------------------------------------------------- controller rules

    [Test]
    public void Controller_HoldMakesBallKinematicAndHarmless()
    {
        GameObject ball = Spawn(ballPrefab, TestOrigin);
        Component controller = GetController(ball);
        Transform holdPoint = CreateHoldPoint();

        Call(controller, "Hold", new[] { typeof(Transform) }, holdPoint);

        Assert.IsTrue(ball.GetComponent<Rigidbody>().isKinematic, "A held ball must be kinematic.");
        Assert.IsFalse(ball.GetComponent<SphereCollider>().enabled, "A held ball's collider must be off.");
        Assert.AreEqual(holdPoint, ball.transform.parent, "A held ball must follow the hold point.");
        Assert.AreEqual("Held", Get(controller, "State").ToString());
    }

    [Test]
    public void Controller_LaunchAppliesImpulseExactlyOnce()
    {
        GameObject ball = Spawn(ballPrefab, TestOrigin);
        Component controller = GetController(ball);
        Call(controller, "Hold", new[] { typeof(Transform) }, CreateHoldPoint());

        Vector3 impulse = new Vector3(0f, 3f, 1.5f);
        Type[] launchArgs = { typeof(Vector3), typeof(Vector3) };
        bool first = (bool)Call(controller, "Launch", launchArgs, impulse, Vector3.zero);
        bool second = (bool)Call(controller, "Launch", launchArgs, impulse, Vector3.zero);

        Assert.IsTrue(first, "Launch should succeed on a held ball.");
        Assert.IsFalse(second, "A ball must not be launchable twice.");

        Rigidbody body = ball.GetComponent<Rigidbody>();
        Assert.IsFalse(body.isKinematic, "A launched ball must use physics.");
        Assert.IsTrue(ball.GetComponent<SphereCollider>().enabled, "A launched ball's collider must be on.");
        Assert.IsNull(ball.transform.parent, "A launched ball must leave the hold point.");
        Assert.AreEqual("Flying", Get(controller, "State").ToString());

        Physics.SyncTransforms();
        Physics.Simulate(Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : 0.02f);
        Vector3 expected = impulse / body.mass;
        Assert.AreEqual(expected.z, body.linearVelocity.z, 0.1f, "Forward speed should be impulse / mass.");
        Assert.AreEqual(expected.y, body.linearVelocity.y, 0.3f, "Upward speed should be impulse / mass.");
    }

    [Test]
    public void Controller_BallCanScoreOnlyOnce()
    {
        GameObject ball = Spawn(ballPrefab, TestOrigin);
        Component controller = GetController(ball);
        Call(controller, "Hold", new[] { typeof(Transform) }, CreateHoldPoint());

        Assert.IsFalse((bool)Call(controller, "TryMarkScored", Type.EmptyTypes), "A held ball must not score.");

        Call(controller, "Launch", new[] { typeof(Vector3), typeof(Vector3) }, Vector3.forward, Vector3.zero);
        Assert.IsTrue((bool)Call(controller, "TryMarkScored", Type.EmptyTypes), "A flying ball should be able to score.");
        Assert.IsFalse((bool)Call(controller, "TryMarkScored", Type.EmptyTypes), "The same ball must not score twice.");
    }

    // ---------------------------------------------------------------- helpers

    private GameObject Spawn(GameObject prefab, Vector3 position)
    {
        GameObject go = (GameObject)Object.Instantiate(prefab, position, Quaternion.identity);
        created.Add(go);
        Physics.SyncTransforms();
        return go;
    }

    private Rigidbody SpawnBall(Vector3 position) => Spawn(ballPrefab, position).GetComponent<Rigidbody>();

    private GameObject SpawnHoop()
    {
        GameObject hoopPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HoopPrefabPath);
        Assert.IsNotNull(hoopPrefab, "Hoop prefab not found. Run Tools > AR Basketball > Create Hoop Prefab.");
        return Spawn(hoopPrefab, TestOrigin);
    }

    private void CreateFloor(Vector3 topCentre)
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        created.Add(floor);
        floor.name = "TestFloor";
        floor.transform.position = topCentre - Vector3.up * 0.1f;
        floor.transform.localScale = new Vector3(10f, 0.2f, 10f);

        PhysicsMaterial material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(FloorMaterialPath);
        Assert.IsNotNull(material, $"{FloorMaterialPath} not found. Run the Create Basketball Prefab tool.");
        floor.GetComponent<Collider>().sharedMaterial = material;
        Physics.SyncTransforms();
    }

    private Transform CreateHoldPoint()
    {
        GameObject holdPoint = new GameObject("TestHoldPoint");
        created.Add(holdPoint);
        holdPoint.transform.position = TestOrigin + Vector3.up * 2f;
        return holdPoint.transform;
    }

    private static Component GetController(GameObject ball)
    {
        Component controller = ball.GetComponent("BasketballController");
        Assert.IsNotNull(controller, "BasketballController missing on the ball prefab.");
        return controller;
    }

    private static object Call(Component target, string method, Type[] argTypes, params object[] args)
    {
        MethodInfo info = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public, null, argTypes, null);
        Assert.IsNotNull(info, $"{target.GetType().Name}.{method} not found - is the script up to date?");
        return info.Invoke(target, args);
    }

    private static object Get(Component target, string property)
    {
        PropertyInfo info = target.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(info, $"{target.GetType().Name}.{property} not found - is the script up to date?");
        return info.GetValue(target);
    }

    /// <summary>Start velocity that reaches target after 'time' seconds, including linear damping (drag).</summary>
    private static Vector3 BallisticVelocity(Vector3 start, Vector3 target, float time, float damping)
    {
        Vector3 g = Physics.gravity;
        Vector3 delta = target - start;
        if (damping < 1e-4f) return (delta - 0.5f * time * time * g) / time;

        // x(t) = g t / k + (v0 - g / k)(1 - e^(-k t)) / k   solved for v0
        float e = 1f - Mathf.Exp(-damping * time);
        return g / damping + (delta - g * time / damping) * damping / e;
    }

    private static void Simulate(float seconds, Action afterStep)
    {
        float step = Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : 0.02f;
        int steps = Mathf.CeilToInt(seconds / step);
        for (int i = 0; i < steps; i++)
        {
            Physics.Simulate(step);
            afterStep?.Invoke();
        }
    }
}
