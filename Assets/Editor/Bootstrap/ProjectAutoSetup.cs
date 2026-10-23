using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;
using Object = UnityEngine.Object;

/// <summary>
/// Automates the manual steps of the Stage 1-9 guides for a fresh project.
/// Run from the command line in two passes (the Input System switch needs an editor restart):
///   -executeMethod ProjectAutoSetup.Phase1   project settings, TMP essentials, URP, XR loaders
///   -executeMethod ProjectAutoSetup.Phase2   prefabs (builders), scene, UI, wiring, build list, icon
/// </summary>
public static class ProjectAutoSetup
{
    private const string ScenePath = "Assets/Scenes/ARBasketballScene.unity";

    // ================================================================ PHASE 1

    public static void Phase1()
    {
        Run("Phase 1", () =>
        {
            SetupPlayerSettings();
            ImportTmpEssentials();
            SetupUrp();
            SetupXr();
        });
    }

    private static void SetupPlayerSettings()
    {
        PlayerSettings.companyName = "AR Basketball Student";
        PlayerSettings.productName = "AR Basketball Shooter";
        PlayerSettings.bundleVersion = "1.0";
        PlayerSettings.Android.bundleVersionCode = 1;
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.student.arbasketball");
        if ((int)PlayerSettings.Android.minSdkVersion < 24) PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)24;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
        PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;

        // Active Input Handling = Input System Package (New). Takes effect after an editor restart.
        Object projectSettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").FirstOrDefault();
        var so = new SerializedObject(projectSettings);
        SerializedProperty input = so.FindProperty("activeInputHandler");
        if (input != null)
        {
            input.intValue = 1;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        Log("Player settings done (package com.student.arbasketball, API 24+, IL2CPP ARM64, portrait, GLES3, Input System).");
    }

    private static void ImportTmpEssentials()
    {
        if (AssetDatabase.FindAssets("t:TMP_Settings").Length > 0)
        {
            Log("TMP Essentials already imported.");
            return;
        }

        var ugui = UnityEditor.PackageManager.PackageInfo.FindForPackageName("com.unity.ugui");
        string package = Path.Combine(ugui.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage");
        if (!File.Exists(package)) throw new Exception("TMP Essential Resources not found at " + package);

        AssetDatabase.ImportPackage(package, false);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Log("TMP Essentials imported.");
    }

    private static void SetupUrp()
    {
        EnsureFolder("Assets/Settings");
        const string rendererPath = "Assets/Settings/Mobile_Renderer.asset";
        const string assetPath = "Assets/Settings/Mobile_RPAsset.asset";

        var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
        if (rendererData == null)
        {
            rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(rendererData, rendererPath);
        }

        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
        if (pipeline == null)
        {
            pipeline = UniversalRenderPipelineAsset.Create(rendererData);
            AssetDatabase.CreateAsset(pipeline, assetPath);
        }

        if (!rendererData.rendererFeatures.Any(f => f is ARBackgroundRendererFeature))
        {
            var feature = ScriptableObject.CreateInstance<ARBackgroundRendererFeature>();
            feature.name = "ARBackgroundRendererFeature";
            AssetDatabase.AddObjectToAsset(feature, rendererData);
            AssetDatabase.SaveAssets();
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);

            // Same two lists the Renderer inspector updates when you press "Add Renderer Feature".
            var so = new SerializedObject(rendererData);
            SerializedProperty features = so.FindProperty("m_RendererFeatures");
            SerializedProperty map = so.FindProperty("m_RendererFeatureMap");
            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rendererData);
        }

