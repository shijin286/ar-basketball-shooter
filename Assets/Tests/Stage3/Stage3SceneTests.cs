using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// STAGE 3 automated checks for the scene: BallSpawner and the Reset Ball button.
/// Run them from Window > General > Test Runner > EditMode > Run All.
/// </summary>
public class Stage3SceneTests
{
    private const string ScenePath = "Assets/Scenes/ARBasketballScene.unity";
    private const string BallPrefabPath = "Assets/Prefabs/Basketball.prefab";

    private Scene scene;
    private bool openedByTest;

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
    }

    [OneTimeTearDown]
    public void CloseScene()
    {
        if (openedByTest && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
    }

    private MonoBehaviour FindScript(string typeName)
    {
        return scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true))
            .FirstOrDefault(m => m != null && m.GetType().Name == typeName);
    }

    private Button FindButton(string name)
    {
        return scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<Button>(true))
            .FirstOrDefault(b => b.name == name);
    }

    private static bool CallsMethod(Button button, string method)
    {
        return Enumerable.Range(0, button.onClick.GetPersistentEventCount())
            .Any(i => button.onClick.GetPersistentMethodName(i) == method &&
                      button.onClick.GetPersistentTarget(i) != null);
    }

    [Test]
    public void BallSpawner_IsWired()
    {
        MonoBehaviour spawner = FindScript("BallSpawner");
        Assert.IsNotNull(spawner, "Add the BallSpawner script to the GameSystems object.");

        var so = new SerializedObject(spawner);
        Object ball = so.FindProperty("ballPrefab").objectReferenceValue;
        Assert.IsNotNull(ball, "Assign Prefabs/Basketball to BallSpawner > Ball Prefab.");

        string assignedPath = AssetDatabase.GetAssetPath(ball);
        Assert.AreEqual(BallPrefabPath, assignedPath, "Ball Prefab should be the Basketball prefab asset (not a scene object).");

        Assert.IsNotNull(so.FindProperty("floorMaterial").objectReferenceValue,
            "Assign Materials/Physics/Floor_Physics to BallSpawner > Floor Material.");
    }

    [Test]
    public void ResetBallButton_CallsResetBall()
    {
        Button button = FindButton("ResetBallButton");
        Assert.IsNotNull(button, "Create a UI Button named ResetBallButton.");
        Assert.IsTrue(CallsMethod(button, "ResetBall"), "ResetBallButton > On Click () must call BallSpawner.ResetBall.");
    }

    [Test]
    public void DropBallButton_CallsTestDropBall()
    {
        Button button = FindButton("DropBallButton");
        if (button == null) Assert.Ignore("DropBallButton is optional (Stage 3 testing only).");
        Assert.IsTrue(CallsMethod(button, "TestDropBall"), "DropBallButton > On Click () must call BallSpawner.TestDropBall.");
    }

    [Test]
    public void BallButtons_AreShownOnlyAfterPlacement()
    {
        MonoBehaviour spawner = FindScript("BallSpawner");
        Assert.IsNotNull(spawner, "BallSpawner missing.");
        Button reset = FindButton("ResetBallButton");
        Assert.IsNotNull(reset, "ResetBallButton missing.");

        SerializedProperty list = new SerializedObject(spawner).FindProperty("showWhenHoopPlaced");
        bool listed = Enumerable.Range(0, list.arraySize)
            .Any(i => list.GetArrayElementAtIndex(i).objectReferenceValue == reset.gameObject);
        Assert.IsTrue(listed, "Add ResetBallButton to BallSpawner > Show When Hoop Placed.");
    }
}
