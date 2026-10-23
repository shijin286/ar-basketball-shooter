using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// STAGE 10 - the laptop version (Windows .exe) and browser version (WebGL) of the game.
///
/// A laptop has no ARCore, so on a desktop build this script:
///  1. switches the AR parts off (AR Session, camera feed, phone tracking, plane detection);
///  2. builds a virtual court: a wooden floor with lines and four walls;
///  3. tells ARPlanePlacement to place the hoop on that floor instead of on an AR plane;
///  4. adds DesktopCameraRig: W A S D to walk, right mouse button to look around, and turns
///     the camera to the hoop once it is placed (on the phone the player raises the phone);
///  5. keeps the phone-shaped UI in a centred column and changes the instructions to mouse ones.
/// Everything else (shooting, scoring, sounds, timer, leaderboard) is the same code as on the phone.
///
/// On the phone, and in the Editor (which uses XR Simulation), it does nothing.
///
/// Attach to: GameSystems. Tools > AR Basketball > Build Windows App adds and wires it.
/// </summary>
[DefaultExecutionOrder(-100)] // runs before ARPlanePlacement, BallSpawner and the UI
[DisallowMultipleComponent]
public class DesktopMode : MonoBehaviour
{
    public const float PortraitAspect = 9f / 16f;

    private const string PlaceText =
        "1. Look at the floor: hold the <b>right</b> mouse button and drag\n" +
        "2. Walk with <b>W A S D</b> or the arrow keys\n" +
        "3. <b>Click</b> the floor to place the hoop";
    private const string AimText =
        "<b>Drag the mouse up</b> and let go to shoot - faster throws harder\n" +
        "Drag a little left or right to aim\nStand about 2-3 m from the hoop";
    private const string HowToText = "Click to place  •  Drag up to shoot  •  WASD to walk";
    private const string SubtitleText = "Laptop version - virtual court";

    [Header("References (found automatically if empty)")]
    [SerializeField] private ARPlanePlacement placement;
    [SerializeField] private Camera playerCamera;

    [Tooltip("Materials/Desktop/DesktopSurface (URP Lit). Copied and tinted for each surface.")]
    [SerializeField] private Material surfaceMaterial;

    [Tooltip("Materials/Physics/Floor_Physics, so the ball bounces on the court like on a real floor.")]
    [SerializeField] private PhysicsMaterial floorPhysics;

    [Header("Instruction texts to change (optional)")]
    [SerializeField] private TMP_Text placeHoopText;
    [SerializeField] private TMP_Text aimShootText;
    [SerializeField] private TMP_Text howToText;
    [SerializeField] private TMP_Text subtitleText;

    [Header("Court")]
    [SerializeField, Min(6f)] private float courtSize = 14f;
    [SerializeField, Min(2f)] private float wallHeight = 5f;

    [Tooltip("Use the laptop version in the Editor too (normally the Editor uses XR Simulation).")]
    [SerializeField] private bool forceInEditor;

    /// <summary>True while the laptop version is running.</summary>
    public static bool IsActive { get; private set; }

    public Collider Floor { get; private set; }

    private DesktopCameraRig rig;
    private bool activatedHere;

    /// <summary>Laptop builds use the virtual court; phones use ARCore; the Editor uses XR Simulation unless forced.</summary>
    public static bool ShouldUse(bool isEditor, bool isMobilePlatform, bool forceInEditor)
    {
        if (isMobilePlatform) return false;
        return !isEditor || forceInEditor;
    }

    /// <summary>Same, but the browser (WebGL) build always uses the court: browsers have no ARCore, not even on a phone.</summary>
    public static bool ShouldUse(bool isEditor, bool isMobilePlatform, bool forceInEditor, bool isWebGL)
    {
        return (isWebGL && !isEditor) || ShouldUse(isEditor, isMobilePlatform, forceInEditor);
    }

    private void Awake()
    {
        bool isWebGL = Application.platform == RuntimePlatform.WebGLPlayer;
        if (!ShouldUse(Application.isEditor, Application.isMobilePlatform, forceInEditor, isWebGL))
        {
            enabled = false;
            return;
        }

        if (placement == null) placement = FindFirstObjectByType<ARPlanePlacement>();
        if (playerCamera == null) playerCamera = Camera.main;
        if (placement == null || playerCamera == null)
        {
            Debug.LogError("[DesktopMode] ARPlanePlacement or the Main Camera not found - the laptop version can't start.", this);
            enabled = false;
            return;
        }

        IsActive = true;
        activatedHere = true;

        SwitchOffAR();
        Floor = BuildCourt(courtSize, wallHeight, surfaceMaterial, floorPhysics);
        placement.UseDesktopFloor(Floor);
        SetUpCamera();
        SetUpUi();
        QualitySettings.vSyncCount = 1; // match the screen's refresh rate instead of running the GPU flat out
        Debug.Log("[DesktopMode] Laptop version: virtual court, mouse and keyboard controls.");
    }

