using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// STAGE 9 automated checks (EditMode tab) for the 60-second challenge and leaderboard:
///  - Leaderboard: ranking, top-10 limit, saving and loading, damaged files, name cleaning.
///  - GameManager: the clock only runs while playing, countdown beeps, time up, buzzer beaters,
///    results, saving once, Play Again, Main Menu, Free Play has no clock.
///  - UIManager: time format, leaderboard text, results panel.
/// The leaderboard file used here is a temporary file, so your real scores are never touched.
/// </summary>
public class Stage9ChallengeTests
{
    private const string PlayerNameKey = "ARBasketball.PlayerName";

    private readonly List<Object> created = new List<Object>();
    private Type leaderboardType;
    private Type stateType;
    private Component manager;
    private object board;
    private string boardPath;
    private string savedPlayerName;

    [SetUp]
    public void SetUp()
    {
        leaderboardType = FindGameType("Leaderboard");
        Type managerType = FindGameType("GameManager");
        stateType = managerType.GetNestedType("GameState");

        boardPath = Path.Combine(Path.GetTempPath(), $"arbasketball_test_{Guid.NewGuid():N}.json");
        board = NewBoard(boardPath);

        GameObject go = new GameObject("TestGameManager");
        created.Add(go);
        manager = go.AddComponent(managerType); // Awake/Start don't run in Edit Mode
        Call(manager, "UseLeaderboard", new[] { leaderboardType }, board);

        savedPlayerName = PlayerPrefs.HasKey(PlayerNameKey) ? PlayerPrefs.GetString(PlayerNameKey) : null;
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        foreach (Object o in created)
        {
            if (o != null) Object.DestroyImmediate(o);
        }
        created.Clear();

        foreach (string file in new[] { boardPath, boardPath + ".tmp" })
        {
            if (File.Exists(file)) File.Delete(file);
        }

        if (savedPlayerName == null) PlayerPrefs.DeleteKey(PlayerNameKey);
        else PlayerPrefs.SetString(PlayerNameKey, savedPlayerName);
    }

    // ---------------------------------------------------------------- Leaderboard

    [Test]
    public void Leaderboard_RanksByScoreThenFewerShots()
    {
        Assert.AreEqual(1, Submit(board, "Ana", 5, 10));
        Assert.AreEqual(1, Submit(board, "Ben", 8, 20), "A higher score goes to the top.");
        Assert.AreEqual(2, Submit(board, "Cy", 5, 7), "Same score as Ana but fewer shots ranks higher.");

        CollectionAssert.AreEqual(new[] { "Ben", "Cy", "Ana" }, Names(board));
    }

    [Test]
    public void Leaderboard_KeepsOnlyTheTop10()
    {
        for (int score = 1; score <= 12; score++) Submit(board, "P" + score, score, 10);

        Assert.AreEqual(10, Names(board).Length, "Only 10 entries are kept.");
        Assert.AreEqual(0, Submit(board, "Low", 2, 10), "A score below the 10th place must not get in.");
        Assert.IsFalse(Qualifies(board, 3, 10), "Equal to 10th place (same shots) loses to the earlier score.");
        Assert.IsTrue(Qualifies(board, 3, 9), "Equal score with fewer shots beats 10th place.");
    }

    [Test]
    public void Leaderboard_ZeroNeverQualifies()
    {
        Assert.IsFalse(Qualifies(board, 0, 15));
        Assert.AreEqual(0, Submit(board, "Nobody", 0, 15));
    }

    [Test]
    public void Leaderboard_IsStillThereAfterRestartingTheApp()
    {
        Submit(board, "Ana", 7, 12);
        Submit(board, "Ben", 9, 15);

        object reloaded = NewBoard(boardPath); // like opening the app again
        CollectionAssert.AreEqual(new[] { "Ben", "Ana" }, Names(reloaded));
    }

    [Test]
    public void Leaderboard_DamagedFileStartsFresh()
    {
        File.WriteAllText(boardPath, "this is not { valid json");

        object reloaded = NewBoard(boardPath);
        Assert.IsEmpty(Names(reloaded), "A damaged file must not crash the game.");
        Assert.AreEqual(1, Submit(reloaded, "Ana", 3, 5), "Saving must still work afterwards.");
    }

