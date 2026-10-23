using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// STAGE 10 automated checks (EditMode tab) for the laptop version:
///  - DesktopMode switches on only in laptop builds (not on the phone, not in the Editor);
///  - the virtual court: floor top at y = 0, four walls, lines without colliders, wooden texture;
///  - DesktopCameraRig maths: walking direction, staying inside the walls, look limits;
///  - SafeArea keeps the phone UI as a centred column on a wide screen;
///  - ARPlanePlacement places the hoop on the court floor, turned to face the player;
///  - the scene has DesktopMode on GameSystems with every reference assigned.
///
/// Game scripts live in Unity's default assembly, so they are called by name (reflection).
/// </summary>
public class Stage10DesktopTests
{
    private const string ScenePath = "Assets/Scenes/ARBasketballScene.unity";
    private const string SurfaceMaterialPath = "Assets/Materials/Desktop/DesktopSurface.mat";
    private static readonly Vector3 Eye = new Vector3(0f, 1.5f, -3f);

    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object o in created)
        {
            if (o == null) continue;
            if (o is GameObject go)
            {
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.sharedMaterial == null) continue;
                    if (r.sharedMaterial.mainTexture != null) Object.DestroyImmediate(r.sharedMaterial.mainTexture);
                    Object.DestroyImmediate(r.sharedMaterial);
                }
            }
            Object.DestroyImmediate(o);
        }
        created.Clear();
    }

    // ---------------------------------------------------------------- when it switches on

    [TestCase(false, false, false, true, TestName = "DesktopMode_On_InLaptopBuild")]
    [TestCase(false, true, false, false, TestName = "DesktopMode_Off_OnPhone")]
    [TestCase(false, true, true, false, TestName = "DesktopMode_Off_OnPhone_EvenIfForced")]
    [TestCase(true, false, false, false, TestName = "DesktopMode_Off_InEditor_UsesXRSimulation")]
    [TestCase(true, false, true, true, TestName = "DesktopMode_On_InEditor_WhenForced")]
    public void DesktopMode_ShouldUse(bool isEditor, bool isMobile, bool force, bool expected)
    {
        object result = Static("DesktopMode", "ShouldUse", isEditor, isMobile, force);
        Assert.AreEqual(expected, result);
    }

    [TestCase(false, false, true, TestName = "DesktopMode_On_InBrowserOnLaptop")]
    [TestCase(false, true, true, TestName = "DesktopMode_On_InBrowserOnPhone_NoARCoreInBrowsers")]
    [TestCase(true, false, false, TestName = "DesktopMode_Off_InEditor_EvenWithWebGLTarget")]
    public void DesktopMode_ShouldUse_WebGL(bool isEditor, bool isMobile, bool expected)
    {
        object result = Static("DesktopMode", "ShouldUse", isEditor, isMobile, false, true);
        Assert.AreEqual(expected, result);
    }

    // ---------------------------------------------------------------- court

    [Test]
    public void Court_FloorTopIsAtGroundLevel_AndCoversTheCourt()
    {
        Collider floor = BuildCourt(14f);
        Physics.SyncTransforms();

        Assert.AreEqual(0f, floor.bounds.max.y, 0.001f, "The hoop stands at y = 0, so the floor top must be there too.");
        Assert.AreEqual(14f, floor.bounds.size.x, 0.01f);
        Assert.AreEqual(14f, floor.bounds.size.z, 0.01f);
        Assert.IsFalse(floor.isTrigger, "The ball must bounce on the floor.");
    }

    [Test]
    public void Court_OnlyFloorAndFourWallsHaveColliders()
    {
        Collider floor = BuildCourt(14f);
        Collider[] colliders = floor.transform.parent.GetComponentsInChildren<Collider>(true);

        CollectionAssert.AreEquivalent(new[] { "CourtFloor", "Wall0", "Wall1", "Wall2", "Wall3" }, colliders.Select(c => c.name).ToArray(),
            "Lines and wall pads are decoration only - a collider on them would make the ball jump.");
    }

    [Test]
    public void Court_WallsSurroundTheFloor()
    {
        Collider floor = BuildCourt(14f);
        Physics.SyncTransforms();
        Transform root = floor.transform.parent;

        foreach (string wall in new[] { "Wall0", "Wall1", "Wall2", "Wall3" })
        {
            Bounds b = root.Find(wall).GetComponent<Collider>().bounds;
            float distance = Mathf.Max(Mathf.Abs(b.center.x), Mathf.Abs(b.center.z));
            Assert.AreEqual(7.1f, distance, 0.05f, $"{wall} should stand just outside the 14 m floor.");
            Assert.Greater(b.size.y, 4f, $"{wall} should be tall enough to stop high shots.");
            Assert.AreEqual(0f, b.min.y, 0.01f, $"{wall} should start at the floor.");
        }
    }

    [Test]
    public void WoodTexture_TilesAndShowsPlanks()
    {
        var texture = (Texture2D)Static("DesktopMode", "CreateWoodTexture", 256);
        created.Add(texture);

        Assert.AreEqual(256, texture.width);
        Assert.AreEqual(TextureWrapMode.Repeat, texture.wrapMode, "The texture repeats across the floor.");
        float[] brightness = texture.GetPixels().Select(c => c.grayscale).ToArray();
        Assert.Greater(brightness.Max() - brightness.Min(), 0.15f, "Planks, grain and gaps should be visible, not one flat colour.");
    }

    // ---------------------------------------------------------------- camera rig

    [Test]
    public void CameraRig_WalksWhereThePlayerLooks()
    {
        var right = (Vector3)Static("DesktopCameraRig", "MoveDirection", 90f, new Vector2(0f, 1f));
        AssertNear(new Vector3(1f, 0f, 0f), right, "Looking along +X, W should walk along +X.");

        var back = (Vector3)Static("DesktopCameraRig", "MoveDirection", 0f, new Vector2(0f, -1f));
        AssertNear(new Vector3(0f, 0f, -1f), back, "S walks backwards.");

        var diagonal = (Vector3)Static("DesktopCameraRig", "MoveDirection", 30f, new Vector2(1f, 1f));
        Assert.LessOrEqual(diagonal.magnitude, 1.0001f, "W + D together must not walk faster.");
        Assert.AreEqual(0f, diagonal.y, 0.0001f, "Walking never changes height.");
    }

    [Test]
    public void CameraRig_StaysInsideTheWalls()
    {
        var clamped = (Vector3)Static("DesktopCameraRig", "ClampToRoom", new Vector3(10f, 1.5f, -9f), 6.5f);
        AssertNear(new Vector3(6.5f, 1.5f, -6.5f), clamped, "Outside the court -> moved back to the edge.");

        var inside = (Vector3)Static("DesktopCameraRig", "ClampToRoom", new Vector3(1f, 1.5f, 2f), 6.5f);
        AssertNear(new Vector3(1f, 1.5f, 2f), inside, "Inside the court -> unchanged.");
    }

    [TestCase(100f, 75f)]
    [TestCase(-80f, -35f)]
    [TestCase(28f, 28f)]
    public void CameraRig_LookAngleIsLimited(float pitch, float expected)
    {
        Assert.AreEqual(expected, (float)Static("DesktopCameraRig", "ClampPitch", pitch, -35f, 75f), 0.0001f);
    }

    [Test]
    public void CameraRig_TurnsTowardsTheHoop()
    {
        var ahead = (Vector2)Static("DesktopCameraRig", "YawPitchTowards", Eye, new Vector3(0f, 1.5f, 0f));
        Assert.AreEqual(0f, ahead.x, 0.01f, "Straight ahead = yaw 0.");
        Assert.AreEqual(0f, ahead.y, 0.01f, "Same height = level.");

        var right = (Vector2)Static("DesktopCameraRig", "YawPitchTowards", Eye, new Vector3(3f, 1.5f, -3f));
        Assert.AreEqual(90f, right.x, 0.01f, "A hoop to the right = yaw 90.");

        var below = (Vector2)Static("DesktopCameraRig", "YawPitchTowards", Eye, new Vector3(0f, 0f, -1.5f));
        Assert.AreEqual(45f, below.y, 0.01f, "1.5 m down and 1.5 m ahead = look 45 degrees down.");
    }

    [Test]
    public void CameraRig_LookAt_ReachesTheTargetAndCanBeTakenOver()
    {
        var camGo = new GameObject("TestRig");
        created.Add(camGo);
        Component rig = camGo.AddComponent(FindGameType("DesktopCameraRig"));
        Call(rig, "Place", new Vector3(0f, 0f, -3f), 0f, 28f, 6.5f);
        Assert.AreEqual(1.5f, camGo.transform.position.y, 0.001f, "Place puts the camera at eye height.");
        AssertNear(new Vector3(28f, 0f, 0f), camGo.transform.eulerAngles, "Place sets the look angle.");

        Call(rig, "LookAt", new Vector3(3f, 1.5f, -3f), 0.5f);
        FieldInfo progress = rig.GetType().GetField("turnProgress", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.AreEqual(0f, (float)progress.GetValue(rig), "LookAt starts a smooth turn.");
        var to = (Vector2)rig.GetType().GetField("turnTo", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(rig);
        Assert.AreEqual(90f, to.x, 0.01f, "The turn ends facing the target.");
        Assert.AreEqual(0f, to.y, 0.01f);
    }

    // ---------------------------------------------------------------- UI column

    [Test]
    public void SafeArea_WideScreen_KeepsCentredPhoneColumn()
    {
        var area = (Rect)Static("SafeArea", "LimitAspect", new Rect(0f, 0f, 1920f, 1080f), 9f / 16f);

        Assert.AreEqual(607.5f, area.width, 0.01f, "1080 high x 9/16 = 607.5 wide.");
        Assert.AreEqual(1080f, area.height, 0.01f);
        Assert.AreEqual(960f, area.center.x, 0.01f, "The column is centred.");
    }

    [Test]
    public void SafeArea_PhoneScreenOrNoLimit_Unchanged()
    {
        var phone = new Rect(0f, 80f, 1080f, 2200f);
        Assert.AreEqual(phone, (Rect)Static("SafeArea", "LimitAspect", phone, 9f / 16f), "A portrait phone is narrower than 9/16 already.");

        var wide = new Rect(0f, 0f, 1920f, 1080f);
        Assert.AreEqual(wide, (Rect)Static("SafeArea", "LimitAspect", wide, 0f), "0 = no limit (the phone build).");
    }

    // ---------------------------------------------------------------- hoop placement on the court

    [Test]
    public void Placement_ClickOnCourtFloor_PlacesHoopFacingThePlayer()
    {
        Component placement = MakePlacement(out Camera cam);
        Collider floor = BuildCourt(14f);
        Call(placement, "UseDesktopFloor", floor);
        Physics.SyncTransforms();

        Vector3 target = new Vector3(0.5f, 0f, 0f);
        bool hit = TryFloorPose(placement, new Ray(cam.transform.position, target - cam.transform.position), out Pose pose);

        Assert.IsTrue(hit, "A ray from eye height to the floor should give a placement.");
        AssertNear(target, pose.position, "The hoop goes where the player clicked.");
        Vector3 toPlayer = Vector3.ProjectOnPlane(cam.transform.position - pose.position, Vector3.up).normalized;
        Assert.Greater(Vector3.Dot(pose.rotation * Vector3.forward, toPlayer), 0.99f, "The hoop's front (+Z) must face the player.");
        Assert.AreEqual(0f, Vector3.Angle(pose.rotation * Vector3.up, Vector3.up), 0.01f, "The hoop stands upright.");
        Assert.IsTrue((bool)Get(placement, "UsesDesktopFloor"));
    }

    [Test]
    public void Placement_LookingUpOrNoCourt_PlacesNothing()
    {
        Component placement = MakePlacement(out Camera cam);
        Collider floor = BuildCourt(14f);
        Call(placement, "UseDesktopFloor", floor);
        Physics.SyncTransforms();

        Assert.IsFalse(TryFloorPose(placement, new Ray(cam.transform.position, Vector3.up), out _), "Looking at the sky hits no floor.");

        Call(placement, "UseDesktopFloor", new object[] { null });
        Assert.IsFalse(TryFloorPose(placement, new Ray(cam.transform.position, Vector3.down), out _), "Without the court (phone) the AR planes are used instead.");
        Assert.IsFalse((bool)Get(placement, "UsesDesktopFloor"));
    }

    // ---------------------------------------------------------------- scene

    [Test]
    public void Scene_DesktopModeIsOnGameSystemsAndWired()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedByTest = !scene.IsValid() || !scene.isLoaded;
        if (openedByTest) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            GameObject systems = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "GameSystems");
            Assert.IsNotNull(systems, "GameSystems missing.");
            Component desktop = systems.GetComponents<MonoBehaviour>().FirstOrDefault(m => m != null && m.GetType().Name == "DesktopMode");
            Assert.IsNotNull(desktop, "Run Tools > AR Basketball > Add Desktop Mode To Scene.");

            var so = new SerializedObject(desktop);
            foreach (string field in new[] { "placement", "playerCamera", "surfaceMaterial", "floorPhysics", "placeHoopText", "aimShootText", "howToText", "subtitleText" })
            {
                Assert.IsNotNull(so.FindProperty(field).objectReferenceValue, $"DesktopMode > {ObjectNames.NicifyVariableName(field)} is not assigned.");
            }
            Assert.IsFalse(so.FindProperty("forceInEditor").boolValue, "Force In Editor must be off, or Play mode stops using XR Simulation.");
        }
        finally
        {
            if (openedByTest) EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void SurfaceMaterial_UsesURPLit()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(SurfaceMaterialPath);
        Assert.IsNotNull(material, $"{SurfaceMaterialPath} missing - run Tools > AR Basketball > Add Desktop Mode To Scene.");
        Assert.AreEqual("Universal Render Pipeline/Lit", material.shader.name, "A URP material, so the court renders in the build.");
    }

    // ---------------------------------------------------------------- helpers

    private Collider BuildCourt(float size)
    {
        var floor = (Collider)Static("DesktopMode", "BuildCourt", size, 5f, null, null);
        created.Add(floor.transform.parent.gameObject);
        return floor;
    }

    private Component MakePlacement(out Camera cam)
    {
        var camGo = new GameObject("TestCamera");
        created.Add(camGo);
        cam = camGo.AddComponent<Camera>();
        cam.transform.position = Eye;

        var go = new GameObject("TestPlacement");
        created.Add(go);
        Component placement = go.AddComponent(FindGameType("ARPlanePlacement")); // Awake doesn't run in EditMode
        placement.GetType().GetField("arCamera", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(placement, cam);
        return placement;
    }

    private static bool TryFloorPose(Component placement, Ray ray, out Pose pose)
    {
        object[] args = { ray, null };
        bool hit = (bool)placement.GetType().GetMethod("TryGetDesktopFloorPose").Invoke(placement, args);
        pose = (Pose)args[1];
        return hit;
    }

    private static Type FindGameType(string name)
    {
        Type type = Type.GetType(name + ", Assembly-CSharp") ??
                    AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).FirstOrDefault(t => t != null);
        Assert.IsNotNull(type, $"Script {name} not found - is it in Assets/Scripts?");
        return type;
    }

    private static object Static(string typeName, string method, params object[] args)
    {
        MethodInfo info = FindGameType(typeName).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == method && m.GetParameters().Length == args.Length);
        Assert.IsNotNull(info, $"{typeName}.{method} not found - is the script up to date?");
        return info.Invoke(null, args);
    }

    private static void Call(Component target, string method, params object[] args)
    {
        MethodInfo info = target.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(info, $"{target.GetType().Name}.{method} not found - is the script up to date?");
        info.Invoke(target, args);
    }

    private static object Get(Component target, string property) =>
        target.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance).GetValue(target);

    private static void AssertNear(Vector3 expected, Vector3 actual, string message)
    {
        Assert.Less(Vector3.Distance(expected, actual), 0.001f, $"{message} Expected {expected}, got {actual}.");
    }
}
