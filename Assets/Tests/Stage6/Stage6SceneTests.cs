using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// STAGE 6 automated checks (EditMode tab) for the finished UI in ARBasketballScene:
/// panels assigned, required texts present, buttons wired, readable on every screen size.
/// </summary>
public class Stage6SceneTests
{
    private const string ScenePath = "Assets/Scenes/ARBasketballScene.unity";

    private Scene scene;
    private bool openedByTest;
    private SerializedObject ui;

    [OneTimeSetUp]
    public void OpenScene()
    {
        Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), $"Scene not found at {ScenePath}.");
        scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            openedByTest = true;
        }

        MonoBehaviour manager = FindAll<MonoBehaviour>().FirstOrDefault(m => m != null && m.GetType().Name == "UIManager");
        Assert.IsNotNull(manager, "UIManager missing on the Canvas (Stage 5).");
        ui = new SerializedObject(manager);
    }

    [OneTimeTearDown]
    public void CloseScene()
    {
        if (openedByTest && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
    }

    // ---------------------------------------------------------------- panels and texts

    [TestCase("titlePanel")]
    [TestCase("hudPanel")]
    [TestCase("placeHoopPanel")]
    [TestCase("aimShootPanel")]
    [TestCase("pausePanel")]
    [TestCase("pauseButton")]
    public void UIManager_PanelIsAssigned(string field)
    {
        Assert.IsNotNull(ui.FindProperty(field).objectReferenceValue, $"Assign UIManager > {ObjectNames.NicifyVariableName(field)}.");
    }

    [Test]
    public void TitlePanel_ShowsGameTitle()
    {
        GameObject title = Panel("titlePanel");
        bool hasTitle = title.GetComponentsInChildren<TMP_Text>(true).Any(t => t.text.Contains("AR Basketball Shooter"));
        Assert.IsTrue(hasTitle, "The title panel needs a text reading 'AR Basketball Shooter'.");
    }

    [Test]
    public void Instructions_ExplainPlacingAndShooting()
    {
        string place = AllText(Panel("placeHoopPanel"));
        string aim = AllText(Panel("aimShootPanel"));
        StringAssert.Contains("tap", place, "Place Hoop instructions should tell the player to tap the floor.");
        StringAssert.Contains("swipe", aim, "Aim & Shoot instructions should tell the player to swipe.");
    }

    [Test]
    public void FullScreenPanels_BlockTouchesUnderneath()
    {
        foreach (string field in new[] { "titlePanel", "pausePanel" })
        {
            bool blocks = Panel(field).GetComponentsInChildren<Graphic>(true).Any(g => g.raycastTarget && g is Image);
            Assert.IsTrue(blocks, $"{field} needs a full-screen background Image with Raycast Target ✔ so taps can't reach the game.");
        }
    }

    [Test]
    public void InstructionCards_DoNotBlockButtons()
    {
        foreach (string field in new[] { "placeHoopPanel", "aimShootPanel" })
        {
            GameObject panel = Panel(field);
            CanvasGroup group = panel.GetComponent<CanvasGroup>();
            if (group != null && !group.blocksRaycasts) continue; // one checkbox covers the whole card

            Graphic blocking = panel.GetComponentsInChildren<Graphic>(true).FirstOrDefault(g => g.raycastTarget);
            Assert.IsNull(blocking, $"Add a Canvas Group to {field} and untick Blocks Raycasts " +
                                    $"('{blocking?.name}' would block the buttons under it).");
        }
    }

    // ---------------------------------------------------------------- buttons

    [TestCase("StartButton", "StartGame")]
    [TestCase("PauseButton", "PauseGame")]
    [TestCase("ResumeButton", "ResumeGame")]
    [TestCase("RestartButton", "RestartGame")]
    [TestCase("ResetBallButton", "ResetBall")]
    [TestCase("RepositionButton", "RepositionHoop")]
    public void Button_IsWired(string buttonName, string method)
    {
        Button button = FindAll<Button>().FirstOrDefault(b => b.name == buttonName);
        Assert.IsNotNull(button, $"Create a UI Button named {buttonName}.");

        bool wired = Enumerable.Range(0, button.onClick.GetPersistentEventCount())
            .Any(i => button.onClick.GetPersistentMethodName(i) == method && button.onClick.GetPersistentTarget(i) != null);
        Assert.IsTrue(wired, $"{buttonName} > On Click () must call {method}.");
    }

    // ---------------------------------------------------------------- screen sizes

    [Test]
    public void Canvas_ScalesWithScreenSize()
    {
        CanvasScaler scaler = FindAll<CanvasScaler>().FirstOrDefault();
        Assert.IsNotNull(scaler, "The Canvas needs a Canvas Scaler.");
        Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode, "UI Scale Mode must be Scale With Screen Size.");
        Assert.AreEqual(1080f, scaler.referenceResolution.x, 1f, "Reference Resolution should be 1080 x 1920.");
        Assert.AreEqual(1920f, scaler.referenceResolution.y, 1f, "Reference Resolution should be 1080 x 1920.");
        Assert.That(scaler.matchWidthOrHeight, Is.InRange(0.3f, 0.7f), "Match should be about 0.5.");
    }

    [Test]
    public void Hud_IsInsideSafeArea()
    {
        MonoBehaviour safeArea = FindAll<MonoBehaviour>().FirstOrDefault(m => m != null && m.GetType().Name == "SafeArea");
        Assert.IsNotNull(safeArea, "Add a full-screen 'SafeArea' object with the SafeArea script under the Canvas.");

        foreach (string field in new[] { "hudPanel", "titlePanel", "pausePanel" })
        {
            Assert.IsTrue(Panel(field).transform.IsChildOf(safeArea.transform), $"Move {field} inside the SafeArea object.");
        }
    }

    // ---------------------------------------------------------------- helpers

    private GameObject Panel(string field)
    {
        Object value = ui.FindProperty(field).objectReferenceValue;
        Assert.IsNotNull(value, $"Assign UIManager > {ObjectNames.NicifyVariableName(field)}.");
        return (GameObject)value;
    }

    private static string AllText(GameObject panel)
    {
        return string.Join(" ", panel.GetComponentsInChildren<TMP_Text>(true).Select(t => t.text)).ToLowerInvariant();
    }

    private T[] FindAll<T>() where T : Component
    {
        return scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
    }
}
