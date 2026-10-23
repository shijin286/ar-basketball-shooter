using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// STAGE 8 automated checks (EditMode tab): is the project ready to build and hand in?
/// Scene in the build list, your own package name, name/version/icon, the build
/// tool's own checks, and MobileSettings in the scene.
/// </summary>
public class Stage8ReleaseTests
{
    private const string ScenePath = "Assets/Scenes/ARBasketballScene.unity";

    // ---------------------------------------------------------------- build list and identity

    [Test]
    public void GameScene_IsFirstInBuildList()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        Assert.IsNotEmpty(scenes, "Add ARBasketballScene in File > Build Profiles > Scene List.");
        Assert.AreEqual(ScenePath, scenes[0].path, "ARBasketballScene must be the first scene (index 0).");
        Assert.IsTrue(scenes[0].enabled, "Tick ARBasketballScene in the Scene List.");
    }

    [TestCase("com.yourname.arbasketball", true)]
    [TestCase("com.college.student.arbasketball", true)]
    [TestCase("com.DefaultCompany.ARBasketballShooter", false)]
    [TestCase("com.company.productname", false)]
    [TestCase("arbasketball", false)]
    [TestCase("com.my app.game", false)]
    public void BuildTool_ChecksPackageNames(string id, bool expected)
    {
        Assert.AreEqual(expected, (bool)CallBuilder("IsValidPackageName", new[] { typeof(string) }, id));
    }

    [Test]
    public void PackageName_IsYourOwn()
    {
        string id = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        Assert.IsTrue((bool)CallBuilder("IsValidPackageName", new[] { typeof(string) }, id),
            $"Package Name '{id}' must be lowercase and your own, e.g. com.yourname.arbasketball.");
    }

    [Test]
    public void App_HasNameVersionAndIcon()
    {
        StringAssert.Contains("Basketball", PlayerSettings.productName, "Product Name should be 'AR Basketball Shooter'.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(PlayerSettings.bundleVersion), "Set Version, e.g. 1.0.");
        Assert.GreaterOrEqual(PlayerSettings.Android.bundleVersionCode, 1, "Bundle Version Code must be 1 or more.");

        Texture2D[] icons = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any);
        Assert.IsTrue(icons != null && icons.Length > 0 && icons[0] != null,
            "Set Player > Default Icon to Assets/UI/AppIcon.png.");
    }

    [Test]
    public void BuildTool_FindsNoProblems()
    {
        var problems = (List<string>)CallBuilder("FindProblems", Type.EmptyTypes);
        Assert.IsEmpty(problems, "Fix these before building:\n• " + string.Join("\n• ", problems));
    }

    [Test]
    public void ForSubmission_DevelopmentBuildIsOff()
    {
        if (EditorUserBuildSettings.development)
        {
            Assert.Ignore("Development Build is on. That's fine for testing; turn it off for the final APK you hand in.");
        }
    }

    // ---------------------------------------------------------------- scene

    [Test]
    public void Scene_HasMobileSettings()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedByTest = false;
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), $"Scene not found at {ScenePath}.");
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            openedByTest = true;
        }

        try
        {
            bool found = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true))
                .Any(m => m != null && m.GetType().Name == "MobileSettings");
            Assert.IsTrue(found, "Add MobileSettings to GameSystems (keeps the screen on during AR).");
        }
        finally
        {
            if (openedByTest) EditorSceneManager.CloseScene(scene, true);
        }
    }

    // ---------------------------------------------------------------- helpers

    private static object CallBuilder(string method, Type[] types, params object[] args)
    {
        Type builder = Type.GetType("AndroidBuilder, Assembly-CSharp-Editor");
        Assert.IsNotNull(builder, "AndroidBuilder not found. Copy AndroidBuilder.cs into Assets/Scripts/Editor.");
        MethodInfo info = builder.GetMethod(method, BindingFlags.Public | BindingFlags.Static, null, types, null);
        Assert.IsNotNull(info, $"AndroidBuilder.{method} not found - is the script up to date?");
        return info.Invoke(null, args);
    }
}
