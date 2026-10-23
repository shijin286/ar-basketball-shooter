using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// STAGE 6 automated checks (EditMode tab) for the game flow:
/// GameManager states (Title -> PlacingHoop -> Playing <-> Paused, Restart, Reposition),
/// UIManager showing the right panel for each state, and the SafeArea maths.
/// </summary>
public class Stage6GameFlowTests
{
    private readonly List<Object> created = new List<Object>();
    private Component manager;
    private Type stateType;

    [SetUp]
    public void SetUp()
    {
        Type managerType = FindGameType("GameManager");
        stateType = managerType.GetNestedType("GameState");
        Assert.IsNotNull(stateType, "GameManager.GameState not found - copy the Stage 6 GameManager.cs.");

        GameObject go = new GameObject("TestGameManager");
        created.Add(go);
        manager = go.AddComponent(managerType); // Awake/Start don't run in Edit Mode: no AR objects are needed
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f; // a paused test must never leave the Editor frozen
        foreach (Object o in created)
        {
            if (o != null) Object.DestroyImmediate(o);
        }
        created.Clear();
    }

    // ---------------------------------------------------------------- GameManager states

    [Test]
    public void Game_StartsOnTitleScreen()
    {
        Assert.AreEqual("Title", State);
    }

    [Test]
    public void StartGame_WithoutHoop_GoesToPlacingHoop()
    {
        Call(manager, "StartGame");
        Assert.AreEqual("PlacingHoop", State, "After Start Game the player must place the hoop first.");
    }

    [Test]
    public void PlacingTheHoop_StartsPlaying()
    {
        StartAndPlaceHoop();
        Assert.AreEqual("Playing", State);
    }

    [Test]
    public void Pause_FreezesTime_AndResumeContinues()
    {
        StartAndPlaceHoop();

        Call(manager, "PauseGame");
        Assert.AreEqual("Paused", State);
        Assert.AreEqual(0f, Time.timeScale, "Pause must freeze physics (Time.timeScale = 0).");

        Call(manager, "ResumeGame");
        Assert.AreEqual("Playing", State, "Resume must go back to where the player was.");
        Assert.AreEqual(1f, Time.timeScale, "Resume must unfreeze time.");
    }

    [Test]
    public void PauseWhilePlacing_ResumesPlacing()
    {
        Call(manager, "StartGame");
        Call(manager, "PauseGame");
        Call(manager, "ResumeGame");
        Assert.AreEqual("PlacingHoop", State);
    }

    [Test]
    public void TogglePause_PausesAndResumes()
    {
        StartAndPlaceHoop();
        Call(manager, "TogglePause");
        Assert.AreEqual("Paused", State);
        Call(manager, "TogglePause");
        Assert.AreEqual("Playing", State);
    }

    [Test]
    public void Pause_IsIgnoredOnTitleScreen()
    {
        Call(manager, "PauseGame");
        Assert.AreEqual("Title", State);
        Assert.AreEqual(1f, Time.timeScale);
    }

    [Test]
    public void Restart_ClearsScore_AndKeepsPlaying()
    {
        StartAndPlaceHoop();
        Call(manager, "RegisterShot");
        Call(manager, "RegisterShot");
        Call(manager, "RegisterBasket", new[] { FindGameType("BasketballController") }, new object[] { null });
        Call(manager, "PauseGame");

        Call(manager, "RestartGame");

        Assert.AreEqual(0, Get(manager, "Score"), "Restart must reset the score.");
        Assert.AreEqual(0, Get(manager, "Shots"), "Restart must reset the shots.");
        Assert.AreEqual("Playing", State, "Restart keeps the hoop and continues playing.");
        Assert.AreEqual(1f, Time.timeScale, "Restart from the pause screen must unfreeze time.");
    }

    [Test]
    public void RepositioningTheHoop_GoesBackToPlacing()
    {
        StartAndPlaceHoop();
        CallPrivate(manager, "OnHoopRemoved", Type.EmptyTypes);
        Assert.AreEqual("PlacingHoop", State);
    }

    [Test]
    public void StateChanged_IsRaisedForEveryChange()
    {
        var states = new List<string>();
        EventInfo stateChanged = manager.GetType().GetEvent("StateChanged");
        Assert.IsNotNull(stateChanged, "GameManager.StateChanged event not found.");

        // Build an Action<GameState> handler without knowing GameState at compile time.
        MethodInfo record = typeof(StateRecorder).GetMethod(nameof(StateRecorder.Record)).MakeGenericMethod(stateType);
        var recorder = new StateRecorder(states);
        stateChanged.AddEventHandler(manager, Delegate.CreateDelegate(stateChanged.EventHandlerType, recorder, record));

        StartAndPlaceHoop();
        Call(manager, "PauseGame");
        Call(manager, "ResumeGame");

        CollectionAssert.AreEqual(new[] { "PlacingHoop", "Playing", "Paused", "Playing" }, states);
    }

    // ---------------------------------------------------------------- UIManager panels

