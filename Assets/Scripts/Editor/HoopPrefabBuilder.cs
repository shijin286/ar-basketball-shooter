using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// STAGE 2 - Editor tool that builds Assets/Prefabs/BasketballHoop.prefab.
///
/// Menu: Tools > AR Basketball > Create Hoop Prefab
///
/// It creates the stand, backboard, rim, net, materials, physics materials and
/// colliders with exact sizes, so you don't have to place 16 rim colliders by hand.
/// Running it again overwrites the same prefab, so the reference in
/// ARPlanePlacement keeps working.
///
/// Must live in a folder called "Editor" (Assets/Scripts/Editor/). It is not
/// included in the Android build and is not attached to any GameObject.
///
/// Layout (1 unit = 1 metre): pivot on the floor at the pole, and the rim sticks
/// out towards +Z. ARPlanePlacement turns +Z towards the player.
/// </summary>
public class HoopPrefabBuilder : ScriptableWizard
{
    [Header("Size (metres)")]
    [Tooltip("Height of the rim above the floor. A real hoop is 3.05 m; 1.6 m works well indoors.")]
    public float rimHeight = 1.6f;

    [Tooltip("Inside diameter of the rim. A real one is 0.45 m (the ball is 0.24 m).")]
    public float rimInnerDiameter = 0.45f;

    [Tooltip("Thickness of the rim tube (radius). Slightly thicker than real for reliable collisions.")]
    public float rimTubeRadius = 0.012f;

    [Tooltip("Gap between the backboard and the inside edge of the rim. Real: 0.15 m.")]
    public float rimDistanceFromBoard = 0.15f;

    public Vector2 backboardSize = new Vector2(1.2f, 0.75f);
    public float backboardThickness = 0.03f;

    [Tooltip("Distance from the pole to the back of the backboard.")]
    public float poleToBoardDistance = 0.3f;

    public float netLength = 0.4f;

    [Range(8, 32)]
    [Tooltip("Number of capsule colliders that form the rim ring. 16 is smooth enough.")]
    public int rimColliderSegments = 16;

    [Header("Physics")]
    [Range(0f, 1f)] public float rimBounciness = 0.5f;
    [Range(0f, 1f)] public float backboardBounciness = 0.6f;

    [Header("Scoring (Stage 5)")]
    [Tooltip("Width of the trigger box inside the rim. Must leave room for the ball (inner diameter - 0.24 m).")]
    public float scoreZoneWidth = 0.12f;

    private const string PrefabPath = "Assets/Prefabs/BasketballHoop.prefab";
    private const string MaterialFolder = "Assets/Materials/Hoop";
    private const string PhysicsFolder = "Assets/Materials/Physics";
    private const string MeshFolder = "Assets/Models/Generated";

    private float lastRimCenterZ;

    [MenuItem("Tools/AR Basketball/Create Hoop Prefab")]
    private static void OpenWizard()
    {
        DisplayWizard<HoopPrefabBuilder>("Create Basketball Hoop", "Create / Update Prefab");
    }

    private void OnWizardUpdate()
    {
        helpString = "Builds " + PrefabPath + " with backboard, rim colliders and net. " +
                     "Safe to run again: the prefab is updated in place.";

        if (rimInnerDiameter < 0.3f)
        {
            errorString = "Rim inner diameter must be at least 0.30 m (the ball is 0.24 m wide).";
            isValid = false;
        }
        else if (rimHeight < 0.5f || backboardSize.x <= 0f || backboardSize.y <= 0f || backboardThickness <= 0f)
        {
            errorString = "Rim height must be at least 0.5 m and backboard sizes must be positive.";
            isValid = false;
        }
        else if (rimTubeRadius <= 0f || netLength <= 0f || rimDistanceFromBoard <= rimTubeRadius || poleToBoardDistance <= 0f)
        {
            errorString = "Tube radius, net length and distances must be positive (board gap > tube radius).";
            isValid = false;
        }
        else if (scoreZoneWidth < 0.04f || scoreZoneWidth > rimInnerDiameter - 0.24f)
        {
            errorString = "Score Zone Width must be between 0.04 m and (rim inner diameter - 0.24 m).";
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
        GameObject root = null;
        try
        {
            root = BuildHoop();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
            if (!success || prefab == null)
            {
                Debug.LogError("[HoopPrefabBuilder] Could not save " + PrefabPath);
                return;
            }

            AssetDatabase.SaveAssets();
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"[HoopPrefabBuilder] Saved {PrefabPath}. Rim centre is {rimHeight:0.00} m high " +
                      $"and {lastRimCenterZ:0.00} m in front of the pole.");
        }
        finally
        {
            if (root != null) DestroyImmediate(root);
        }
    }