    private void OnEnable()
    {
        if (activatedHere) placement.HoopPlaced += OnHoopPlaced;
    }

    private void OnDisable()
    {
        if (activatedHere && placement != null) placement.HoopPlaced -= OnHoopPlaced;
    }

    private void OnDestroy()
    {
        if (!activatedHere) return;
        IsActive = false;
        SafeArea.MaxAspect = 0f;
    }

    /// <summary>On the phone the player raises the phone to see the hoop; here the camera turns to it.</summary>
    private void OnHoopPlaced(Transform hoop)
    {
        if (rig == null || hoop == null) return;
        Transform rim = hoop.Find("Rim");
        Vector3 target = rim != null ? rim.position + Vector3.down * 0.5f : hoop.position + Vector3.up * 1.1f;
        rig.LookAt(target, 0.6f); // rim in the upper half of the screen, hoop base still visible
    }

    // ---------------------------------------------------------------- AR off

    private void SwitchOffAR()
    {
        foreach (ARSession session in FindObjectsByType<ARSession>(FindObjectsSortMode.None)) session.enabled = false;
        foreach (ARPlaneManager manager in FindObjectsByType<ARPlaneManager>(FindObjectsSortMode.None)) manager.enabled = false;
        foreach (ARRaycastManager manager in FindObjectsByType<ARRaycastManager>(FindObjectsSortMode.None)) manager.enabled = false;
        foreach (ARAnchorManager manager in FindObjectsByType<ARAnchorManager>(FindObjectsSortMode.None)) manager.enabled = false;

        // No camera feed and no phone tracking: the camera shows the 3D court and DesktopCameraRig moves it.
        SetEnabled(playerCamera.GetComponent<ARCameraBackground>(), false);
        SetEnabled(playerCamera.GetComponent<ARCameraManager>(), false);
        SetEnabled(playerCamera.GetComponent<TrackedPoseDriver>(), false);
    }

    private static void SetEnabled(Behaviour behaviour, bool value)
    {
        if (behaviour != null) behaviour.enabled = value;
    }

    // ---------------------------------------------------------------- camera

    private void SetUpCamera()
    {
        playerCamera.clearFlags = CameraClearFlags.SolidColor;
        playerCamera.backgroundColor = new Color(0.55f, 0.72f, 0.9f);
        playerCamera.fieldOfView = 60f;
        playerCamera.nearClipPlane = 0.05f;
        playerCamera.farClipPlane = 100f;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.55f, 0.56f, 0.6f);

