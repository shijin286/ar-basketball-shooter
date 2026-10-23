using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// STAGE 10 - browser build (WebGL) of the laptop version, for hosting on GitHub Pages.
///
/// Menu: Tools > AR Basketball > Build Web Version (WebGL)
/// Output: Builds/WebGL (index.html + Build/). Open it through a web server, not by double-clicking.
/// Command line:
///   Unity -batchmode -quit -projectPath &lt;project&gt; -buildTarget WebGL -executeMethod WebGLBuilder.BuildWeb
///
/// The page comes from Assets/WebGLTemplates/ARBasketball (full-window canvas, loading bar, controls).
/// Editor-only (lives in an Editor folder), so it is not part of the game.
/// </summary>
public static class WebGLBuilder
{
    public const string OutputFolder = "Builds/WebGL";
    public const string Template = "PROJECT:ARBasketball";

    [MenuItem("Tools/AR Basketball/Build Web Version (WebGL)", priority = 112)]
    public static void BuildWeb()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Web build", "Stop Play mode first.", "OK");
            return;
        }
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        WindowsBuilder.SetUpScene(); // the browser uses the same DesktopMode as the laptop app

        PlayerSettings.WebGL.template = Template;
        // GitHub Pages can't send "Content-Encoding" headers, so the loader unpacks the gzip files itself.
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true; // the second visit loads from the browser cache

        BuildSummary summary = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { AndroidBuilder.ScenePath },
            locationPathName = OutputFolder,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        }).summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[WebGLBuilder] Built {summary.outputPath} ({summary.totalSize / (1024f * 1024f):0.0} MB) " +
                      $"in {summary.totalTime.TotalSeconds:0} s.");
            if (!Application.isBatchMode) EditorUtility.RevealInFinder(OutputFolder);
        }
        else
        {
            Debug.LogError($"[WebGLBuilder] Build {summary.result} with {summary.totalErrors} error(s). " +
                           "Scroll up in the Console for the first red error.");
        }
    }
}