    // ---------------------------------------------------------------- build

    private GameObject BuildHoop()
    {
        EnsureFolder("Assets/Prefabs");
        EnsureFolder(MaterialFolder);
        EnsureFolder(PhysicsFolder);
        EnsureFolder(MeshFolder);

        Material boardMat = GetMaterial("Hoop_Backboard", new Color(0.95f, 0.95f, 0.95f), 0f, 0.7f);
        Material lineMat = GetMaterial("Hoop_BoardLines", new Color(0.85f, 0.1f, 0.1f), 0f, 0.3f);
        Material rimMat = GetMaterial("Hoop_Rim", new Color(1f, 0.38f, 0.05f), 0.7f, 0.6f);
        Material netMat = GetMaterial("Hoop_Net", Color.white, 0f, 0.2f);
        Material poleMat = GetMaterial("Hoop_Pole", new Color(0.2f, 0.22f, 0.25f), 0.5f, 0.5f);

        PhysicsMaterial rimPhysics = GetPhysicsMaterial("Rim_Physics", rimBounciness, 0.4f);
        PhysicsMaterial boardPhysics = GetPhysicsMaterial("Backboard_Physics", backboardBounciness, 0.3f);

        float innerRadius = rimInnerDiameter * 0.5f;
        float rimRadius = innerRadius + rimTubeRadius;              // centre line of the rim tube
        float boardBackZ = poleToBoardDistance;
        float boardFrontZ = boardBackZ + backboardThickness;
        float boardCenterZ = boardBackZ + backboardThickness * 0.5f;
        float boardCenterY = rimHeight - 0.15f + backboardSize.y * 0.5f; // board bottom 15 cm below rim
        float rimCenterZ = boardFrontZ + rimDistanceFromBoard + innerRadius;
        lastRimCenterZ = rimCenterZ;

        GameObject root = new GameObject("BasketballHoop");

        // The hoop moves with its AR anchor, so a kinematic Rigidbody is the
        // correct way to move colliders without physics pushing them around.
        Rigidbody body = root.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        // --- Stand
        Transform stand = CreateEmpty("Stand", root.transform, Vector3.zero);
        CreatePrimitive(PrimitiveType.Cube, "Base", stand, new Vector3(0f, 0.025f, 0.1f),
            new Vector3(0.6f, 0.05f, 0.7f), poleMat, true, null);
        CreatePrimitive(PrimitiveType.Cylinder, "Pole", stand, new Vector3(0f, boardCenterY * 0.5f, 0f),
            new Vector3(0.08f, boardCenterY * 0.5f, 0.08f), poleMat, true, null);
        CreatePrimitive(PrimitiveType.Cube, "Arm", stand, new Vector3(0f, boardCenterY, boardBackZ * 0.5f),
            new Vector3(0.08f, 0.08f, boardBackZ), poleMat, true, null);

        // --- Backboard (one box collider) + red lines (visual only)
        Transform board = CreateEmpty("Backboard", root.transform, new Vector3(0f, boardCenterY, boardCenterZ));
        CreatePrimitive(PrimitiveType.Cube, "BoardPanel", board, Vector3.zero,
            new Vector3(backboardSize.x, backboardSize.y, backboardThickness), boardMat, true, boardPhysics);

        float lineZ = backboardThickness * 0.5f + 0.002f;
        const float lineWidth = 0.03f;
        AddFrame(board, "Border", new Vector2(backboardSize.x - lineWidth, backboardSize.y - lineWidth),
            Vector2.zero, lineZ, lineWidth, lineMat);

        float squareW = backboardSize.x * 0.33f;
        float squareH = squareW * 0.75f;
        float rimLocalY = rimHeight - boardCenterY;
        AddFrame(board, "ShooterSquare", new Vector2(squareW, squareH),
            new Vector2(0f, rimLocalY + squareH * 0.5f), lineZ, lineWidth * 0.8f, lineMat);

        // --- Rim: the "Rim" transform sits exactly at the centre of the hoop opening.
        Transform rim = CreateEmpty("Rim", root.transform, new Vector3(0f, rimHeight, rimCenterZ));

        Mesh torus = LoadOrCreateMesh("RimTorus");
        BuildTorus(torus, rimRadius, rimTubeRadius, 48, 10);
        CreateMeshObject("RimVisual", rim, torus, rimMat);

        Transform rimColliders = CreateEmpty("RimColliders", rim, Vector3.zero);
        int n = Mathf.Max(8, rimColliderSegments);
        float halfStep = Mathf.PI / n;
        float chord = 2f * rimRadius * Mathf.Sin(halfStep);
        float midRadius = rimRadius * Mathf.Cos(halfStep);
        for (int i = 0; i < n; i++)
        {
            float angle = (i + 0.5f) * 2f * Mathf.PI / n;
            Vector3 outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 tangent = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle));