    [TestCase("Title", true, false, false, false, false, TestName = "Panels_Title_ShowsOnlyTitle")]
    [TestCase("PlacingHoop", false, true, true, false, true, TestName = "Panels_Placing_ShowsHudAndPlaceInstructions")]
    [TestCase("Playing", false, true, false, false, true, TestName = "Panels_Playing_ShowsHud")]
    [TestCase("Paused", false, true, false, true, false, TestName = "Panels_Paused_ShowsPauseMenu")]
    public void UIManager_ShowsRightPanels(string state, bool title, bool hud, bool place, bool pause, bool pauseButton)
    {
        Component ui = CreateComponent("UIManager");
        var panels = new Dictionary<string, GameObject>();
        var so = new SerializedObject(ui);
        foreach (string field in new[] { "titlePanel", "hudPanel", "placeHoopPanel", "aimShootPanel", "pausePanel", "pauseButton" })
        {
            GameObject panel = new GameObject(field);
            created.Add(panel);
            panels[field] = panel;
            so.FindProperty(field).objectReferenceValue = panel;
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        Call(ui, "ShowState", new[] { stateType }, Enum.Parse(stateType, state));

        Assert.AreEqual(title, panels["titlePanel"].activeSelf, "Title panel");
        Assert.AreEqual(hud, panels["hudPanel"].activeSelf, "HUD");
        Assert.AreEqual(place, panels["placeHoopPanel"].activeSelf, "Place Hoop instructions");
        Assert.AreEqual(pause, panels["pausePanel"].activeSelf, "Pause panel");
        Assert.AreEqual(pauseButton, panels["pauseButton"].activeSelf, "Pause button");
    }

    [Test]
    public void UIManager_ShowsAimInstructionsWhenPlayStarts()
    {
        Component ui = CreateComponent("UIManager");
        GameObject aim = new GameObject("aimShootPanel");
        created.Add(aim);
        aim.SetActive(false);
        var so = new SerializedObject(ui);
        so.FindProperty("aimShootPanel").objectReferenceValue = aim;
        so.ApplyModifiedPropertiesWithoutUndo();

        Call(ui, "ShowState", new[] { stateType }, Enum.Parse(stateType, "PlacingHoop"));
        Call(ui, "ShowState", new[] { stateType }, Enum.Parse(stateType, "Playing"));
        Assert.IsTrue(aim.activeSelf, "Aim & Shoot instructions must appear once the hoop is placed.");

        Call(ui, "Refresh", new[] { typeof(int), typeof(int) }, 0, 1);
        Assert.IsFalse(aim.activeSelf, "Aim & Shoot instructions must hide after the first shot.");
    }

    // ---------------------------------------------------------------- SafeArea

    [Test]
    public void SafeArea_KeepsUiAwayFromNotchAndGestureBar()
    {
        // 1080 x 2400 phone: 110 px notch at the top, 66 px gesture bar at the bottom.
        object[] args = { new Rect(0f, 66f, 1080f, 2400f - 66f - 110f), new Vector2(1080f, 2400f), null, null };
        bool ok = (bool)CallStatic("SafeArea", "CalculateAnchors", SafeAreaTypes, args);
        Vector2 min = (Vector2)args[2];
        Vector2 max = (Vector2)args[3];

        Assert.IsTrue(ok);
        Assert.AreEqual(0f, min.x, 0.0001f);
        Assert.AreEqual(66f / 2400f, min.y, 0.0001f, "Bottom must start above the gesture bar.");
        Assert.AreEqual(1f, max.x, 0.0001f);
        Assert.AreEqual((2400f - 110f) / 2400f, max.y, 0.0001f, "Top must stop below the notch.");
    }

    [Test]
    public void SafeArea_IgnoresInvalidScreen()
    {
        object[] args = { new Rect(0f, 0f, 0f, 0f), Vector2.zero, null, null };
        Assert.IsFalse((bool)CallStatic("SafeArea", "CalculateAnchors", SafeAreaTypes, args));
    }

    // ---------------------------------------------------------------- helpers

    private static readonly Type[] SafeAreaTypes =
    {
        typeof(Rect), typeof(Vector2), typeof(Vector2).MakeByRefType(), typeof(Vector2).MakeByRefType()
    };

    private string State => Get(manager, "State").ToString();

    private void StartAndPlaceHoop()
    {
        Call(manager, "StartGame");
        CallPrivate(manager, "OnHoopPlaced", new[] { typeof(Transform) }, new object[] { null });
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

    /// <summary>Calls the private event handlers that ARPlanePlacement would normally trigger.</summary>
    private static object CallPrivate(object target, string method, Type[] types, params object[] args)
    {
        MethodInfo info = target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance, null, types, null);
        Assert.IsNotNull(info, $"{target.GetType().Name}.{method} not found - is the script up to date?");
        return info.Invoke(target, args);
    }

    private static object CallStatic(string typeName, string method, Type[] types, object[] args)
    {
        MethodInfo info = FindGameType(typeName).GetMethod(method, BindingFlags.Public | BindingFlags.Static, null, types, null);
        Assert.IsNotNull(info, $"{typeName}.{method} not found - is the script up to date?");
        return info.Invoke(null, args);
    }

    private static object Get(object target, string property)
    {
        PropertyInfo info = target.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(info, $"{target.GetType().Name}.{property} not found - is the script up to date?");
        return info.GetValue(target);
    }

    /// <summary>Collects state names from GameManager.StateChanged.</summary>
    private class StateRecorder
    {
        private readonly List<string> states;
        public StateRecorder(List<string> states) { this.states = states; }
        public void Record<T>(T state) => states.Add(state.ToString());
    }
}
