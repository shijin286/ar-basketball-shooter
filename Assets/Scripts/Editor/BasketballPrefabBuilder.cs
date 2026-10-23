using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// STAGE 3 - Editor tool that builds Assets/Prefabs/Basketball.prefab.
///
/// Menu: Tools > AR Basketball > Create Basketball Prefab
///
/// Creates:
///  - a procedural basketball texture (orange with black seams) and a URP material,
///  - Ball_Physics and Floor_Physics Physic Materials,
///  - the prefab: Rigidbody + Sphere Collider + BasketballController, with a Visual child,
///  - and assigns Floor_Physics to the ARPlane prefab's Mesh Collider so the
///    real floor bounces like a court.
///
/// Running it again updates the same assets, so Inspector references keep working.
/// Lives in an "Editor" folder, so it is not included in the Android build.
/// </summary>
public class BasketballPrefabBuilder : ScriptableWizard
{
    [Header("Ball (a real size-7 ball is 0.24 m and about 0.6 kg)")]
    public float diameter = 0.24f;
    public float mass = 0.6f;

    [Tooltip("Air resistance. Small, so shots keep their speed.")]
    public float linearDamping = 0.05f;

    [Tooltip("Slows down spinning over time.")]
    public float angularDamping = 0.3f;

    [Header("Bounciness (0 = dead, 1 = super ball)")]
    [Range(0f, 1f)] public float ballBounciness = 0.8f;
    [Range(0f, 1f)] public float floorBounciness = 0.7f;

    [Header("Look")]
    public Color ballColor = new Color(0.85f, 0.36f, 0.1f);
    public Color seamColor = new Color(0.08f, 0.05f, 0.04f);
    public int textureWidth = 1024;

    private const string PrefabPath = "Assets/Prefabs/Basketball.prefab";
    private const string PlanePrefabPath = "Assets/Prefabs/ARPlane.prefab";
    private const string MaterialFolder = "Assets/Materials/Ball";
    private const string PhysicsFolder = "Assets/Materials/Physics";
    private const string TexturePath = MaterialFolder + "/Basketball_Albedo.png";

    [MenuItem("Tools/AR Basketball/Create Basketball Prefab")]
    private static void OpenWizard()
    {
        DisplayWizard<BasketballPrefabBuilder>("Create Basketball", "Create / Update Prefab");
    }

    private void OnWizardUpdate()
    {
        helpString = "Builds " + PrefabPath + ", its texture and physics materials, " +
                     "and gives the ARPlane prefab the Floor_Physics material.";

        if (diameter < 0.1f || diameter > 0.4f)
        {
            errorString = "Diameter should be between 0.10 and 0.40 m (the rim is 0.45 m wide).";
            isValid = false;
        }
        else if (mass <= 0f || linearDamping < 0f || angularDamping < 0f)
        {
            errorString = "Mass must be positive and damping can't be negative.";
            isValid = false;
        }
        else if (textureWidth < 256 || textureWidth > 4096)
        {
            errorString = "Texture width should be between 256 and 4096.";
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
        EnsureFolder(PhysicsFolder);

        Texture2D texture = CreateBallTexture(TexturePath, textureWidth, ballColor, seamColor);
        Material ballMaterial = GetMaterial("Basketball", texture, 0.3f);
        PhysicsMaterial ballPhysics = GetPhysicsMaterial("Ball_Physics", ballBounciness, 0.6f);
        PhysicsMaterial floorPhysics = GetPhysicsMaterial("Floor_Physics", floorBounciness, 0.6f);

        GameObject root = null;
        try
        {
            root = BuildBall(ballMaterial, ballPhysics);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
            if (!success || prefab == null)
            {
                Debug.LogError("[BasketballPrefabBuilder] Could not save " + PrefabPath);
                return;
            }

            ApplyFloorPhysicsToPlanePrefab(floorPhysics);
            AssetDatabase.SaveAssets();

            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"[BasketballPrefabBuilder] Saved {PrefabPath} ({diameter:0.00} m, {mass:0.00} kg).");
        }
        finally
        {
            if (root != null) DestroyImmediate(root);
        }
    }

    // ---------------------------------------------------------------- build

    private GameObject BuildBall(Material material, PhysicsMaterial physics)
    {
        GameObject root = new GameObject("Basketball");

        // The visual is a scaled child so the root keeps scale 1 (better for physics and effects later).
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visual.name = "Visual";
        visual.transform.SetParent(root.transform, false);
        visual.transform.localScale = Vector3.one * diameter;
        DestroyImmediate(visual.GetComponent<Collider>());
        visual.GetComponent<MeshRenderer>().sharedMaterial = material;

        SphereCollider sphere = root.AddComponent<SphereCollider>();
        sphere.radius = diameter * 0.5f;
        sphere.sharedMaterial = physics;

        Rigidbody body = root.AddComponent<Rigidbody>();
        body.mass = mass;
        body.linearDamping = linearDamping;
        body.angularDamping = angularDamping;
        body.useGravity = true;
        body.isKinematic = false; // BasketballController makes it kinematic while it is held
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // fast ball vs thin rim

        root.AddComponent<BasketballController>();
        return root;
    }

