using System.Linq;
using NUnit.Framework;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// STAGE 1 automated checks. Run them from Window > General > Test Runner > EditMode > Run All.
/// They check the project settings and scene wiring from the Stage 1 guide.
/// They can't check the real camera or plane detection; test those with
/// XR Simulation and on the phone.
/// </summary>
public class Stage1SetupTests
{
    private const string ScenePath = "Assets/Scenes/ARBasketballScene.unity";

    private Scene scene;
    private bool openedByTest;

    [OneTimeSetUp]
    public void OpenScene()
    {
        Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath),
            $"Scene not found at {ScenePath}. Save your scene with exactly this name.");

        scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            openedByTest = true;
        }
    }

    [OneTimeTearDown]
    public void CloseScene()
    {
        if (openedByTest && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
    }

    private T FindInScene<T>() where T : Component
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true))
            .FirstOrDefault();
    }

    // ---------------------------------------------------------------- project settings

    [Test]
    public void BuildTarget_IsAndroid()
    {
        Assert.AreEqual(BuildTarget.Android, EditorUserBuildSettings.activeBuildTarget,
            "Switch platform: File > Build Profiles > Android > Switch Platform.");
    }

    [Test]
    public void ARCoreLoader_IsEnabledForAndroid()
    {
        var settings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
        bool arCoreOn = settings != null && settings.Manager != null &&
                        settings.Manager.activeLoaders.Any(l => l != null && l.GetType().Name.Contains("ARCore"));
        Assert.IsTrue(arCoreOn, "Tick Google ARCore in Project Settings > XR Plug-in Management > Android tab.");
    }

    [Test]
    public void PlayerSettings_MatchARCoreRequirements()
    {
        Assert.GreaterOrEqual((int)PlayerSettings.Android.minSdkVersion, 24,
            "Minimum API Level must be Android 7.0 (API 24) or higher.");
        Assert.AreEqual(ScriptingImplementation.IL2CPP, PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android),
            "Scripting Backend must be IL2CPP.");
        Assert.IsTrue((PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) != 0,
            "Target Architectures must include ARM64.");
        Assert.AreEqual(UIOrientation.Portrait, PlayerSettings.defaultInterfaceOrientation,
            "Default Orientation should be Portrait.");
    }

    [Test]
    public void GraphicsApi_IsOpenGLES3First()
    {
        Assert.IsFalse(PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android),
            "Untick Auto Graphics API in Player > Android > Other Settings.");
        GraphicsDeviceType[] apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
        Assert.IsTrue(apis.Length > 0 && apis[0] == GraphicsDeviceType.OpenGLES3,
            "OpenGLES3 must be the first (preferably only) Graphics API.");
    }

    [Test]
    public void UrpRenderers_HaveARBackgroundFeature()
    {
        // The URP template keeps its renderers in Assets/Settings. XR Simulation samples bring their own, so skip those.
        string folder = AssetDatabase.IsValidFolder("Assets/Settings") ? "Assets/Settings" : "Assets";
        string[] guids = AssetDatabase.FindAssets("t:UniversalRendererData", new[] { folder });
        if (guids.Length == 0) Assert.Ignore("No URP renderers found (Built-in pipeline) - this check does not apply.");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            bool hasFeature = data != null && data.rendererFeatures.Any(f => f != null && f is ARBackgroundRendererFeature);
            Assert.IsTrue(hasFeature, $"{path} is missing 'AR Background Renderer Feature' (causes a black camera screen).");
        }
    }

    // ---------------------------------------------------------------- scene wiring

    [Test]
    public void Scene_HasARSession()
    {
        Assert.IsNotNull(FindInScene<ARSession>(), "Add GameObject > XR > AR Session.");
    }

    [Test]
    public void XROrigin_HasRequiredManagers()
    {
        XROrigin origin = FindInScene<XROrigin>();
        Assert.IsNotNull(origin, "Add GameObject > XR > XR Origin (Mobile AR).");

        ARPlaneManager planes = origin.GetComponent<ARPlaneManager>();
        Assert.IsNotNull(planes, "Add AR Plane Manager to the XR Origin.");
        Assert.IsTrue((planes.requestedDetectionMode & PlaneDetectionMode.Horizontal) != 0,
            "AR Plane Manager Detection Mode must include Horizontal.");
        Assert.IsNotNull(planes.planePrefab, "Assign the ARPlane prefab to AR Plane Manager > Plane Prefab.");

        Assert.IsNotNull(origin.GetComponent<ARRaycastManager>(), "Add AR Raycast Manager to the XR Origin.");
        Assert.IsNotNull(origin.GetComponent<ARAnchorManager>(), "Add AR Anchor Manager to the XR Origin.");

        Assert.Less(origin.transform.position.sqrMagnitude, 1e-6f, "Keep the XR Origin at position 0,0,0.");
    }

    [Test]
    public void ARCamera_IsConfigured()
    {
        XROrigin origin = FindInScene<XROrigin>();
        Assert.IsNotNull(origin, "XR Origin missing.");
        Camera cam = origin.Camera;
        Assert.IsNotNull(cam, "XR Origin has no Camera assigned.");
        Assert.IsTrue(cam.CompareTag("MainCamera"), "The AR camera must be tagged MainCamera.");
        Assert.IsNotNull(cam.GetComponent<ARCameraManager>(), "AR camera needs AR Camera Manager.");
        Assert.IsNotNull(cam.GetComponent<ARCameraBackground>(), "AR camera needs AR Camera Background.");

        int cameraCount = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)).Count();
        Assert.AreEqual(1, cameraCount, "Delete the default Main Camera - only the XR Origin camera should exist.");
    }

    [Test]
    public void ARPlanePlacement_IsWired()
    {
        MonoBehaviour placement = scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true))
            .FirstOrDefault(m => m != null && m.GetType().Name == "ARPlanePlacement");
        Assert.IsNotNull(placement, "Add the ARPlanePlacement script to the GameSystems object.");

        var so = new SerializedObject(placement);
        Assert.IsNotNull(so.FindProperty("hoopPrefab").objectReferenceValue, "Assign Hoop Prefab.");
        Assert.IsNotNull(so.FindProperty("placementIndicatorPrefab").objectReferenceValue, "Assign Placement Indicator Prefab.");

        GameObject indicator = (GameObject)so.FindProperty("placementIndicatorPrefab").objectReferenceValue;
        Assert.IsEmpty(indicator.GetComponentsInChildren<Collider>(true),
            "The PlacementIndicator prefab must not have any colliders.");
    }

    [Test]
    public void RepositionButton_CallsRepositionHoop()
    {
        Button button = scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<Button>(true))
            .FirstOrDefault(b => b.name == "RepositionButton");
        Assert.IsNotNull(button, "Create a UI Button named RepositionButton.");

        bool wired = Enumerable.Range(0, button.onClick.GetPersistentEventCount())
            .Any(i => button.onClick.GetPersistentMethodName(i) == "RepositionHoop" &&
                      button.onClick.GetPersistentTarget(i) != null);
        Assert.IsTrue(wired, "RepositionButton > On Click () must call ARPlanePlacement.RepositionHoop.");
    }

    [Test]
    public void EventSystem_UsesInputSystemModule()
    {
        EventSystem eventSystem = FindInScene<EventSystem>();
        Assert.IsNotNull(eventSystem, "The scene needs an EventSystem (created with the Canvas).");
        bool newModule = eventSystem.GetComponents<BaseInputModule>()
            .Any(m => m != null && m.GetType().Name == "InputSystemUIInputModule");
        Assert.IsTrue(newModule, "Select EventSystem and click 'Replace with InputSystemUIInputModule'.");
    }
}
