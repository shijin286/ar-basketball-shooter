using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Management;

/// <summary>
/// STAGE 10 - one-click build of the laptop version (Windows .exe, virtual court instead of AR).
///
/// Menu: Tools > AR Basketball >
///   Add Desktop Mode To Scene  - adds DesktopMode to GameSystems and wires it (safe to run again)
///   Build Windows App (.exe)   - does the above, then builds Builds/Windows/ARBasketballShooter.exe
///
/// Command line:
///   Unity -batchmode -quit -projectPath &lt;project&gt; -buildTarget Win64 -executeMethod WindowsBuilder.BuildExe
///
/// To move the app to another PC, copy (or zip) the whole Builds/Windows folder, not just the .exe.
/// Editor-only (lives in an Editor folder), so it is not part of the app.
/// </summary>
public static class WindowsBuilder
{
    public const string OutputFolder = "Builds/Windows";
    public const string ExeName = "ARBasketballShooter.exe";
    public const string SurfaceMaterialPath = "Assets/Materials/Desktop/DesktopSurface.mat";
    private const string FloorPhysicsPath = "Assets/Materials/Physics/Floor_Physics.asset";

    [MenuItem("Tools/AR Basketball/Add Desktop Mode To Scene", priority = 110)]
    public static void SetUpSceneFromMenu()
    {
        if (!CanChangeScene()) return;
        SetUpScene();
        EditorUtility.DisplayDialog("Desktop mode", "DesktopMode is on GameSystems and wired. The scene is saved.", "OK");
    }

    [MenuItem("Tools/AR Basketball/Build Windows App (.exe)", priority = 111)]
    public static void BuildExe()
    {
        if (!CanChangeScene()) return;
        SetUpScene();

        PlayerSettings.fullScreenMode = FullScreenMode.Windowed; // Alt+Enter switches to full screen
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

        // XR Simulation (the Editor's fake AR) is Editor-only, so the laptop app starts without XR.
        // The setting is shared with Play mode in the Editor, so it is put back after the build.
        XRGeneralSettings xr = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
        bool initBefore = xr != null && xr.InitManagerOnStart;

        // "-buildFolder Builds/Windows_next" builds next to a copy of the game that is still running.
        string folder = CommandLineValue("-buildFolder") ?? OutputFolder;
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, ExeName);
        BuildSummary summary;
        try
        {
            if (xr != null) xr.InitManagerOnStart = false;
            summary = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { AndroidBuilder.ScenePath },
                locationPathName = path,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            }).summary;
        }
        finally
        {
            if (xr != null)
            {
                xr.InitManagerOnStart = initBefore;
                EditorUtility.SetDirty(xr);
                AssetDatabase.SaveAssets();
            }
        }

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[WindowsBuilder] Built {summary.outputPath} ({summary.totalSize / (1024f * 1024f):0.0} MB) " +
                      $"in {summary.totalTime.TotalSeconds:0} s.");
            if (!Application.isBatchMode) EditorUtility.RevealInFinder(path);
        }
        else
        {
            Debug.LogError($"[WindowsBuilder] Build {summary.result} with {summary.totalErrors} error(s). " +
                           "Scroll up in the Console for the first red error.");
        }
    }

    /// <summary>Adds DesktopMode to GameSystems (once) and assigns its references, then saves the scene.</summary>
    public static void SetUpScene()
    {
        Scene scene = EditorSceneManager.OpenScene(AndroidBuilder.ScenePath, OpenSceneMode.Single);
        GameObject systems = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "GameSystems");
        if (systems == null) throw new Exception("GameSystems not found in " + AndroidBuilder.ScenePath);

        DesktopMode desktop = systems.GetComponent<DesktopMode>();
        if (desktop == null) desktop = systems.AddComponent<DesktopMode>();

        Camera cam = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true))
            .FirstOrDefault(c => c.CompareTag("MainCamera"));

        var so = new SerializedObject(desktop);
        so.FindProperty("placement").objectReferenceValue = systems.GetComponent<ARPlanePlacement>();
        so.FindProperty("playerCamera").objectReferenceValue = cam;
        so.FindProperty("surfaceMaterial").objectReferenceValue = EnsureSurfaceMaterial();
        so.FindProperty("floorPhysics").objectReferenceValue = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(FloorPhysicsPath);
        so.FindProperty("placeHoopText").objectReferenceValue = FindText(scene, "PlaceHoopPanel", "Body");
        so.FindProperty("aimShootText").objectReferenceValue = FindText(scene, "AimShootPanel", "Body");
        so.FindProperty("howToText").objectReferenceValue = FindText(scene, "TitlePanel", "HowToText");
        so.FindProperty("subtitleText").objectReferenceValue = FindText(scene, "TitlePanel", "SubtitleText");
        so.FindProperty("forceInEditor").boolValue = false; // the Editor keeps using XR Simulation
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[WindowsBuilder] DesktopMode added to GameSystems and wired.");
    }

    private static string CommandLineValue(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private static bool CanChangeScene()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Windows build", "Stop Play mode first.", "OK");
            return false;
        }
        return Application.isBatchMode || EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
    }

    private static Material EnsureSurfaceMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(SurfaceMaterialPath);
        if (material != null) return material;

        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder("Assets/Materials/Desktop")) AssetDatabase.CreateFolder("Assets/Materials", "Desktop");

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new Exception("URP Lit shader not found.");
        material = new Material(shader) { name = "DesktopSurface" };
        AssetDatabase.CreateAsset(material, SurfaceMaterialPath);
        AssetDatabase.SaveAssets();
        return material;
    }

    private static TMP_Text FindText(Scene scene, string parentName, string textName)
    {
        TMP_Text text = scene.GetRootGameObjects()
            .SelectMany(g => g.GetComponentsInChildren<TMP_Text>(true))
            .FirstOrDefault(t => t.name == textName && t.transform.parent != null && t.transform.parent.name == parentName);
        if (text == null) Debug.LogWarning($"[WindowsBuilder] {parentName}/{textName} not found - that instruction stays as on the phone.");
        return text;
    }
}