        // Start a few metres from the centre, looking at the floor where the hoop will go.
        rig = playerCamera.GetComponent<DesktopCameraRig>();
        if (rig == null) rig = playerCamera.gameObject.AddComponent<DesktopCameraRig>();
        rig.Place(new Vector3(0f, 0f, -courtSize * 0.25f), 0f, 28f, courtSize * 0.5f - 0.5f);
    }

    // ---------------------------------------------------------------- UI

    private void SetUpUi()
    {
        // The UI was designed for a 1080 x 1920 phone: scale it by height and keep it in a portrait column.
        SafeArea.MaxAspect = PortraitAspect;
        foreach (CanvasScaler scaler in FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize) scaler.matchWidthOrHeight = 1f;
        }

        SetText(placeHoopText, PlaceText);
        SetText(aimShootText, AimText);
        SetText(howToText, HowToText);
        SetText(subtitleText, SubtitleText);

        BasketballShooter shooter = FindFirstObjectByType<BasketballShooter>();
        if (shooter != null) shooter.MouseWording = true; // "Drag up to shoot"
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null) text.text = value;
    }

    // ---------------------------------------------------------------- court (public for the Stage 10 tests)

    /// <summary>
    /// Builds the virtual court under a new "DesktopCourt" object: a floor whose top is at y = 0,
    /// court lines (no colliders) and four walls. Returns the floor collider.
    /// </summary>
    public static Collider BuildCourt(float size, float height, Material template, PhysicsMaterial floorPhysics)
    {
        Transform root = new GameObject("DesktopCourt").transform;
        float half = size * 0.5f;

        Material wood = MakeMaterial(template, Color.white, CreateWoodTexture(), size * 0.5f, 0.45f);
        Material wall = MakeMaterial(template, new Color(0.86f, 0.89f, 0.93f), null, 1f, 0.1f);
        Material pad = MakeMaterial(template, new Color(0.12f, 0.2f, 0.38f), null, 1f, 0.2f);
        Material line = MakeMaterial(template, new Color(0.96f, 0.96f, 0.96f), null, 1f, 0.3f);

        GameObject floor = Box(root, "CourtFloor", new Vector3(0f, -0.1f, 0f), new Vector3(size, 0.2f, size), wood, true);
        floor.GetComponent<Collider>().sharedMaterial = floorPhysics;

        // Court lines: a boundary 1 m inside the walls and a centre line.
        float inner = half - 1f;
        const float lineWidth = 0.06f, lineY = 0.003f, lineThickness = 0.002f;
        Box(root, "LineNorth", new Vector3(0f, lineY, inner), new Vector3(inner * 2f, lineThickness, lineWidth), line, false);
        Box(root, "LineSouth", new Vector3(0f, lineY, -inner), new Vector3(inner * 2f, lineThickness, lineWidth), line, false);
        Box(root, "LineEast", new Vector3(inner, lineY, 0f), new Vector3(lineWidth, lineThickness, inner * 2f), line, false);
        Box(root, "LineWest", new Vector3(-inner, lineY, 0f), new Vector3(lineWidth, lineThickness, inner * 2f), line, false);
        Box(root, "LineCentre", new Vector3(0f, lineY, 0f), new Vector3(inner * 2f, lineThickness, lineWidth), line, false);

        // Walls keep the balls on the court; a dark padded band at the bottom.
        const float wallThickness = 0.2f, padHeight = 1.2f, padThickness = 0.05f;
        for (int side = 0; side < 4; side++)
        {
            Quaternion turn = Quaternion.Euler(0f, side * 90f, 0f);
            Vector3 outward = turn * Vector3.forward;
            GameObject w = Box(root, "Wall" + side, outward * (half + wallThickness * 0.5f) + Vector3.up * (height * 0.5f),
                new Vector3(size + wallThickness * 2f, height, wallThickness), wall, true);
            w.transform.rotation = turn;
            w.GetComponent<Collider>().sharedMaterial = floorPhysics;

            GameObject p = Box(root, "WallPad" + side, outward * (half - padThickness * 0.5f) + Vector3.up * (padHeight * 0.5f),
                new Vector3(size, padHeight, padThickness), pad, false);
            p.transform.rotation = turn;
        }

        return floor.GetComponent<Collider>();
    }

    /// <summary>A tiling wooden-plank texture (8 planks), made in code so no image file is needed.</summary>
    public static Texture2D CreateWoodTexture(int size = 256)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
        {
            name = "CourtWood",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Trilinear,
            anisoLevel = 4,
        };

        const int planks = 8;
        int plankHeight = Mathf.Max(1, size / planks);
        var random = new System.Random(7); // same planks every time
        var pixels = new Color32[size * size];

        for (int p = 0; p < planks; p++)
        {
            float shade = 0.88f + (float)random.NextDouble() * 0.2f;
            float grainOffset = (float)random.NextDouble() * 100f;
            int joint = random.Next(size);
            for (int y = p * plankHeight; y < Mathf.Min(size, (p + 1) * plankHeight); y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float grain = Mathf.PerlinNoise(x * 0.02f + grainOffset, y * 0.35f);
                    float v = shade * (0.85f + 0.25f * grain);
                    if (y == p * plankHeight || x == joint) v *= 0.6f; // gaps between planks
                    pixels[y * size + x] = new Color(0.8f * v, 0.55f * v, 0.32f * v, 1f);
                }
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(true);
        return texture;
    }

    private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Material material, bool collider)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = position;
        box.transform.localScale = size;
        box.GetComponent<Renderer>().sharedMaterial = material;
        if (!collider) DestroyCollider(box.GetComponent<Collider>());
        return box;
    }

    private static void DestroyCollider(Collider c)
    {
        if (Application.isPlaying) Destroy(c);
        else DestroyImmediate(c);
    }

    private static Material MakeMaterial(Material template, Color colour, Texture texture, float tiling, float smoothness)
    {
        Material material;
        if (template != null)
        {
            material = new Material(template);
        }
        else
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            material = new Material(shader);
        }

        material.color = colour;
        material.mainTexture = texture;
        material.mainTextureScale = Vector2.one * tiling;
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        return material;
    }
}