    [TestCase("  Ana  ", "Ana")]
    [TestCase("<color=red>Bob</color>", "Bob")]
    [TestCase("a<b", "ab")]
    [TestCase("", "Player")]
    [TestCase("<>", "Player")]
    [TestCase("ABCDEFGHIJKLMNOP", "ABCDEFGHIJKL")]
    public void Leaderboard_CleansNames(string input, string expected)
    {
        Assert.AreEqual(expected, CallStatic(leaderboardType, "SanitizeName", new[] { typeof(string) }, input));
    }

    // ---------------------------------------------------------------- challenge flow

    [Test]
    public void Challenge_StartsWithAFullClock()
    {
        Call(manager, "StartChallenge");
        Assert.AreEqual("Challenge", Get(manager, "Mode").ToString());
        Assert.AreEqual("PlacingHoop", State);
        Assert.AreEqual(60f, TimeLeft, 0.001f);
    }

    [Test]
    public void Clock_RunsOnlyWhilePlaying()
    {
        Call(manager, "StartChallenge");
        Tick(5f);
        Assert.AreEqual(60f, TimeLeft, 0.001f, "The clock must wait while the hoop is being placed.");

        PlaceHoop();
        Tick(10f);
        Assert.AreEqual(50f, TimeLeft, 0.001f);

        Call(manager, "PauseGame");
        Tick(10f);
        Assert.AreEqual(50f, TimeLeft, 0.001f, "The clock must stop while paused.");

        Call(manager, "ResumeGame");
        Tick(5f);
        Assert.AreEqual(45f, TimeLeft, 0.001f);
    }

    [Test]
    public void TimeUp_EndsTheGame()
    {
        int timeUps = 0;
        Action handler = () => timeUps++;
        manager.GetType().GetEvent("TimeUp").AddEventHandler(manager, handler);

        StartChallengeWithHoop();
        Tick(61f);

        Assert.AreEqual("GameOver", State);
        Assert.AreEqual(0f, TimeLeft, 0.001f);
        Assert.AreEqual(1, timeUps, "TimeUp must fire exactly once.");
    }

    [Test]
    public void Countdown_BeepsForTheLast5Seconds()
    {
        var beeps = new List<int>();
        Action<int> handler = seconds => beeps.Add(seconds);
        manager.GetType().GetEvent("CountdownTick").AddEventHandler(manager, handler);

        StartChallengeWithHoop();
        for (int i = 0; i < 130; i++) Tick(0.5f);

        CollectionAssert.AreEqual(new[] { 5, 4, 3, 2, 1 }, beeps);
    }

    [Test]
    public void BuzzerBeater_CountsDuringGrace_ThenResultsAreFinal()
    {
        var finished = new List<bool>();
        Action<bool> handler = qualifies => finished.Add(qualifies);
        manager.GetType().GetEvent("ChallengeFinished").AddEventHandler(manager, handler);

        StartChallengeWithHoop();
        Tick(60f);
        RegisterBasket(); // ball released before the buzzer drops in
        Assert.AreEqual(1, Get(manager, "Score"), "A basket during the grace time must count.");

        Tick(1f);
        Assert.IsEmpty(finished, "Results must wait for the grace time (2 s).");
        Tick(1.5f);
        CollectionAssert.AreEqual(new[] { true }, finished, "Score 1 on an empty board qualifies.");
        Assert.IsTrue((bool)Get(manager, "ResultsReady"));

        RegisterBasket();
        Assert.AreEqual(1, Get(manager, "Score"), "After the results are final, nothing more can score.");
    }

    [Test]
    public void ChallengeFinished_SaysNoWhenThereWereNoBaskets()
    {
        var finished = new List<bool>();
        Action<bool> handler = qualifies => finished.Add(qualifies);
        manager.GetType().GetEvent("ChallengeFinished").AddEventHandler(manager, handler);

        StartChallengeWithHoop();
        Tick(60f);
        Tick(3f);

        CollectionAssert.AreEqual(new[] { false }, finished);
    }

