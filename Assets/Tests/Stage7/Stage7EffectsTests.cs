using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// STAGE 7 automated checks (EditMode tab): sound files, impact sound rules,
/// the confetti prefab, and the scene wiring (AudioManager, ScoreEffects,
/// one AudioListener, ButtonFeedback on every button).
/// Listening to the sounds and watching the confetti is done by hand (see the Stage 7 guide).
/// </summary>
public class Stage7EffectsTests
{
    private const string AudioFolder = "Assets/Audio/";
    private const string EffectPrefabPath = "Assets/Prefabs/BasketEffect.prefab";
    private const string ScenePath = "Assets/Scenes/ARBasketballScene.unity";

    private static readonly string[] SoundNames =
        { "ball_bounce", "rim_clank", "backboard_thud", "net_swish", "shoot_whoosh", "button_click", "score_chime" };

    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object o in created)
        {
            if (o != null) Object.DestroyImmediate(o);
        }
        created.Clear();
    }

    // ---------------------------------------------------------------- sound files

    [TestCaseSource(nameof(SoundNames))]
    public void SoundFile_IsImportedAndShort(string name)
    {
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + name + ".wav");
        Assert.IsNotNull(clip, $"{AudioFolder}{name}.wav not found. Copy the Stage 7 Audio folder into Assets.");
        Assert.That(clip.length, Is.InRange(0.03f, 1.5f), "Sound effects should be short.");
        Assert.AreEqual(1, clip.channels, "Sound effects should be mono (3D sound needs mono).");
    }

    // ---------------------------------------------------------------- impact rules

    [TestCase("BasketballHoop/Rim/RimColliders/RimSegment_03", "Rim", TestName = "Surface_RimSegment_IsRim")]
    [TestCase("BasketballHoop/Rim/Bracket", "Rim", TestName = "Surface_Bracket_IsRim")]
    [TestCase("BasketballHoop/Backboard/BoardPanel", "Backboard", TestName = "Surface_BoardPanel_IsBackboard")]
    [TestCase("BasketballHoop/Stand/Pole", "Backboard", TestName = "Surface_Pole_IsBackboard")]
    [TestCase("BasketballHoop/VirtualFloor", "Floor", TestName = "Surface_VirtualFloor_IsFloor")]
    [TestCase("Trackables/ARPlane", "Floor", TestName = "Surface_ARPlane_IsFloor")]
    public void AudioManager_RecognisesWhatWasHit(string path, string expected)
    {
        Collider collider = CreatePath(path).gameObject.AddComponent<BoxCollider>();
        object surface = CallStatic("AudioManager", "GetSurface", new[] { typeof(Collider) }, collider);
        Assert.AreEqual(expected, surface.ToString());
    }

    [Test]
    public void AudioManager_HarderHitsAreLouder()
    {
        Type[] types = { typeof(float), typeof(float), typeof(float) };
        float Volume(float speed) => (float)CallStatic("AudioManager", "ImpactVolume", types, speed, 0.5f, 7f);

        Assert.AreEqual(0f, Volume(0.3f), "Tiny touches must be silent.");
        Assert.Less(Volume(2f), Volume(5f), "A harder hit must be louder.");
        Assert.AreEqual(1f, Volume(7f), 0.001f);
        Assert.AreEqual(1f, Volume(20f), 0.001f, "Volume must never go above 1.");
    }

    // ---------------------------------------------------------------- particle effect

    [Test]
    public void BasketEffect_IsAShortConfettiBurst()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EffectPrefabPath);
        Assert.IsNotNull(prefab, "Run Tools > AR Basketball > Create Basket Effect.");
        ParticleSystem system = prefab.GetComponent<ParticleSystem>();
        Assert.IsNotNull(system, "BasketEffect needs a Particle System.");

        ParticleSystem.MainModule main = system.main;
        Assert.IsFalse(main.loop, "The burst must play once, not loop.");
        Assert.IsFalse(main.playOnAwake, "ScoreEffects starts it; Play On Awake must be off.");
        Assert.AreEqual(ParticleSystemSimulationSpace.World, main.simulationSpace);
        Assert.LessOrEqual(main.duration + main.startLifetime.constantMax, 3f, "The effect should be over within 3 seconds.");

        ParticleSystem.EmissionModule emission = system.emission;
        Assert.GreaterOrEqual(emission.burstCount, 1, "The effect needs a burst.");
        Assert.GreaterOrEqual(emission.GetBurst(0).count.constant, 20f, "At least 20 confetti pieces.");
    }

    [Test]
    public void BasketEffect_MaterialIsNotPink()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EffectPrefabPath);
        Assert.IsNotNull(prefab, "Run Tools > AR Basketball > Create Basket Effect.");
        Material material = prefab.GetComponent<ParticleSystemRenderer>().sharedMaterial;
        Assert.IsNotNull(material, "The particle renderer needs a material.");
        Assert.IsTrue(material.shader.isSupported && material.shader.name != "Hidden/InternalErrorShader",
            $"Shader '{material.shader.name}' doesn't work here (pink). Run the tool again in a URP project.");
    }

    // ---------------------------------------------------------------- scene

    [Test]
    public void Scene_IsWiredForAudioAndEffects()
    {
        WithScene(scene =>
        {
            MonoBehaviour[] scripts = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).Where(m => m != null).ToArray();

            MonoBehaviour audio = scripts.FirstOrDefault(m => m.GetType().Name == "AudioManager");
            Assert.IsNotNull(audio, "Add AudioManager to GameSystems.");
            var audioSo = new SerializedObject(audio);
            foreach (string clip in new[] { "floorBounce", "rimHit", "backboardHit", "basketSwish", "basketChime", "shoot", "buttonClick" })
            {
                Assert.IsNotNull(audioSo.FindProperty(clip).objectReferenceValue,
                    $"Assign AudioManager > {ObjectNames.NicifyVariableName(clip)}.");
            }

            MonoBehaviour effects = scripts.FirstOrDefault(m => m.GetType().Name == "ScoreEffects");
            Assert.IsNotNull(effects, "Add ScoreEffects to GameSystems.");
            Assert.IsNotNull(new SerializedObject(effects).FindProperty("basketEffectPrefab").objectReferenceValue,
                "Assign ScoreEffects > Basket Effect Prefab.");
        });
    }

    [Test]
    public void Scene_HasOneAudioListenerOnTheARCamera()
    {
        WithScene(scene =>
        {
            AudioListener[] listeners = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<AudioListener>(true)).ToArray();
            Assert.AreEqual(1, listeners.Length, "There must be exactly one Audio Listener (on the AR camera).");
            Assert.IsTrue(listeners[0].CompareTag("MainCamera"), "The Audio Listener must be on the AR Main Camera, so 3D sounds come from the right place.");
        });
    }

    [Test]
    public void Scene_EveryButtonHasPressFeedback()
    {
        WithScene(scene =>
        {
            Button[] buttons = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Button>(true)).ToArray();
            Assert.IsNotEmpty(buttons, "No buttons found in the scene.");
            foreach (Button button in buttons)
            {
                Assert.IsNotNull(button.GetComponent("ButtonFeedback"), $"Add Button Feedback to '{button.name}'.");
            }
        });
    }

    // ---------------------------------------------------------------- helpers

    private Transform CreatePath(string path)
    {
        Transform parent = null;
        foreach (string part in path.Split('/'))
        {
            GameObject go = new GameObject(part);
            if (parent == null) created.Add(go);
            else go.transform.SetParent(parent, false);
            parent = go.transform;
        }
        return parent;
    }

    private static void WithScene(Action<Scene> check)
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
            check(scene);
        }
        finally
        {
            if (openedByTest) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static object CallStatic(string typeName, string method, Type[] types, params object[] args)
    {
        Type type = Type.GetType(typeName + ", Assembly-CSharp");
        Assert.IsNotNull(type, $"Class {typeName} not found. Copy {typeName}.cs into Assets/Scripts and fix any Console errors.");
        MethodInfo info = type.GetMethod(method, BindingFlags.Public | BindingFlags.Static, null, types, null);
        Assert.IsNotNull(info, $"{typeName}.{method} not found - is the script up to date?");
        return info.Invoke(null, args);
    }
}
