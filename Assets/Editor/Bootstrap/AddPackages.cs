using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

/// <summary>
/// One-time bootstrap (run from the command line with -executeMethod AddPackages.Run).
/// Adds the packages the game needs; Unity picks the versions that match this editor.
/// </summary>
public static class AddPackages
{
    private static readonly string[] Packages =
    {
        "com.unity.xr.arfoundation",
        "com.unity.xr.arcore",
        "com.unity.inputsystem",
        "com.unity.render-pipelines.universal",
        "com.unity.ugui",
        "com.unity.test-framework",
    };

    private static AddAndRemoveRequest request;

    public static void Run()
    {
        Debug.Log("[AddPackages] Adding: " + string.Join(", ", Packages));
        request = Client.AddAndRemove(Packages, null);
        EditorApplication.update += Poll;
    }

    private static void Poll()
    {
        if (request == null || !request.IsCompleted) return;
        EditorApplication.update -= Poll;

        if (request.Status == StatusCode.Success)
        {
            foreach (var package in request.Result)
            {
                if (System.Array.IndexOf(Packages, package.name) >= 0)
                    Debug.Log($"[AddPackages] OK {package.name}@{package.version}");
            }
            EditorApplication.Exit(0);
        }
        else
        {
            Debug.LogError($"[AddPackages] FAILED: {request.Error?.message}");
            EditorApplication.Exit(1);
        }
    }
}