    [Test]
    public void SubmitScore_SavesOnceWithRank()
    {
        StartChallengeWithHoop();
        Tick(30f);
        RegisterBasket();
        RegisterBasket();
        Tick(30f);
        Tick(3f);

        Assert.AreEqual(1, Call(manager, "SubmitChallengeScore", new[] { typeof(string) }, "Tester"));
        Assert.AreEqual(1, Call(manager, "SubmitChallengeScore", new[] { typeof(string) }, "Again"), "Saving twice returns the same rank...");
        CollectionAssert.AreEqual(new[] { "Tester" }, Names(board), "...and must not add a second entry.");
        Assert.AreEqual("Tester", Get(manager, "LastPlayerName"), "The name is remembered for next time.");
    }

    [Test]
    public void FreePlay_HasNoTimeLimit()
    {
        Call(manager, "StartGame");
        PlaceHoop();
        Tick(1000f);
        Assert.AreEqual("Playing", State);
    }

    [Test]
    public void PlayAgain_ResetsClockAndScore()
    {
        StartChallengeWithHoop();
        RegisterBasket();
        Tick(60f);

        Call(manager, "RestartGame");

        Assert.AreEqual("Playing", State);
        Assert.AreEqual(60f, TimeLeft, 0.001f);
        Assert.AreEqual(0, Get(manager, "Score"));
        Assert.IsFalse((bool)Get(manager, "ResultsReady"));
        Assert.AreEqual("Challenge", Get(manager, "Mode").ToString(), "Play Again keeps the challenge mode.");
    }

    [Test]
    public void MainMenu_ReturnsToTitle()
    {
        StartChallengeWithHoop();
        RegisterBasket();
        Tick(63f);

        Call(manager, "ReturnToTitle");

        Assert.AreEqual("Title", State);
        Assert.AreEqual("FreePlay", Get(manager, "Mode").ToString());
        Assert.AreEqual(0, Get(manager, "Score"));
    }

    // ---------------------------------------------------------------- UIManager

    [TestCase(60f, "1:00")]
    [TestCase(42.3f, "0:43")]
    [TestCase(9.01f, "0:10")]
    [TestCase(0.2f, "0:01")]
    [TestCase(0f, "0:00")]
    [TestCase(-3f, "0:00")]
    public void Timer_IsFormattedAsMinutesAndSeconds(float seconds, string expected)
    {
        Assert.AreEqual(expected, CallStatic(FindGameType("UIManager"), "FormatTime", new[] { typeof(float) }, seconds));
    }

    [Test]
    public void LeaderboardText_ListsInOrderAndHighlightsYou()
    {
        Submit(board, "Ana", 5, 10);
        Submit(board, "Ben", 8, 12);
        Submit(board, "Cy", 3, 9);

        string text = FormatBoard(board, 2);
        string[] lines = text.Split('\n');

        Assert.AreEqual(3, lines.Length);
        StringAssert.Contains("Ben", lines[0]);
        StringAssert.Contains("Ana", lines[1]);
        StringAssert.Contains("Cy", lines[2]);
        StringAssert.Contains("<color", lines[1], "Your new rank must be highlighted.");
        StringAssert.DoesNotContain("<color", lines[0]);
    }

    [Test]
    public void LeaderboardText_EmptyBoardInvitesToPlay()
    {
        StringAssert.Contains("No scores yet", FormatBoard(board, 0));
    }

