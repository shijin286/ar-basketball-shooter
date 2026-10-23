using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// STAGE 7 - Editor tool that builds Assets/Prefabs/BasketEffect.prefab:
/// a one-shot burst of colourful confetti that shoots up from under the rim
/// and falls back down, shrinking as it goes.
///
/// Menu: Tools > AR Basketball > Create Basket Effect
///
/// Confetti uses small opaque quads (URP Particles/Unlit), which is cheap on
/// phones and needs no transparency setup. Running it again updates the same assets.
/// </summary>
public class EffectsPrefabBuilder : ScriptableWizard
{
    [Header("Burst")]
    [Range(10, 100)] public int particleCount = 40;
    public float minSpeed = 1.2f;
    public float maxSpeed = 2.6f;

    [Tooltip("Spread of the burst in degrees (0 = straight up).")]
    [Range(5f, 80f)] public float spreadAngle = 35f;

    [Header("Confetti pieces (metres)")]
    public float minSize = 0.015f;
    public float maxSize = 0.035f;
    public float minLifetime = 0.6f;
    public float maxLifetime = 1.1f;

    [Tooltip("How strongly gravity pulls the confetti back down.")]
    [Range(0f, 2f)] public float gravity = 0.6f;

    [Tooltip("Confetti colours (up to 4).")]
    public Color[] colours =
    {
        new Color(1f, 0.55f, 0.1f), new Color(1f, 0.85f, 0.2f), Color.white, new Color(0.2f, 0.6f, 1f)
    };

    private const string PrefabPath = "Assets/Prefabs/BasketEffect.prefab";
    private const string MaterialFolder = "Assets/Materials/Effects";
    private const string MaterialPath = MaterialFolder + "/Confetti.mat";

    [MenuItem("Tools/AR Basketball/Create Basket Effect")]
    private static void OpenWizard()
    {
        DisplayWizard<EffectsPrefabBuilder>("Create Basket Effect", "Create / Update Prefab");
    }

    private void OnWizardUpdate()
    {
        helpString = "Builds " + PrefabPath + " (confetti burst played by ScoreEffects on every basket).";

        if (minSpeed <= 0f || maxSpeed < minSpeed || minSize <= 0f || maxSize < minSize ||
            minLifetime <= 0f || maxLifetime < minLifetime)
        {
            errorString = "Each 'min' value must be above 0 and not larger than its 'max' value.";
            isValid = false;
        }
        else if (colours == null || colours.Length == 0)
        {
            errorString = "Add at least one colour.";
            isValid = false;
        }
        else
        {
            errorString = "";
            isValid = true;
        }
    }

    private void OnWizardCreate()
    {
        EnsureFolder("Assets/Prefabs");
        EnsureFolder(MaterialFolder);
        Material material = GetMaterial();

        GameObject root = null;
        try
        {
            root = BuildEffect(material);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
            if (!success || prefab == null)
            {
                Debug.LogError("[EffectsPrefabBuilder] Could not save " + PrefabPath);
                return;
            }

            AssetDatabase.SaveAssets();
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"[EffectsPrefabBuilder] Saved {PrefabPath} ({particleCount} confetti pieces).");
        }
        finally
        {
            if (root != null) DestroyImmediate(root);
        }
    }

    private GameObject BuildEffect(Material material)
    {
        GameObject root = new GameObject("BasketEffect");
        ParticleSystem system = root.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = system.main;
        main.duration = 1f;
        main.loop = false;
        main.playOnAwake = false; // ScoreEffects calls Play()
        main.startLifetime = new ParticleSystem.MinMaxCurve(minLifetime, maxLifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
        main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.Max(particleCount, 10) * 2;
        main.startColor = new ParticleSystem.MinMaxGradient(BuildGradient()) { mode = ParticleSystemGradientMode.RandomColor };

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)particleCount) });

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = spreadAngle;
        shape.radius = 0.08f;
        shape.rotation = new Vector3(-90f, 0f, 0f); // cones point along Z; turn it to point up

        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f)); // shrink away

        ParticleSystem.RotationOverLifetimeModule spin = system.rotationOverLifetime;
        spin.enabled = true;
        spin.z = new ParticleSystem.MinMaxCurve(-6f, 6f); // radians per second: confetti tumbles

        ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        return root;
    }

    /// <summary>
    /// A gradient made of equal solid bands, one per colour (two keys per band, so at most
    /// 4 colours fit in Unity's 8-key limit). RandomColor mode then picks one band per particle.
    /// </summary>
    private Gradient BuildGradient()
    {
        int count = Mathf.Min(colours.Length, 4);
        var keys = new GradientColorKey[count * 2];
        for (int i = 0; i < count; i++)
        {
            keys[i * 2] = new GradientColorKey(colours[i], i / (float)count);
            keys[i * 2 + 1] = new GradientColorKey(colours[i], (i + 1) / (float)count - 0.001f);
        }

        var gradient = new Gradient();
        gradient.SetKeys(keys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return gradient;
    }

    private static Material GetMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "Confetti" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        material.color = Color.white; // particle colours come from the gradient
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        string parent = path.Substring(0, slash);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
    }
}