            Transform segment = CreateEmpty($"RimSegment_{i:00}", rimColliders, outward * midRadius);
            segment.localRotation = Quaternion.FromToRotation(Vector3.right, tangent);

            CapsuleCollider capsule = segment.gameObject.AddComponent<CapsuleCollider>();
            capsule.direction = 0; // X axis = along the rim
            capsule.radius = rimTubeRadius;
            capsule.height = chord + rimTubeRadius * 2f; // overlap neighbours so there are no gaps
            capsule.sharedMaterial = rimPhysics;
        }

        float bracketLength = rimDistanceFromBoard - rimTubeRadius;
        CreatePrimitive(PrimitiveType.Cube, "Bracket", rim,
            new Vector3(0f, -0.01f, -(rimRadius + bracketLength * 0.5f)),
            new Vector3(0.12f, 0.025f, bracketLength), rimMat, true, rimPhysics);

        // --- Net: visual only, no collider, so the ball drops straight through.
        Mesh netMesh = LoadOrCreateMesh("HoopNet");
        BuildNet(netMesh, innerRadius, innerRadius * 0.6f, netLength, 12, 5, 0.004f);
        CreateMeshObject("Net", rim, netMesh, netMat);

        // --- Score zone (Stage 5): a narrow trigger box from 0.1 m above the rim down to
        // 0.3 m below it. It sits at the rim centre, so HoopScoreTrigger measures heights from the rim.
        Transform zone = CreateEmpty("ScoreZone", rim, Vector3.zero);
        BoxCollider zoneBox = zone.gameObject.AddComponent<BoxCollider>();
        zoneBox.isTrigger = true;
        zoneBox.center = new Vector3(0f, -0.1f, 0f);
        zoneBox.size = new Vector3(scoreZoneWidth, 0.4f, scoreZoneWidth);
        zone.gameObject.AddComponent<HoopScoreTrigger>();

        return root;
    }

    // ---------------------------------------------------------------- object helpers

    private static Transform CreateEmpty(string name, Transform parent, Vector3 localPosition)
    {
        Transform t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = localPosition;
        return t;
    }

    private static GameObject CreatePrimitive(PrimitiveType type, string name, Transform parent,
        Vector3 localPosition, Vector3 localScale, Material material, bool keepCollider, PhysicsMaterial physics)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = localScale;
        go.GetComponent<Renderer>().sharedMaterial = material;

        Collider col = go.GetComponent<Collider>();
        if (!keepCollider) DestroyImmediate(col);
        else if (physics != null) col.sharedMaterial = physics;
        return go;
    }

    private static void CreateMeshObject(string name, Transform parent, Mesh mesh, Material material)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
    }

    /// <summary>Four thin cubes forming a rectangle outline on the front of the board (no colliders).</summary>
    private static void AddFrame(Transform parent, string name, Vector2 size, Vector2 centre, float z,
        float width, Material material)
    {
        Transform frame = CreateEmpty(name, parent, new Vector3(centre.x, centre.y, z));
        const float depth = 0.004f;
        CreatePrimitive(PrimitiveType.Cube, "Top", frame, new Vector3(0f, size.y * 0.5f, 0f),
            new Vector3(size.x + width, width, depth), material, false, null);
        CreatePrimitive(PrimitiveType.Cube, "Bottom", frame, new Vector3(0f, -size.y * 0.5f, 0f),
            new Vector3(size.x + width, width, depth), material, false, null);
        CreatePrimitive(PrimitiveType.Cube, "Left", frame, new Vector3(-size.x * 0.5f, 0f, 0f),
            new Vector3(width, size.y, depth), material, false, null);
        CreatePrimitive(PrimitiveType.Cube, "Right", frame, new Vector3(size.x * 0.5f, 0f, 0f),
            new Vector3(width, size.y, depth), material, false, null);
    }

    // ---------------------------------------------------------------- meshes

    private static void BuildTorus(Mesh mesh, float radius, float tubeRadius, int ringSegments, int tubeSegments)
    {
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var triangles = new List<int>();

        for (int i = 0; i <= ringSegments; i++)
        {
            float a = i * 2f * Mathf.PI / ringSegments;
            Vector3 outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Vector3 centre = outward * radius;
            for (int j = 0; j <= tubeSegments; j++)
            {
                float b = j * 2f * Mathf.PI / tubeSegments;
                Vector3 normal = outward * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                vertices.Add(centre + normal * tubeRadius);
                normals.Add(normal);
            }
        }

        int stride = tubeSegments + 1;
        for (int i = 0; i < ringSegments; i++)
        {
            for (int j = 0; j < tubeSegments; j++)
            {
                int a0 = i * stride + j;
                int a1 = a0 + stride;
                triangles.Add(a0); triangles.Add(a0 + 1); triangles.Add(a1);
                triangles.Add(a1); triangles.Add(a0 + 1); triangles.Add(a1 + 1);
            }
        }

        ApplyMesh(mesh, vertices, normals, triangles);
    }

    /// <summary>Diamond-pattern net made of thin tubes, hanging down from the rim.</summary>
    private static void BuildNet(Mesh mesh, float topRadius, float bottomRadius, float length,
        int columns, int rows, float strandRadius)
    {
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var triangles = new List<int>();

        var points = new Vector3[rows + 1, columns];
        for (int row = 0; row <= rows; row++)
        {
            float t = row / (float)rows;
            float radius = Mathf.Lerp(topRadius, bottomRadius, t);
            float offset = (row % 2) * 0.5f;
            for (int c = 0; c < columns; c++)
            {
                float angle = (c + offset) / columns * 2f * Mathf.PI;
                points[row, c] = new Vector3(Mathf.Cos(angle) * radius, -length * t, Mathf.Sin(angle) * radius);
            }
        }

        for (int row = 0; row < rows; row++)
        {
            for (int c = 0; c < columns; c++)
            {
                int partner = row % 2 == 0 ? (c - 1 + columns) % columns : (c + 1) % columns;
                AddTube(vertices, normals, triangles, points[row, c], points[row + 1, c], strandRadius);
                AddTube(vertices, normals, triangles, points[row, c], points[row + 1, partner], strandRadius);
            }
        }

        for (int c = 0; c < columns; c++)
        {
            AddTube(vertices, normals, triangles, points[rows, c], points[rows, (c + 1) % columns], strandRadius);
        }

        ApplyMesh(mesh, vertices, normals, triangles);
    }

    private static void AddTube(List<Vector3> vertices, List<Vector3> normals, List<int> triangles,
        Vector3 from, Vector3 to, float radius)
    {
        const int sides = 4;
        Vector3 along = (to - from).normalized;
        Vector3 u = Vector3.Cross(along, Vector3.up);
        if (u.sqrMagnitude < 0.0001f) u = Vector3.Cross(along, Vector3.right);
        u.Normalize();
        Vector3 w = Vector3.Cross(along, u); // this order keeps the triangles facing outwards

        int start = vertices.Count;
        for (int k = 0; k < 2; k++)
        {
            Vector3 centre = k == 0 ? from : to;
            for (int j = 0; j <= sides; j++)
            {
                float b = j * 2f * Mathf.PI / sides;
                Vector3 normal = u * Mathf.Cos(b) + w * Mathf.Sin(b);
                vertices.Add(centre + normal * radius);
                normals.Add(normal);
            }
        }

        int stride = sides + 1;
        for (int j = 0; j < sides; j++)
        {
            int a0 = start + j;
            int a1 = a0 + stride;
            triangles.Add(a0); triangles.Add(a0 + 1); triangles.Add(a1);
            triangles.Add(a1); triangles.Add(a0 + 1); triangles.Add(a1 + 1);
        }
    }

    private static void ApplyMesh(Mesh mesh, List<Vector3> vertices, List<Vector3> normals, List<int> triangles)
    {
        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
    }

    // ---------------------------------------------------------------- assets

    private static Mesh LoadOrCreateMesh(string name)
    {
        string path = $"{MeshFolder}/{name}.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null)
        {
            mesh = new Mesh { name = name };
            AssetDatabase.CreateAsset(mesh, path);
        }
        return mesh;
    }

    private static Material GetMaterial(string name, Color color, float metallic, float smoothness)
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

        material.color = color;
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
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