    [Test]
    public void ResultsPanel_ShowsWhenTimeIsUp()
    {
        Component ui = CreateComponent("UIManager");
        var panels = new Dictionary<string, GameObject>();
        var so = new UnityEditor.SerializedObject(ui);
        foreach (string field in new[] { "titlePanel", "hudPanel", "pausePanel", "pauseButton", "gameOverPanel", "leaderboardPanel" })
        {
            GameObject panel = new GameObject(field);
            created.Add(panel);
            panels[field] = panel;
            so.FindProperty(field).objectReferenceValue = panel;
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        Call(ui, "ShowState", new[] { stateType }, Enum.Parse(stateType, "GameOver"));

        Assert.IsTrue(panels["gameOverPanel"].activeSelf, "Results panel");
        Assert.IsTrue(panels["hudPanel"].activeSelf, "HUD stays visible behind the results");
        Assert.IsFalse(panels["pauseButton"].activeSelf, "No pausing on the results screen");
        Assert.IsFalse(panels["titlePanel"].activeSelf);
        Assert.IsFalse(panels["pausePanel"].activeSelf);
        Assert.IsFalse(panels["leaderboardPanel"].activeSelf, "The leaderboard opens only when asked");
    }

    // ---------------------------------------------------------------- helpers

    private string State => Get(manager, "State").ToString();
    private float TimeLeft => (float)Get(manager, "TimeRemaining");

    private void Tick(float seconds) => Call(manager, "Tick", new[] { typeof(float) }, seconds);

    private void RegisterBasket() =>
        Call(manager, "RegisterBasket", new[] { FindGameType("BasketballController") }, new object[] { null });

    private void PlaceHoop()
    {
        MethodInfo onHoopPlaced = manager.GetType().GetMethod("OnHoopPlaced", BindingFlags.NonPublic | BindingFlags.Instance,
            null, new[] { typeof(Transform) }, null);
        Assert.IsNotNull(onHoopPlaced, "GameManager.OnHoopPlaced not found - is the script up to date?");
        onHoopPlaced.Invoke(manager, new object[] { null });
    }

    private void StartChallengeWithHoop()
    {
        Call(manager, "StartChallenge");
        PlaceHoop();
    }

    private object NewBoard(string path) => Activator.CreateInstance(leaderboardType, path, 10);

    private int Submit(object leaderboard, string name, int score, int shots) =>
        (int)Call(leaderboard, "Submit", new[] { typeof(string), typeof(int), typeof(int), typeof(float) },
            name, score, shots, shots > 0 ? score / (float)shots : 0f);

    private bool Qualifies(object leaderboard, int score, int shots) =>
        (bool)Call(leaderboard, "Qualifies", new[] { typeof(int), typeof(int) }, score, shots);

    private static string[] Names(object leaderboard)
    {
        var entries = (IEnumerable)leaderboard.GetType().GetProperty("Entries").GetValue(leaderboard);
        return entries.Cast<object>().Select(e => (string)e.GetType().GetField("name").GetValue(e)).ToArray();
    }

    private string FormatBoard(object leaderboard, int highlight)
    {
        object entries = leaderboard.GetType().GetProperty("Entries").GetValue(leaderboard);
        MethodInfo format = FindGameType("UIManager").GetMethod("FormatLeaderboard", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(format, "UIManager.FormatLeaderboard not found - is the script up to date?");
        return ((string)format.Invoke(null, new[] { entries, highlight })).Replace("\r", "");
    }

    private Component CreateComponent(string typeName)
    {
        GameObject go = new GameObject("Test" + typeName);
        created.Add(go);
        return go.AddComponent(FindGameType(typeName));
    }

    private static Type FindGameType(string name)
    {
        Type type = Type.GetType(name + ", Assembly-CSharp");
        Assert.IsNotNull(type, $"Class {name} not found. Copy {name}.cs into Assets/Scripts and fix any Console errors.");
        return type;
    }

    private static object Call(object target, string method)
    {
        return Call(target, method, Type.EmptyTypes, new object[0]);
    }

    private static object Call(object target, string method, Type[] types, params object[] args)
    {
        MethodInfo info = target.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.Instance, null, types, null);
        Assert.IsNotNull(info, $"{target.GetType().Name}.{method} not found - is the script up to date?");
        return info.Invoke(target, args);
    }

    private static object CallStatic(Type type, string method, Type[] types, params object[] args)
    {
        MethodInfo info = type.GetMethod(method, BindingFlags.Public | BindingFlags.Static, null, types, null);
        Assert.IsNotNull(info, $"{type.Name}.{method} not found - is the script up to date?");
        return info.Invoke(null, args);
    }

    private static object Get(object target, string property)
    {
        PropertyInfo info = target.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(info, $"{target.GetType().Name}.{property} not found - is the script up to date?");
        return info.GetValue(target);
    }
}
