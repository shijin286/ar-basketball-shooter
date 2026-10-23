using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// STAGE 2 automated checks for Assets/Prefabs/BasketballHoop.prefab.
/// Run them from Window > General > Test Runner > EditMode > Run All.
///
/// Besides checking the prefab's structure, these tests run real Unity physics
/// in Edit Mode (Physics.Simulate) with a 0.24 m test ball:
///  - a ball dropped through the centre falls through the net,
///  - a ball dropped onto the rim bounces off,
///  - a ball thrown at the backboard bounces back.
/// The test objects are created 1 km away from your scene and deleted afterwards.
/// </summary>
public class Stage2HoopTests
{
    private const string PrefabPath = "Assets/Prefabs/BasketballHoop.prefab";
    private const float BallRadius = 0.12f;
    private static readonly Vector3 TestOrigin = new Vector3(1000f, 0f, 1000f);

    private readonly List<Object> created = new List<Object>();
    private GameObject hoop;
    private Transform rim;
    private PhysicsMaterial ballMaterial;
    private SimulationMode previousMode;

    [SetUp]
    public void SetUp()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Assert.Fail("Hoop prefab not found. Run Tools > AR Basketball > Create Hoop Prefab first.");
        }

        previousMode = Physics.simulationMode;
        Physics.simulationMode = SimulationMode.Script;

        hoop = Object.Instantiate(prefab, TestOrigin, Quaternion.identity);
        created.Add(hoop);
        rim = hoop.transform.Find("Rim");

        ballMaterial = new PhysicsMaterial("TestBall") { bounciness = 0.8f, dynamicFriction = 0.6f, staticFriction = 0.6f };
        created.Add(ballMaterial);

        Physics.SyncTransforms();
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

    // ---------------------------------------------------------------- structure

    [Test]
    public void Root_HasKinematicRigidbody()
    {
        Rigidbody body = hoop.GetComponent<Rigidbody>();
        Assert.IsNotNull(body, "The hoop root needs a Rigidbody.");
        Assert.IsTrue(body.isKinematic, "The hoop Rigidbody must be Is Kinematic.");
    }

    [Test]
    public void Rim_ExistsInFrontAndAboveFloor()
    {
        Assert.IsNotNull(rim, "Child 'Rim' not found.");
        Assert.Greater(rim.localPosition.z, 0.2f, "The rim must stick out towards +Z (the player side).");
        Assert.Greater(rim.localPosition.y, 0.5f, "The rim is too low.");
    }

    [Test]
    public void Rim_HasSolidCapsuleRing()
    {
        Assert.IsNotNull(rim, "Child 'Rim' not found.");
        CapsuleCollider[] capsules = rim.GetComponentsInChildren<CapsuleCollider>();
        Assert.GreaterOrEqual(capsules.Length, 8, "The rim needs at least 8 capsule colliders.");
        Assert.IsTrue(capsules.All(c => !c.isTrigger), "Rim colliders must NOT be triggers.");
        Assert.IsTrue(capsules.All(c => c.sharedMaterial != null), "Rim colliders need the Rim_Physics material.");
    }

    [Test]
    public void Rim_OpeningFitsTheBall()
    {
        Assert.IsNotNull(rim, "Child 'Rim' not found.");
        foreach (CapsuleCollider capsule in rim.GetComponentsInChildren<CapsuleCollider>())
        {
            Vector3 local = rim.InverseTransformPoint(capsule.transform.TransformPoint(capsule.center));
            local.y = 0f;
            float clearance = local.magnitude - capsule.radius;
            Assert.Greater(clearance, BallRadius + 0.03f, $"{capsule.name} leaves too little room for the ball.");
        }
    }

    [Test]
    public void Net_HasNoCollider()
    {
        Assert.IsNotNull(rim, "Child 'Rim' not found.");
        Transform net = rim.Find("Net");
        Assert.IsNotNull(net, "Child 'Rim/Net' not found.");
        Assert.IsEmpty(net.GetComponentsInChildren<Collider>(), "The net must not have a collider.");
    }

    [Test]
    public void Backboard_HasSolidCollider()
    {
        Transform board = hoop.transform.Find("Backboard/BoardPanel");
        Assert.IsNotNull(board, "Child 'Backboard/BoardPanel' not found.");
        BoxCollider box = board.GetComponent<BoxCollider>();
        Assert.IsNotNull(box, "The backboard needs a Box Collider.");
        Assert.IsFalse(box.isTrigger, "The backboard collider must NOT be a trigger.");
        Assert.IsNotNull(box.sharedMaterial, "The backboard needs the Backboard_Physics material.");
    }

    // ---------------------------------------------------------------- physics

    [Test]
    public void Ball_DroppedThroughCentre_FallsThroughNet()
    {
        Assert.IsNotNull(rim, "Child 'Rim' not found.");
        Rigidbody ball = SpawnBall(rim.position + Vector3.up * 0.6f);

        Simulate(1.0f, ball, null);

        Assert.Less(ball.position.y, rim.position.y - 0.5f, "The ball did not fall through the hoop.");
        Vector3 offset = ball.position - rim.position;
        offset.y = 0f;
        Assert.Less(offset.magnitude, 0.1f, "The ball was deflected - something is blocking the opening.");
    }

    [Test]
    public void Ball_DroppedOnRim_BouncesOff()
    {
        Assert.IsNotNull(rim, "Child 'Rim' not found.");
        float ringRadius = rim.GetComponentsInChildren<CapsuleCollider>()
            .Select(c =>
            {
                Vector3 p = rim.InverseTransformPoint(c.transform.TransformPoint(c.center));
                p.y = 0f;
                return p.magnitude;
            })
            .Average();

        Vector3 frontOfRim = rim.TransformPoint(new Vector3(0f, 0f, ringRadius));
        Rigidbody ball = SpawnBall(frontOfRim + Vector3.up * 0.5f);

        bool bounced = false;
        Simulate(1.0f, ball, b => { if (b.linearVelocity.y > 0.2f) bounced = true; });

        Assert.IsTrue(bounced, "The ball went straight through the rim - check the rim colliders.");
    }

    [Test]
    public void Ball_ThrownAtBackboard_BouncesBack()
    {
        Transform board = hoop.transform.Find("Backboard");
        Assert.IsNotNull(board, "Child 'Backboard' not found.");

        Vector3 forward = hoop.transform.forward;
        Rigidbody ball = SpawnBall(board.position + forward * 1.0f + Vector3.up * 0.15f);
        ball.useGravity = false;
        ball.linearVelocity = -forward * 6f;

        Simulate(0.6f, ball, null);

        Assert.Greater(Vector3.Dot(ball.linearVelocity, forward), 0.5f, "The ball did not bounce off the backboard.");
    }

    // ---------------------------------------------------------------- helpers

    private Rigidbody SpawnBall(Vector3 position)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        created.Add(go);
        go.name = "TestBall";
        go.transform.position = position;
        go.transform.localScale = Vector3.one * BallRadius * 2f;
        go.GetComponent<SphereCollider>().sharedMaterial = ballMaterial;

        Rigidbody body = go.AddComponent<Rigidbody>();
        body.mass = 0.6f;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        Physics.SyncTransforms();
        return body;
    }

    private static void Simulate(float seconds, Rigidbody ball, System.Action<Rigidbody> afterStep)
    {
        float step = Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : 0.02f;
        int steps = Mathf.CeilToInt(seconds / step);
        for (int i = 0; i < steps; i++)
        {
            Physics.Simulate(step);
            afterStep?.Invoke(ball);
        }
    }
}
