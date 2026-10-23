using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// STAGE 9 automated checks (EditMode tab) for the scene: challenge and leaderboard UI
/// assigned, buttons wired, panels block touches and sit in the safe area, timer in the HUD,
/// and the two new sounds imported and assigned.
/// </summary>
public class Stage9SceneTests
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
        Assert.IsNotNull(manager, "UIManager missing on the Canvas.");
        ui = new SerializedObject(manager);
    }

    [OneTimeTearDown]
    public void CloseScene()
    {
        if (openedByTest && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
    }

    [TestCase("timerText")]
    [TestCase("gameOverPanel")]
    [TestCase("resultText")]
    [TestCase("rankText")]
    [TestCase("nameEntry")]
    [TestCase("nameInput")]
    [TestCase("leaderboardPanel")]
    [TestCase("leaderboardText")]
    public void UIManager_ChallengeFieldIsAssigned(string field)
    {
        Assert.IsNotNull(ui.FindProperty(field).objectReferenceValue, $"Assign UIManager > {ObjectNames.NicifyVariableName(field)}.");
    }

    [TestCase("ChallengeButton", "StartChallenge")]
    [TestCase("LeaderboardButton", "OpenLeaderboard")]
    [TestCase("CloseLeaderboardButton", "CloseLeaderboard")]
    [TestCase("PlayAgainButton", "RestartGame")]
    [TestCase("MainMenuButton", "ReturnToTitle")]
    [TestCase("SaveScoreButton", "SaveScore")]
    public void Button_IsWired(string buttonName, string method)
    {
        Button button = FindAll<Button>().FirstOrDefault(b => b.name == buttonName);
        Assert.IsNotNull(button, $"Create a UI Button named {buttonName}.");

        bool wired = Enumerable.Range(0, button.onClick.GetPersistentEventCount())
            .Any(i => button.onClick.GetPersistentMethodName(i) == method && button.onClick.GetPersistentTarget(i) != null);
        Assert.IsTrue(wired, $"{buttonName} > On Click () must call {method}.");
    }

    [Test]
    public void ResultAndLeaderboardPanels_BlockTouchesAndUseSafeArea()
    {
        MonoBehaviour safeArea = FindAll<MonoBehaviour>().FirstOrDefault(m => m != null && m.GetType().Name == "SafeArea");
        Assert.IsNotNull(safeArea, "SafeArea missing (Stage 6).");

        foreach (string field in new[] { "gameOverPanel", "leaderboardPanel" })
        {
            GameObject panel = Panel(field);
            Assert.IsTrue(panel.transform.IsChildOf(safeArea.transform), $"Move {field} inside SafeArea.");
            bool blocks = panel.GetComponentsInChildren<Graphic>(true).Any(g => g.raycastTarget && g is Image);
            Assert.IsTrue(blocks, $"{field} needs a full-screen background Image with Raycast Target ✔.");
        }
    }

    [Test]
    public void Timer_IsPartOfTheHud()
    {
        GameObject timer = ((Component)ui.FindProperty("timerText").objectReferenceValue)?.gameObject;
        Assert.IsNotNull(timer, "Assign UIManager > Timer Text.");
        Assert.IsTrue(timer.transform.IsChildOf(Panel("hudPanel").transform), "Put TimerText inside the HUD.");
    }

    [Test]
    public void AudioManager_HasChallengeSounds()
    {
        MonoBehaviour audio = FindAll<MonoBehaviour>().FirstOrDefault(m => m != null && m.GetType().Name == "AudioManager");
        Assert.IsNotNull(audio, "AudioManager missing (Stage 7).");
        var so = new SerializedObject(audio);
        Assert.IsNotNull(so.FindProperty("countdownTick").objectReferenceValue, "Assign AudioManager > Countdown Tick (countdown_tick.wav).");
        Assert.IsNotNull(so.FindProperty("buzzer").objectReferenceValue, "Assign AudioManager > Buzzer (buzzer.wav).");
    }

    [TestCase("countdown_tick")]
    [TestCase("buzzer")]
    public void ChallengeSound_IsImported(string name)
    {
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Audio/{name}.wav");
        Assert.IsNotNull(clip, $"Assets/Audio/{name}.wav not found. Copy the Stage 9 sounds into Assets/Audio.");
        Assert.AreEqual(1, clip.channels, "Sound effects should be mono.");
    }

    // ---------------------------------------------------------------- helpers

    private GameObject Panel(string field)
    {
        Object value = ui.FindProperty(field).objectReferenceValue;
        Assert.IsNotNull(value, $"Assign UIManager > {ObjectNames.NicifyVariableName(field)}.");
        return (GameObject)value;
    }

    private T[] FindAll<T>() where T : Component
    {
        return scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
    }
}
