using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// STAGE 8 - one-click Android build.
///
/// Menu: Tools > AR Basketball >
///   Check Build Settings    - lists anything that would break or weaken the build
///   Build Android APK       - checks, then builds Builds/ARBasketballShooter_v{version}.apk
///   Build And Run On Phone  - same, then installs and starts it on the USB-connected phone
///
/// Editor-only (lives in an Editor folder), so it is not part of the APK.
/// </summary>
public static class AndroidBuilder
{
    public const string ScenePath = "Assets/Scenes/ARBasketballScene.unity";
    public const string OutputFolder = "Builds";

    [MenuItem("Tools/AR Basketball/Check Build Settings", priority = 100)]
    public static void CheckFromMenu()
    {
        List<string> problems = FindProblems();
        if (problems.Count == 0)
        {
            EditorUtility.DisplayDialog("Build check", "Everything looks ready for an Android build.", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Build check", "Fix these first:\n\n• " + string.Join("\n• ", problems), "OK");
        }
    }

    [MenuItem("Tools/AR Basketball/Build Android APK", priority = 101)]
    public static void BuildApk() => Build(false);

    [MenuItem("Tools/AR Basketball/Build And Run On Phone", priority = 102)]
    public static void BuildAndRun() => Build(true);

    /// <summary>Everything that must be right before building. Empty list = ready.</summary>
    public static List<string> FindProblems()
    {
        var problems = new List<string>();

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            problems.Add("Switch the platform to Android (File > Build Profiles > Android > Switch Platform).");

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            problems.Add($"Scene {ScenePath} not found.");

        string id = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        if (!IsValidPackageName(id))
            problems.Add($"Package Name '{id}' must be your own, like com.yourname.arbasketball (Player > Other Settings).");

        if ((int)PlayerSettings.Android.minSdkVersion < 24)
            problems.Add("Minimum API Level must be Android 7.0 (API 24) or higher for ARCore.");

        if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP)
            problems.Add("Scripting Backend must be IL2CPP.");

        if ((PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) == 0)
            problems.Add("Target Architectures must include ARM64.");

        if (string.IsNullOrWhiteSpace(PlayerSettings.bundleVersion))
            problems.Add("Set a Version (Player > Other Settings), e.g. 1.0.");

        return problems;
    }

    /// <summary>
    /// True for a reverse-domain name with at least three lowercase parts (com.name.app)
    /// that is not one of Unity's placeholder names.
    /// </summary>
    public static bool IsValidPackageName(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        if (!Regex.IsMatch(id, @"^[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*){2,}$")) return false;
        string lower = id.ToLowerInvariant();
        return !lower.Contains("defaultcompany") && !lower.StartsWith("com.company.") && !lower.Contains("unity.template");
    }

    private static void Build(bool runOnPhone)
    {
        List<string> problems = FindProblems();
        if (problems.Count > 0)
        {
            EditorUtility.DisplayDialog("Can't build yet", "Fix these first:\n\n• " + string.Join("\n• ", problems), "OK");
            return;
        }

        EditorUserBuildSettings.buildAppBundle = false; // .apk for direct install (.aab is only for Google Play)
        Directory.CreateDirectory(OutputFolder);
        string path = Path.Combine(OutputFolder, $"ARBasketballShooter_v{PlayerSettings.bundleVersion}.apk");

        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = path,
            target = BuildTarget.Android,
            options = runOnPhone ? BuildOptions.AutoRunPlayer : BuildOptions.None,
        };
        if (EditorUserBuildSettings.development) options.options |= BuildOptions.Development;

        BuildSummary summary = BuildPipeline.BuildPlayer(options).summary;
        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[AndroidBuilder] Built {summary.outputPath} ({summary.totalSize / (1024f * 1024f):0.0} MB) " +
                      $"in {summary.totalTime.TotalSeconds:0} s.");
            if (!runOnPhone) EditorUtility.RevealInFinder(path);
        }
        else
        {
            Debug.LogError($"[AndroidBuilder] Build {summary.result} with {summary.totalErrors} error(s). " +
                           "Scroll up in the Console for the first red error.");
        }
    }
}
