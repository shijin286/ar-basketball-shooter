using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// STAGE 5 automated checks (EditMode tab): the hoop's score trigger, GameManager's
/// counting rules, UIManager's score text and the scene wiring.
/// The real "ball through the hoop" tests are in the PlayMode tab (Stage5ScoringPlayTests),
/// because Unity only sends trigger events in Play mode.
/// </summary>
public class Stage5ScoringSetupTests
{
    private const string HoopPrefabPath = "Assets/Prefabs/BasketballHoop.prefab";
    private const string ScenePath = "Assets/Scenes/ARBasketballScene.unity";

    private readonly List<Object> created = new List<Object>();
    private GameObject hoopPrefab;

    [SetUp]
    public void SetUp()
    {
        hoopPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HoopPrefabPath);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (Object o in created)
        {
            if (o != null) Object.DestroyImmediate(o);
        }
        created.Clear();
    }

    // ---------------------------------------------------------------- hoop trigger

    private BoxCollider GetScoreZone()
    {
        Assert.IsNotNull(hoopPrefab, "Hoop prefab missing (Stage 2).");
        Transform zone = hoopPrefab.transform.Find("Rim/ScoreZone");
        Assert.IsNotNull(zone, "Rim/ScoreZone not found. Run Tools > AR Basketball > Create Hoop Prefab again (Stage 5 version).");
        BoxCollider box = zone.GetComponent<BoxCollider>();
        Assert.IsNotNull(box, "ScoreZone needs a Box Collider.");
        return box;
    }

    [Test]
    public void ScoreZone_IsTriggerWithScript()
    {
        BoxCollider box = GetScoreZone();
        Assert.IsTrue(box.isTrigger, "ScoreZone's Box Collider must be a trigger (Is Trigger ✔).");
        Assert.IsNotNull(box.GetComponent("HoopScoreTrigger"), "ScoreZone needs the HoopScoreTrigger script.");
        Assert.AreEqual(Vector3.zero, box.transform.localPosition, "ScoreZone must sit exactly at the rim centre.");
    }

    [Test]
    public void ScoreZone_IsNarrowEnoughThatRimTouchesDontCount()
    {
        BoxCollider box = GetScoreZone();
        // A ball resting on the rim has its centre about 0.24 m from the rim centre.
        // It only touches the zone if its centre is within half-width + 0.12 m, so the half-width must stay small.
        float halfWidth = Mathf.Max(box.size.x, box.size.z) * 0.5f;
        Assert.That(halfWidth, Is.InRange(0.02f, 0.105f), "ScoreZone is too wide (or too thin).");
    }

    [Test]
    public void ScoreZone_ReachesAboveAndBelowRim()
    {
        BoxCollider box = GetScoreZone();
        float top = box.center.y + box.size.y * 0.5f;
        float bottom = box.center.y - box.size.y * 0.5f;
        Assert.Greater(top, 0.05f, "The zone must start above the rim.");
        Assert.Less(bottom, -0.2f, "The zone must reach well below the rim (into the net).");
    }

    [Test]
    public void ScoreZone_IsTheOnlyTriggerOnTheHoop()
    {
        Assert.IsNotNull(hoopPrefab, "Hoop prefab missing.");
        Collider[] triggers = hoopPrefab.GetComponentsInChildren<Collider>(true).Where(c => c.isTrigger).ToArray();
        Assert.AreEqual(1, triggers.Length, "Only ScoreZone may be a trigger. The rim and backboard must stay solid.");
        Assert.AreEqual("ScoreZone", triggers[0].name);
    }

    // ---------------------------------------------------------------- GameManager

    [Test]
    public void GameManager_CountsShotsAndBaskets()
    {
        Component manager = CreateComponent("GameManager");

        Call(manager, "RegisterShot");
        Call(manager, "RegisterShot");
        Call(manager, "RegisterShot");
        Call(manager, "RegisterBasket", new[] { FindGameType("BasketballController") }, new object[] { null });

        Assert.AreEqual(1, Get(manager, "Score"), "One basket = one point.");
        Assert.AreEqual(3, Get(manager, "Shots"));
        Assert.AreEqual(1, Get(manager, "Baskets"));
        Assert.AreEqual(1f / 3f, (float)Get(manager, "Accuracy"), 0.001f);

        Call(manager, "ResetScore");
        Assert.AreEqual(0, Get(manager, "Score"), "Restart must clear the score.");
        Assert.AreEqual(0, Get(manager, "Shots"), "Restart must clear the shots.");
    }

    [Test]
    public void GameManager_ReportsScoreChangeImmediately()
    {
        Component manager = CreateComponent("GameManager");
        var reported = new List<string>();
        Action<int, int> handler = (score, shots) => reported.Add($"{score}/{shots}");
        manager.GetType().GetEvent("ScoreChanged").AddEventHandler(manager, handler);

        Call(manager, "RegisterShot");
        Call(manager, "RegisterBasket", new[] { FindGameType("BasketballController") }, new object[] { null });

        CollectionAssert.AreEqual(new[] { "0/1", "1/1" }, reported, "ScoreChanged must fire right after every shot and basket.");
    }

    // ---------------------------------------------------------------- UIManager

    [Test]
    public void UIManager_WritesScoreAndShots()
    {
        Component ui = CreateComponent("UIManager");
        TMP_Text score = CreateText("TestScore");
        TMP_Text shots = CreateText("TestShots");

        var so = new SerializedObject(ui);
        so.FindProperty("scoreText").objectReferenceValue = score;
        so.FindProperty("shotsText").objectReferenceValue = shots;
        so.ApplyModifiedPropertiesWithoutUndo();

        Call(ui, "Refresh", new[] { typeof(int), typeof(int) }, 3, 7);

        Assert.AreEqual("Score: 3", score.text);
        Assert.AreEqual("Shots: 7", shots.text);
    }

    // ---------------------------------------------------------------- scene

    [Test]
    public void Scene_HasGameManagerAndWiredUI()
    {
        Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), $"Scene not found at {ScenePath}.");
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedByTest = false;
        if (!scene.IsValid() || !scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            openedByTest = true;
        }

        try
        {
            MonoBehaviour[] scripts = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true))
                .Where(m => m != null).ToArray();

            Assert.IsTrue(scripts.Any(m => m.GetType().Name == "GameManager"), "Add GameManager to GameSystems.");
            MonoBehaviour ui = scripts.FirstOrDefault(m => m.GetType().Name == "UIManager");
            Assert.IsNotNull(ui, "Add UIManager to the Canvas.");

            var so = new SerializedObject(ui);
            Assert.IsNotNull(so.FindProperty("scoreText").objectReferenceValue, "Assign UIManager > Score Text.");
            Assert.IsNotNull(so.FindProperty("shotsText").objectReferenceValue, "Assign UIManager > Shots Text.");
            Assert.IsNotNull(so.FindProperty("basketMessage").objectReferenceValue, "Assign UIManager > Basket Message.");
        }
        finally
        {
            if (openedByTest) EditorSceneManager.CloseScene(scene, true);
        }
    }

    // ---------------------------------------------------------------- helpers

    private Component CreateComponent(string typeName)
    {
        GameObject go = new GameObject("Test" + typeName);
        created.Add(go);
        return go.AddComponent(FindGameType(typeName)); // Awake doesn't run in Edit Mode, so nothing else is needed
    }

    private TMP_Text CreateText(string name)
    {
        GameObject go = new GameObject(name);
        created.Add(go);
        return go.AddComponent<TextMeshPro>();
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

    private static object Get(object target, string property)
    {
        PropertyInfo info = target.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(info, $"{target.GetType().Name}.{property} not found - is the script up to date?");
        return info.GetValue(target);
    }
}