        GraphicsSettings.defaultRenderPipeline = pipeline;
        int current = QualitySettings.GetQualityLevel();
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = pipeline;
        }
        QualitySettings.SetQualityLevel(current, false);
        AssetDatabase.SaveAssets();
        Log($"URP active with AR Background Renderer Feature ({rendererData.rendererFeatures.Count} feature(s)).");
    }

    private static void SetupXr()
    {
        EnsureFolder("Assets/XR");
        if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget) || perTarget == null)
        {
            perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            AssetDatabase.CreateAsset(perTarget, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
        }

        AssignLoader(perTarget, BuildTargetGroup.Android, "UnityEngine.XR.ARCore.ARCoreLoader");
        AssignLoader(perTarget, BuildTargetGroup.Standalone, "UnityEngine.XR.Simulation.SimulationLoader");
        AssetDatabase.SaveAssets();
    }

    private static void AssignLoader(XRGeneralSettingsPerBuildTarget perTarget, BuildTargetGroup group, string loader)
    {
        XRGeneralSettings general = perTarget.SettingsForBuildTarget(group);
        if (general == null)
        {
            general = ScriptableObject.CreateInstance<XRGeneralSettings>();
            general.name = $"{group} Settings";
            AssetDatabase.AddObjectToAsset(general, perTarget);
            perTarget.SetSettingsForBuildTarget(group, general);
        }
        if (general.AssignedSettings == null)
        {
            var manager = ScriptableObject.CreateInstance<XRManagerSettings>();
            manager.name = $"{group} Providers";
            AssetDatabase.AddObjectToAsset(manager, perTarget);
            general.AssignedSettings = manager;
        }
        general.InitManagerOnStart = true;
        EditorUtility.SetDirty(general);
        EditorUtility.SetDirty(perTarget);

        bool ok = general.AssignedSettings.activeLoaders.Any(l => l != null && l.GetType().FullName == loader)
                  || XRPackageMetadataStore.AssignLoader(general.AssignedSettings, loader, group);
        Log($"XR {group}: {loader} -> {(ok ? "enabled" : "FAILED")}");
        if (!ok) throw new Exception($"Could not enable {loader} for {group}.");
    }

    // ================================================================ PHASE 2

    public static void Phase2()
    {
        Run("Phase 2", () =>
        {
            BuildPlanePrefab();
            BuildIndicatorPrefab();
            RunWizard(typeof(HoopPrefabBuilder));
            RunWizard(typeof(BasketballPrefabBuilder));
            RunWizard(typeof(EffectsPrefabBuilder));
            BuildScene();
            SetIcon();
        });
    }

    private static void BuildPlanePrefab()
    {
        const string path = "Assets/Prefabs/ARPlane.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        EnsureFolder("Assets/Prefabs");

        GameObject plane = CreateFromMenu("GameObject/XR/AR Default Plane");
        if (plane == null)
        {
            plane = new GameObject("ARPlane");
            plane.AddComponent<ARPlane>();
            plane.AddComponent<MeshFilter>();
            plane.AddComponent<MeshCollider>();
            plane.AddComponent<MeshRenderer>().sharedMaterial = Unlit("ARPlaneMat", new Color(1f, 1f, 1f));
            plane.AddComponent<LineRenderer>();
            plane.AddComponent<ARPlaneMeshVisualizer>();
        }
        plane.name = "ARPlane";
        PrefabUtility.SaveAsPrefabAsset(plane, path);
        Object.DestroyImmediate(plane);
        Log("ARPlane prefab saved.");
    }

    private static void BuildIndicatorPrefab()
    {
        const string path = "Assets/Prefabs/PlacementIndicator.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

        var root = new GameObject("PlacementIndicator");
        GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "Disc";
        disc.transform.SetParent(root.transform, false);
        disc.transform.localPosition = new Vector3(0f, 0.002f, 0f);
        disc.transform.localScale = new Vector3(0.25f, 0.002f, 0.25f);
        Object.DestroyImmediate(disc.GetComponent<Collider>());
        disc.GetComponent<Renderer>().sharedMaterial = Unlit("IndicatorMat", new Color(0.2f, 0.9f, 1f));
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        Log("PlacementIndicator prefab saved.");
    }

    private static void RunWizard(Type wizardType)
    {
        var wizard = ScriptableObject.CreateInstance(wizardType);
        MethodInfo create = wizardType.GetMethod("OnWizardCreate", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        create.Invoke(wizard, null);
        Object.DestroyImmediate(wizard);
        Log($"{wizardType.Name} ran.");
    }

    // ---------------------------------------------------------------- scene

    private static TMP_DefaultControls.Resources uiResources;

    private static void BuildScene()
    {
        EnsureFolder("Assets/Scenes");
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var light = new GameObject("Directional Light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.shadows = LightShadows.Soft;
        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // --- AR
        GameObject session = CreateFromMenu("GameObject/XR/AR Session");
        if (session == null)
        {
            session = new GameObject("AR Session");
            session.AddComponent<ARSession>();
            session.AddComponent<ARInputManager>();
        }
        session.name = "AR Session";

        GameObject originGo = CreateFromMenu("GameObject/XR/XR Origin (Mobile AR)");
        if (originGo == null) throw new Exception("Menu 'GameObject/XR/XR Origin (Mobile AR)' not found.");
        originGo.transform.position = Vector3.zero;
        var origin = originGo.GetComponent<XROrigin>();
        var planeManager = GetOrAdd<ARPlaneManager>(originGo);
        planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal;
        planeManager.planePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ARPlane.prefab");
        var raycastManager = GetOrAdd<ARRaycastManager>(originGo);
        GetOrAdd<ARAnchorManager>(originGo);
        Camera cam = origin.Camera;
        cam.gameObject.tag = "MainCamera";
        GetOrAdd<AudioListener>(cam.gameObject);

        // --- game systems
        var systems = new GameObject("GameSystems");
        var placement = systems.AddComponent<ARPlanePlacement>();
        var spawner = systems.AddComponent<BallSpawner>();
        var shooter = systems.AddComponent<BasketballShooter>();
        var game = systems.AddComponent<GameManager>();
        var audio = systems.AddComponent<AudioManager>();
        var effects = systems.AddComponent<ScoreEffects>();
        systems.AddComponent<MobileSettings>();

        // --- UI
        uiResources = new TMP_DefaultControls.Resources
        {
            standard = LoadSprite("UI/Skin/UISprite.psd"),
            background = LoadSprite("UI/Skin/Background.psd"),
            inputField = LoadSprite("UI/Skin/InputFieldBackground.psd"),
            knob = LoadSprite("UI/Skin/Knob.psd"),
            checkmark = LoadSprite("UI/Skin/Checkmark.psd"),
            dropdown = LoadSprite("UI/Skin/DropdownArrow.psd"),
            mask = LoadSprite("UI/Skin/UIMask.psd"),
        };

        var canvasGo = new GameObject("Canvas", typeof(RectTransform));
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        var ui = canvasGo.AddComponent<UIManager>();

        var eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<InputSystemUIInputModule>();

        Transform safe = Stretch(new GameObject("SafeArea", typeof(RectTransform)), canvasGo.transform);
        safe.gameObject.AddComponent<SafeArea>();

        // HUD
        Transform hud = Stretch(new GameObject("HUD", typeof(RectTransform)), safe);
        var topBar = Panel(hud, "TopBar", new Color(0f, 0f, 0f, 0.45f), false);
        Place(topBar.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 230f));
        TMP_Text score = MakeText(hud, "ScoreText", "Score: 0", 60, TextAlignmentOptions.Left, Color.white, true);
        Place(score.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(450f, 100f));
        TMP_Text shots = MakeText(hud, "ShotsText", "Shots: 0", 48, TextAlignmentOptions.Right, Color.white);
        Place(shots.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(450f, 80f));
        TMP_Text accuracy = MakeText(hud, "AccuracyText", "Accuracy: 0%", 40, TextAlignmentOptions.Right, new Color(0.85f, 0.85f, 0.85f));
        Place(accuracy.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -125f), new Vector2(450f, 70f));
        TMP_Text timer = MakeText(hud, "TimerText", "1:00", 72, TextAlignmentOptions.Center, Color.white, true);
        Place(timer.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(300f, 100f));
        TMP_Text status = MakeText(hud, "StatusText", "", 44, TextAlignmentOptions.Center, Color.white);
        Place(status.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -260f), new Vector2(-80f, 150f));
        TMP_Text hint = MakeText(hud, "ShootHintText", "", 44, TextAlignmentOptions.Center, Color.white);
        Place(hint.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 330f), new Vector2(-80f, 90f));
        TMP_Text basket = MakeText(hud, "BasketMessage", "SWISH!", 130, TextAlignmentOptions.Center, new Color(1f, 0.75f, 0.15f), true);
        Place(basket.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(900f, 220f));

        Button pause = MakeButton(hud, "PauseButton", "II", new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(140f, 110f), new Color(0.15f, 0.15f, 0.2f, 0.85f), 56, game.PauseGame);
        Button resetBall = MakeButton(hud, "ResetBallButton", "Reset Ball", new Vector2(0f, 0f), new Vector2(220f, 150f), new Vector2(380f, 130f), new Color(0.15f, 0.45f, 0.85f), 44, spawner.ResetBall);
        Button reposition = MakeButton(hud, "RepositionButton", "Reposition Hoop", new Vector2(1f, 0f), new Vector2(-220f, 150f), new Vector2(380f, 130f), new Color(0.15f, 0.45f, 0.85f), 40, placement.RepositionHoop);

        // Instruction cards (never block touches)
        GameObject placeCard = Card(safe, "PlaceHoopPanel", 380f, 420f, "PLACE THE HOOP",
            "1. Point your phone at the floor\n2. Move it slowly until a grid appears\n3. <b>Tap</b> the grid to place the hoop");
        GameObject aimCard = Card(safe, "AimShootPanel", 560f, 380f, "AIM & SHOOT",
            "<b>Swipe</b> up to shoot - a faster swipe throws harder\nSwipe a little left or right to aim\nStand about 2-3 m from the hoop");

        // Title
        Image title = Panel(safe, "TitlePanel", new Color(0.04f, 0.055f, 0.12f, 0.82f), true);
        TMP_Text titleText = MakeText(title.transform, "TitleText", "AR Basketball Shooter", 110, TextAlignmentOptions.Center, new Color(1f, 0.55f, 0.12f), true);
        FitTitle(titleText);
        Place(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -380f), new Vector2(-80f, 300f));
        TMP_Text subtitle = MakeText(title.transform, "SubtitleText", "Shoot hoops in your own room!", 48, TextAlignmentOptions.Center, Color.white);
        Place(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -700f), new Vector2(-80f, 90f));
        TMP_Text howTo = MakeText(title.transform, "HowToText", "Scan the floor  •  Tap to place  •  Swipe to shoot", 38, TextAlignmentOptions.Center, new Color(0.8f, 0.8f, 0.8f));
        Place(howTo.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(-80f, 80f));
        TMP_Text best = MakeText(title.transform, "BestScoreText", "Set the first challenge record!", 40, TextAlignmentOptions.Center, new Color(0.8f, 0.8f, 0.8f));
        Place(best.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 680f), new Vector2(-80f, 70f));
        MakeButton(title.transform, "StartButton", "FREE PLAY", new Vector2(0.5f, 0f), new Vector2(0f, 520f), new Vector2(680f, 160f), new Color(1f, 0.5f, 0.1f), 62, game.StartGame);
        MakeButton(title.transform, "ChallengeButton", "60s CHALLENGE", new Vector2(0.5f, 0f), new Vector2(0f, 330f), new Vector2(680f, 160f), new Color(0.9f, 0.25f, 0.2f), 58, game.StartChallenge);
        MakeButton(title.transform, "LeaderboardButton", "LEADERBOARD", new Vector2(0.5f, 0f), new Vector2(0f, 160f), new Vector2(520f, 130f), new Color(0.25f, 0.25f, 0.3f), 46, ui.OpenLeaderboard);

        // Pause
        Image pausePanel = Panel(safe, "PausePanel", new Color(0f, 0f, 0f, 0.75f), true);
        TMP_Text paused = MakeText(pausePanel.transform, "PausedText", "PAUSED", 100, TextAlignmentOptions.Center, Color.white, true);
        Place(paused.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 300f), new Vector2(800f, 160f));
        MakeButton(pausePanel.transform, "ResumeButton", "RESUME", new Vector2(0.5f, 0.5f), new Vector2(0f, 50f), new Vector2(600f, 160f), new Color(0.2f, 0.65f, 0.3f), 56, game.ResumeGame);
        MakeButton(pausePanel.transform, "RestartButton", "RESTART GAME", new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), new Vector2(600f, 160f), new Color(0.85f, 0.35f, 0.2f), 52, game.RestartGame);

        // Game over (results)
        Image over = Panel(safe, "GameOverPanel", new Color(0f, 0f, 0f, 0.8f), true);
        TMP_Text timeUp = MakeText(over.transform, "TimeUpText", "TIME UP!", 110, TextAlignmentOptions.Center, new Color(1f, 0.55f, 0.12f), true);
        Place(timeUp.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 600f), new Vector2(900f, 180f));
        TMP_Text result = MakeText(over.transform, "ResultText", "Score: 0\nShots: 0\nAccuracy: 0%", 56, TextAlignmentOptions.Center, Color.white);
        Place(result.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 330f), new Vector2(900f, 260f));
        TMP_Text rank = MakeText(over.transform, "RankText", "", 44, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.3f));
        Place(rank.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(900f, 90f));
        var nameEntry = new GameObject("NameEntry", typeof(RectTransform));
        nameEntry.transform.SetParent(over.transform, false);
        Place((RectTransform)nameEntry.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(900f, 150f));
        GameObject inputGo = TMP_DefaultControls.CreateInputField(uiResources);
        inputGo.name = "NameInput";
        inputGo.transform.SetParent(nameEntry.transform, false);
        Place((RectTransform)inputGo.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(600f, 130f));
        var nameInput = inputGo.GetComponent<TMP_InputField>();
        nameInput.characterLimit = 12;
        nameInput.pointSize = 48;
        if (nameInput.placeholder is TMP_Text placeholderText) placeholderText.text = "Your name";
        MakeButton(nameEntry.transform, "SaveScoreButton", "SAVE", new Vector2(1f, 0.5f), new Vector2(-130f, 0f), new Vector2(260f, 130f), new Color(0.2f, 0.65f, 0.3f), 48, ui.SaveScore);
        MakeButton(over.transform, "PlayAgainButton", "PLAY AGAIN", new Vector2(0.5f, 0.5f), new Vector2(0f, -260f), new Vector2(600f, 150f), new Color(1f, 0.5f, 0.1f), 56, game.RestartGame);
        MakeButton(over.transform, "MainMenuButton", "MAIN MENU", new Vector2(0.5f, 0.5f), new Vector2(0f, -440f), new Vector2(600f, 130f), new Color(0.25f, 0.25f, 0.3f), 46, game.ReturnToTitle);
        MakeButton(over.transform, "ResultsLeaderboardButton", "LEADERBOARD", new Vector2(0.5f, 0.5f), new Vector2(0f, -600f), new Vector2(600f, 130f), new Color(0.25f, 0.25f, 0.3f), 46, ui.OpenLeaderboard);

        // Leaderboard (drawn on top of everything)
        Image board = Panel(safe, "LeaderboardPanel", new Color(0.03f, 0.05f, 0.12f, 0.95f), true);
        TMP_Text boardTitle = MakeText(board.transform, "LeaderboardTitle", "TOP 10 - 60s CHALLENGE", 64, TextAlignmentOptions.Center, new Color(1f, 0.55f, 0.12f), true);
        Place(boardTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(1000f, 110f));
        TMP_Text header = MakeText(board.transform, "LeaderboardHeader", "#<pos=12%>NAME<pos=64%>SCORE<pos=82%>ACC", 38, TextAlignmentOptions.Left, new Color(0.7f, 0.7f, 0.7f));
        Place(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -280f), new Vector2(900f, 70f));
        TMP_Text boardText = MakeText(board.transform, "LeaderboardText", "No scores yet.", 44, TextAlignmentOptions.TopLeft, Color.white);
        boardText.lineSpacing = 20f;
        Place(boardText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -360f), new Vector2(900f, 1000f));
        MakeButton(board.transform, "CloseLeaderboardButton", "CLOSE", new Vector2(0.5f, 0f), new Vector2(0f, 200f), new Vector2(500f, 140f), new Color(0.25f, 0.25f, 0.3f), 50, ui.CloseLeaderboard);

        // --- wire the scripts (same fields as the Inspector tables in the guides)
        Set(placement, "raycastManager", raycastManager);
        Set(placement, "planeManager", planeManager);
        Set(placement, "arCamera", cam);
        Set(placement, "hoopPrefab", Asset<GameObject>("Assets/Prefabs/BasketballHoop.prefab"));
        Set(placement, "placementIndicatorPrefab", Asset<GameObject>("Assets/Prefabs/PlacementIndicator.prefab"));
        Set(placement, "statusText", status);
        Set(placement, "repositionButton", reposition.gameObject);

        Set(spawner, "placement", placement);
        Set(spawner, "arCamera", cam);
        Set(spawner, "ballPrefab", Asset<GameObject>("Assets/Prefabs/Basketball.prefab").GetComponent<BasketballController>());
        Set(spawner, "floorMaterial", Asset<PhysicsMaterial>("Assets/Materials/Physics/Floor_Physics.asset"));
        SetArray(spawner, "showWhenHoopPlaced", resetBall.gameObject, hint.gameObject);

        Set(shooter, "spawner", spawner);
        Set(shooter, "placement", placement);
        Set(shooter, "hintText", hint);

        Set(game, "placement", placement);
        Set(game, "shooter", shooter);
        Set(game, "spawner", spawner);

        Set(audio, "spawner", spawner);
        Set(audio, "shooter", shooter);
        Set(audio, "gameManager", game);
        Set(audio, "floorBounce", Clip("ball_bounce"));
        Set(audio, "rimHit", Clip("rim_clank"));
        Set(audio, "backboardHit", Clip("backboard_thud"));
        Set(audio, "basketSwish", Clip("net_swish"));
        Set(audio, "basketChime", Clip("score_chime"));
        Set(audio, "shoot", Clip("shoot_whoosh"));
        Set(audio, "buttonClick", Clip("button_click"));
        Set(audio, "countdownTick", Clip("countdown_tick"));
        Set(audio, "buzzer", Clip("buzzer"));

        Set(effects, "gameManager", game);
        Set(effects, "placement", placement);
        Set(effects, "basketEffectPrefab", Asset<GameObject>("Assets/Prefabs/BasketEffect.prefab").GetComponent<ParticleSystem>());

        Set(ui, "gameManager", game);
        Set(ui, "scoreText", score);
        Set(ui, "shotsText", shots);
        Set(ui, "accuracyText", accuracy);
        Set(ui, "basketMessage", basket);
        Set(ui, "titlePanel", title.gameObject);
        Set(ui, "hudPanel", hud.gameObject);
        Set(ui, "placeHoopPanel", placeCard);
        Set(ui, "aimShootPanel", aimCard);
        Set(ui, "pausePanel", pausePanel.gameObject);
        Set(ui, "pauseButton", pause.gameObject);
        Set(ui, "timerText", timer);
        Set(ui, "gameOverPanel", over.gameObject);
        Set(ui, "resultText", result);
        Set(ui, "rankText", rank);
        Set(ui, "nameEntry", nameEntry);
        Set(ui, "nameInput", nameInput);
        Set(ui, "leaderboardPanel", board.gameObject);
        Set(ui, "leaderboardText", boardText);
        Set(ui, "bestScoreText", best);

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        Log("Scene saved and added to the build list: " + ScenePath);
    }

    private static void SetIcon()
    {
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/UI/AppIcon.png");
        if (icon == null) throw new Exception("Assets/UI/AppIcon.png not found.");
        PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        AssetDatabase.SaveAssets();
        Log("App icon set.");
    }

    // ================================================================ FONT FIX

    /// <summary>
    /// Run after importing TMP Essential Resources (-importPackage): gives every text in the scene
    /// the default TextMeshPro font, because the texts were created before the font existed.
    /// </summary>
    public static void FixFonts()
    {
        Run("Fix fonts", () =>
        {
            TMP_FontAsset font = TMP_Settings.defaultFontAsset;
            if (font == null) throw new Exception("TMP Settings / default font not found - import TMP Essential Resources first.");

            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            int count = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    text.font = font;
                    text.fontSharedMaterial = font.material;
                    EditorUtility.SetDirty(text);
                    count++;
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Log($"Assigned '{font.name}' to {count} texts.");
        });
    }

    /// <summary>Applies UI polish to an already-built scene (currently: the title fits any screen width).</summary>
    public static void Polish()
    {
        Run("Polish", () =>
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            TMP_Text title = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<TMP_Text>(true))
                .FirstOrDefault(t => t.name == "TitleText");
            if (title == null) throw new Exception("TitleText not found.");
            FitTitle(title);
            EditorUtility.SetDirty(title);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Log("Title now auto-sizes to the screen width.");
        });
    }

    /// <summary>Shrinks the game title to fit narrow screens (auto-size 56-110, up to two lines).</summary>
    private static void FitTitle(TMP_Text title)
    {
        title.enableAutoSizing = true;
        title.fontSizeMin = 56f;
        title.fontSizeMax = 110f;
    }

    // ================================================================ UI helpers

    private static Transform Stretch(GameObject go, Transform parent)
    {
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return go.transform;
    }

    private static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    private static Image Panel(Transform parent, string name, Color colour, bool blocksTouches)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Stretch(go, parent);
        var image = go.AddComponent<Image>();
        image.color = colour;
        image.raycastTarget = blocksTouches;
        return image;
    }

    private static GameObject Card(Transform parent, string name, float y, float height, string title, string body)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Place((RectTransform)go.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, y), new Vector2(-120f, height));
        var image = go.AddComponent<Image>();
        image.sprite = uiResources.standard;
        image.type = Image.Type.Sliced;
        image.color = new Color(0f, 0f, 0f, 0.63f);
        image.raycastTarget = false;
        var group = go.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        TMP_Text header = MakeText(go.transform, "Header", title, 56, TextAlignmentOptions.Center, new Color(1f, 0.7f, 0.2f), true);
        Place(header.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(-40f, 80f));
        TMP_Text text = MakeText(go.transform, "Body", body, 40, TextAlignmentOptions.Center, Color.white);
        Place(text.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(-60f, -120f));
        return go;
    }

    private static TMP_Text MakeText(Transform parent, string name, string value, float size, TextAlignmentOptions align, Color colour, bool bold = false)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.alignment = align;
        text.color = colour;
        text.raycastTarget = false;
        if (bold) text.fontStyle = FontStyles.Bold;
        return text;
    }

    private static Button MakeButton(Transform parent, string name, string label, Vector2 anchor, Vector2 position, Vector2 size,
        Color colour, float fontSize, UnityAction onClick)
    {
        GameObject go = TMP_DefaultControls.CreateButton(uiResources);
        go.name = name;
        go.transform.SetParent(parent, false);
        Place((RectTransform)go.transform, anchor, anchor, new Vector2(0.5f, 0.5f), position, size);
        go.GetComponent<Image>().color = colour;

        TMP_Text text = go.GetComponentInChildren<TMP_Text>();
        text.text = label;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.color = Color.white;

        var button = go.GetComponent<Button>();
        UnityEventTools.AddPersistentListener(button.onClick, onClick);
        go.AddComponent<ButtonFeedback>();
        return button;
    }

    // ================================================================ general helpers

    private static void Run(string phase, Action body)
    {
        try
        {
            Log($"{phase} started");
            body();
            AssetDatabase.SaveAssets();
            Log($"{phase} FINISHED OK");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogError($"[AutoSetup] {phase} FAILED: {e}");
            EditorApplication.Exit(1);
        }
    }

    private static void Log(string message) => Debug.Log("[AutoSetup] " + message);

    private static GameObject CreateFromMenu(string menu)
    {
        Selection.activeObject = null;
        if (!EditorApplication.ExecuteMenuItem(menu))
        {
            Log($"Menu not available: {menu}");
            return null;
        }
        GameObject go = Selection.activeGameObject;
        if (go != null) go.transform.SetParent(null, false);
        return go;
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    private static T Asset<T>(string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) throw new Exception("Missing asset: " + path);
        return asset;
    }

    private static AudioClip Clip(string name) => Asset<AudioClip>($"Assets/Audio/{name}.wav");

    private static Sprite LoadSprite(string path) => AssetDatabase.GetBuiltinExtraResource<Sprite>(path);

    private static Material Unlit(string name, Color colour)
    {
        EnsureFolder("Assets/Materials");
        string path = $"Assets/Materials/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = name, color = colour };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void Set(Component target, string field, Object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(field);
        if (property == null) throw new Exception($"{target.GetType().Name} has no serialized field '{field}'.");
        property.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetArray(Component target, string field, params Object[] values)
    {
        var so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(field);
        if (property == null) throw new Exception($"{target.GetType().Name} has no serialized field '{field}'.");
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        string parent = path.Substring(0, slash);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
    }
}