    private static void ApplyFloorPhysicsToPlanePrefab(PhysicsMaterial floorPhysics)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PlanePrefabPath) == null)
        {
            Debug.LogWarning($"[BasketballPrefabBuilder] {PlanePrefabPath} not found - assign Floor_Physics " +
                             "to your plane prefab's Mesh Collider by hand.");
            return;
        }

        GameObject contents = PrefabUtility.LoadPrefabContents(PlanePrefabPath);
        try
        {
            MeshCollider[] colliders = contents.GetComponentsInChildren<MeshCollider>(true);
            if (colliders.Length == 0)
            {
                Debug.LogWarning($"[BasketballPrefabBuilder] {PlanePrefabPath} has no Mesh Collider.");
                return;
            }

            foreach (MeshCollider meshCollider in colliders) meshCollider.sharedMaterial = floorPhysics;
            PrefabUtility.SaveAsPrefabAsset(contents, PlanePrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    // ---------------------------------------------------------------- texture

    /// <summary>
    /// Draws the ball pattern in the sphere's UV layout (longitude/latitude).
    /// For every pixel we work out the 3D point on the ball and measure how
    /// close it is to a seam, so the lines have the same width everywhere.
    /// </summary>
    private static Texture2D CreateBallTexture(string path, int width, Color ball, Color seam)
    {
        int height = width / 2;
        const float seamHalfWidth = 0.02f; // on a unit sphere
        var pixels = new Color32[width * height];

        for (int y = 0; y < height; y++)
        {
            float latitude = ((y + 0.5f) / height - 0.5f) * Mathf.PI;
            float cosLat = Mathf.Cos(latitude);
            float sinLat = Mathf.Sin(latitude);

            for (int x = 0; x < width; x++)
            {
                float longitude = (x + 0.5f) / width * 2f * Mathf.PI;
                Vector3 p = new Vector3(cosLat * Mathf.Cos(longitude), sinLat, cosLat * Mathf.Sin(longitude));

                float distance = SeamDistance(p);
                float seamAmount = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(seamHalfWidth * 0.6f, seamHalfWidth, distance));
                float pebble = 1f + (Hash(x, y) - 0.5f) * 0.08f; // tiny brightness noise = leather grain

                Color c = Color.Lerp(ball * pebble, seam, seamAmount);
                c.a = 1f;
                pixels[y * width + x] = c;
            }
        }

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.SetPixels32(pixels);
        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        DestroyImmediate(texture);

        AssetDatabase.ImportAsset(path);
        if (AssetImporter.GetAtPath(path) is TextureImporter importer)
        {
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.wrapModeU = TextureWrapMode.Repeat;
            importer.wrapModeV = TextureWrapMode.Clamp;
            importer.maxTextureSize = Mathf.NextPowerOfTwo(width);
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    /// <summary>Distance from a point on the unit sphere to the nearest seam line.</summary>
    private static float SeamDistance(Vector3 p)
    {
        float equator = Mathf.Abs(p.y);  // horizontal great circle
        float meridian = Mathf.Abs(p.x); // vertical great circle
        // Two curved seams, one each side of the vertical line, bending in towards the poles: ( | )
        float curveX = 0.7f - 0.25f * p.y * p.y;
        float curved = Mathf.Abs(Mathf.Abs(p.x) - curveX);
        return Mathf.Min(equator, Mathf.Min(meridian, curved));
    }

    private static float Hash(int x, int y)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h & 0xFFFF) / 65535f;
        }
    }

    // ---------------------------------------------------------------- assets

    private static Material GetMaterial(string name, Texture2D texture, float smoothness)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        material.color = Color.white; // the texture already has the colours
        material.mainTexture = texture;
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness); // URP
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness); // Built-in
        EditorUtility.SetDirty(material);
        return material;
    }

    private static PhysicsMaterial GetPhysicsMaterial(string name, float bounciness, float friction)
    {
        string path = $"{PhysicsFolder}/{name}.asset";
        PhysicsMaterial material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
        if (material == null)
        {
            material = new PhysicsMaterial(name);
            AssetDatabase.CreateAsset(material, path);
        }

        material.bounciness = bounciness;
        material.dynamicFriction = friction;
        material.staticFriction = friction;
        material.bounceCombine = PhysicsMaterialCombine.Average;
        material.frictionCombine = PhysicsMaterialCombine.Average;
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
