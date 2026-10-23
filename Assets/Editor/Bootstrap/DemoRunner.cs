using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Opens the game scene, switches the Game view to a 1080x1920 portrait phone screen,
/// and enters Play mode with DemoDriver running the automatic demo.
/// Command line: -executeMethod DemoRunner.Begin   Menu: Tools > AR Basketball > Run Demo
/// </summary>
public static class DemoRunner
{
    [MenuItem("Tools/AR Basketball/Run Demo", priority = 200)]
    public static void Begin()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/ARBasketballScene.unity");
        Debug.Log("[Demo] Portrait Game view: " + (TrySetPortraitGameView() ? "set to 1080x1920" : "not available, using current size"));

        Directory.CreateDirectory("Temp");
        if (File.Exists(DemoDriver.DoneFile)) File.Delete(DemoDriver.DoneFile);
        File.WriteAllText(DemoDriver.RequestFile, "1");
        EditorApplication.EnterPlaymode();
    }

    /// <summary>Opens the game scene in a portrait Game view and presses Play - for you to play yourself.</summary>
    [MenuItem("Tools/AR Basketball/Play (phone portrait)", priority = 201)]
    public static void PlayNow()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/ARBasketballScene.unity");
        TrySetPortraitGameView();
        if (File.Exists(DemoDriver.RequestFile)) File.Delete(DemoDriver.RequestFile); // no automatic demo
        EditorApplication.EnterPlaymode();
    }

    /// <summary>Adds and selects a 1080x1920 size in the Game view (Unity has no public API for this).</summary>
    private static bool TrySetPortraitGameView()
    {
        try
        {
            Assembly editor = typeof(Editor).Assembly;
            Type sizesType = editor.GetType("UnityEditor.GameViewSizes");
            Type sizeType = editor.GetType("UnityEditor.GameViewSize");
            Type kindType = editor.GetType("UnityEditor.GameViewSizeType");
            object sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
            object group = sizesType.GetMethod("GetGroup").Invoke(sizes, new object[] { (int)GameViewSizeGroupType.Standalone });

            object size = sizeType.GetConstructor(new[] { kindType, typeof(int), typeof(int), typeof(string) })
                .Invoke(new object[] { Enum.Parse(kindType, "FixedResolution"), 1080, 1920, "Phone portrait 1080x1920" });
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            int index = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null) - 1;

            Type gameViewType = editor.GetType("UnityEditor.GameView");
            EditorWindow gameView = EditorWindow.GetWindow(gameViewType);
            gameViewType.GetMethod("SizeSelectionCallback", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Invoke(gameView, new object[] { index, null });
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Demo] Could not set the Game view size: " + e.Message);
            return false;
        }
    }
}
