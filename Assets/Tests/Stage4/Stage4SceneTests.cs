using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// STAGE 4 automated checks for the scene: BasketballShooter is added and the
/// temporary Test Drop button from Stage 3 is gone.
/// Run them from Window > General > Test Runner > EditMode > Run All.
/// </summary>
public class Stage4SceneTests
{
    private const string ScenePath = "Assets/Scenes/ARBasketballScene.unity";

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

    [Test]
    public void BasketballShooter_IsInScene()
    {
        MonoBehaviour shooter = FindScript("BasketballShooter");
        Assert.IsNotNull(shooter, "Add the BasketballShooter script to the GameSystems object.");
        Assert.IsNotNull(FindScript("BallSpawner"), "BasketballShooter needs a BallSpawner in the scene (Stage 3).");

        var so = new SerializedObject(shooter);
        float min = so.FindProperty("minShotForce").floatValue;
        float max = so.FindProperty("maxShotForce").floatValue;
        Assert.Greater(min, 0f, "Min Shot Force must be above 0.");
        Assert.GreaterOrEqual(max, min, "Max Shot Force must be at least Min Shot Force.");
        Assert.LessOrEqual(max, 8f, "Max Shot Force above 8 N·s (over 13 m/s) is far too strong for a room.");
    }

    [Test]
    public void TestDropButton_IsRemoved()
    {
        bool stillThere = scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<Button>(true))
            .Any(b => b.name == "DropBallButton");
        Assert.IsFalse(stillThere, "Delete the temporary DropBallButton - swipe shooting replaces it.");
    }

    [Test]
    public void BallSpawner_HasNoEmptyUiSlots()
    {
        MonoBehaviour spawner = FindScript("BallSpawner");
        Assert.IsNotNull(spawner, "BallSpawner missing.");

        SerializedProperty list = new SerializedObject(spawner).FindProperty("showWhenHoopPlaced");
        for (int i = 0; i < list.arraySize; i++)
        {
            Assert.IsNotNull(list.GetArrayElementAtIndex(i).objectReferenceValue,
                $"BallSpawner > Show When Hoop Placed, element {i} is empty. Remove it (it was the deleted Test Drop button).");
        }
    }
}
