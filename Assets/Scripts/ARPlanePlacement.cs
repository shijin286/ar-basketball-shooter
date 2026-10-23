using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// STAGE 1 - AR plane detection and hoop placement.
///
/// What it does:
///  1. Every frame, casts a ray from the centre of the screen onto detected
///     horizontal (floor/table) planes and shows a placement indicator there.
///  2. When the user taps the screen (not on a button), places the hoop on the
///     plane at the tap position (or at the indicator if the tap missed).
///  3. Anchors the hoop with an ARAnchor so it stays fixed in the real world.
///  4. RepositionHoop() (called by a UI button) picks the hoop up again so the
///     user can place it somewhere else.
///
/// Attach to: an empty GameObject called "GameSystems" in ARBasketballScene.
///
/// Other scripts (GameManager, added in a later stage) listen to the
/// HoopPlaced / HoopRemoved events and read HoopTransform.
/// </summary>
[DisallowMultipleComponent]
public class ARPlanePlacement : MonoBehaviour
{
    [Header("AR references (found automatically if left empty)")]
    [Tooltip("AR Raycast Manager on the XR Origin.")]
    [SerializeField] private ARRaycastManager raycastManager;

    [Tooltip("AR Plane Manager on the XR Origin.")]
    [SerializeField] private ARPlaneManager planeManager;

    [Tooltip("The Main Camera inside XR Origin > Camera Offset.")]
    [SerializeField] private Camera arCamera;

    [Header("Prefabs")]
    [Tooltip("Prefabs/BasketballHoop. Its front (where the rim sticks out) must face +Z (blue arrow).")]
    [SerializeField] private GameObject hoopPrefab;

    [Tooltip("Prefabs/PlacementIndicator. A flat ring/disc with NO collider.")]
    [SerializeField] private GameObject placementIndicatorPrefab;

    [Header("UI (optional in Stage 1)")]
    [Tooltip("A TextMeshPro text that shows instructions such as 'Tap to place the hoop'.")]
    [SerializeField] private TMP_Text statusText;

    [Tooltip("The Reposition Hoop button. Hidden until the hoop is placed.")]
    [SerializeField] private GameObject repositionButton;

    [Header("Settings")]
    [Tooltip("Hide the plane visuals after placing the hoop (their colliders stay active).")]
    [SerializeField] private bool hidePlanesAfterPlacement = true;

    [Tooltip("Extra rotation if your hoop model faces the wrong way (e.g. 180).")]
    [SerializeField] private float hoopYawOffset = 0f;

    [Tooltip("Allow placement as soon as the scene starts. The GameManager will control this later.")]
    [SerializeField] private bool placementEnabledOnStart = true;

    /// <summary>Raised after the hoop is placed. Passes the hoop's Transform.</summary>
    public event Action<Transform> HoopPlaced;

    /// <summary>Raised when the hoop is picked up to be repositioned.</summary>
    public event Action HoopRemoved;

    public bool IsHoopPlaced { get; private set; }
    public Transform HoopTransform => hoopInstance != null ? hoopInstance.transform : null;

    private static readonly List<ARRaycastHit> s_Hits = new List<ARRaycastHit>();
    private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
    private readonly List<Renderer> planeRenderers = new List<Renderer>();

    private GameObject hoopInstance;
    private GameObject indicatorInstance;
    private GameObject anchorObject;
    private Collider desktopFloor;
    private bool placementEnabled;
    private bool planesHidden;
    private bool indicatorHasPose;
    private Pose indicatorPose;
    private string lastStatus;

    private void Awake()
    {
        if (raycastManager == null) raycastManager = FindFirstObjectByType<ARRaycastManager>();
        if (planeManager == null) planeManager = FindFirstObjectByType<ARPlaneManager>();
        if (arCamera == null) arCamera = Camera.main;

        if (raycastManager == null || planeManager == null || arCamera == null)
        {
            Debug.LogError("[ARPlanePlacement] Missing AR Raycast Manager, AR Plane Manager or AR Camera. " +
                           "Add the managers to the XR Origin and tag the AR camera 'MainCamera'.", this);
            enabled = false;
            return;
        }

        if (hoopPrefab == null)
        {
            Debug.LogError("[ARPlanePlacement] Hoop Prefab is not assigned in the Inspector.", this);
            enabled = false;
            return;
        }

        if (FindFirstObjectByType<ARAnchorManager>() == null)
        {
            Debug.LogWarning("[ARPlanePlacement] No AR Anchor Manager found on the XR Origin. " +
                             "The hoop may drift when you move around.", this);
        }

        if (placementIndicatorPrefab != null)
        {
            indicatorInstance = Instantiate(placementIndicatorPrefab);
            indicatorInstance.name = "PlacementIndicator";
            indicatorInstance.SetActive(false);
        }
        else
        {
            Debug.LogWarning("[ARPlanePlacement] Placement Indicator Prefab is not assigned. " +
                             "Placement still works, but there is no visual guide.", this);
        }

        placementEnabled = placementEnabledOnStart;
        if (repositionButton != null) repositionButton.SetActive(false);
    }

    private IEnumerator Start()
    {
        if (desktopFloor != null) yield break; // laptop version: there is no AR to check

        // ARSession also does this check, but waiting here lets us show a clear message.
        if (ARSession.state == ARSessionState.None || ARSession.state == ARSessionState.CheckingAvailability)
        {
            yield return ARSession.CheckAvailability();
        }

        if (ARSession.state == ARSessionState.Unsupported)
        {
            SetStatus("Sorry, this device does not support ARCore.");
        }
    }

    private void Update()
    {
        if (IsHoopPlaced || !placementEnabled)
        {
            SetIndicatorVisible(false);
            return;
        }

        ARSessionState state = ARSession.state;
        if (desktopFloor == null && state != ARSessionState.SessionTracking)
        {
            SetIndicatorVisible(false);
            SetStatus(GetSessionMessage(state));
            return;
        }

        UpdateIndicator();
        if (desktopFloor != null)
        {
            SetStatus(indicatorHasPose
                ? "Click the floor to place the hoop"
                : "Look at the floor: hold the right mouse button and drag");
        }
        else
        {
            SetStatus(indicatorHasPose
                ? "Tap a highlighted surface to place the hoop"
                : "Move your phone slowly to scan the floor");
        }

        HandleTap();
    }

    private void LateUpdate()
    {
        // ARPlaneMeshVisualizer can switch renderers back on, and new planes keep
        // appearing, so while hidden we keep them hidden every frame.
        if (planesHidden) SetPlaneRenderers(false);
    }

    private void OnDisable()
    {
        SetIndicatorVisible(false);
    }

    private void OnDestroy()
    {
        if (indicatorInstance != null) Destroy(indicatorInstance);
    }

    // ---------------------------------------------------------------- public API

    /// <summary>Called by the "Reposition Hoop" button. Picks the hoop up so it can be placed again.</summary>
    public void RepositionHoop()
    {
        if (!IsHoopPlaced) return;

        IsHoopPlaced = false;
        DetachHoop();
        if (hoopInstance != null) hoopInstance.SetActive(false);
        RemoveAnchor();

        SetPlanesVisible(true);
        if (repositionButton != null) repositionButton.SetActive(false);
        placementEnabled = true;

        HoopRemoved?.Invoke();
    }

    /// <summary>Lets the GameManager turn tap-to-place on or off (for example while paused).</summary>
    public void SetPlacementEnabled(bool value)
    {
        placementEnabled = value;
        if (!value) SetIndicatorVisible(false);
    }

    /// <summary>
    /// STAGE 10 laptop version (no AR): place the hoop on this collider - the virtual court
    /// floor made by DesktopMode - instead of on detected AR planes. Null = normal AR placement.
    /// </summary>
    public void UseDesktopFloor(Collider floor)
    {
        desktopFloor = floor;
        lastStatus = null;
    }

    public bool UsesDesktopFloor => desktopFloor != null;

    /// <summary>Where a ray hits the top of the desktop floor, turned to face the camera. False if it misses.</summary>
    public bool TryGetDesktopFloorPose(Ray ray, out Pose pose)
    {
        pose = default;
        if (desktopFloor == null || !desktopFloor.Raycast(ray, out RaycastHit hit, 50f) || hit.normal.y < 0.9f) return false;

        pose = new Pose(hit.point, FaceCameraRotation(hit.point));
        return true;
    }

    // ---------------------------------------------------------------- placement

    private void UpdateIndicator()
    {
        Vector2 screenCentre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        indicatorHasPose = TryRaycastHorizontalPlane(screenCentre, out indicatorPose);

        if (indicatorHasPose && indicatorInstance != null)
        {
            indicatorInstance.transform.SetPositionAndRotation(indicatorPose.position, indicatorPose.rotation);
        }
        SetIndicatorVisible(indicatorHasPose);
    }

    private void HandleTap()
    {
        // Pointer.current is the touchscreen on a phone and the mouse in the Editor,
        // so the same code works on the device and in XR Simulation.
        Pointer pointer = Pointer.current;
        if (pointer == null || !pointer.press.wasPressedThisFrame) return;

        Vector2 screenPosition = pointer.position.ReadValue();
        if (IsPointerOverButton(screenPosition)) return;

        if (TryRaycastHorizontalPlane(screenPosition, out Pose tapPose))
        {
            PlaceHoop(tapPose);
        }
        else if (indicatorHasPose)
        {
            PlaceHoop(indicatorPose);
        }
    }

    private bool TryRaycastHorizontalPlane(Vector2 screenPosition, out Pose pose)
    {
        if (desktopFloor != null) return TryGetDesktopFloorPose(arCamera.ScreenPointToRay(screenPosition), out pose);

        pose = default;
        if (!raycastManager.Raycast(screenPosition, s_Hits, TrackableType.PlaneWithinPolygon)) return false;

        // Hits are sorted nearest first. Use the first upward-facing horizontal plane.
        foreach (ARRaycastHit hit in s_Hits)
        {
            ARPlane plane = planeManager.GetPlane(hit.trackableId);
            if (plane != null && plane.alignment == PlaneAlignment.HorizontalUp)
            {
                pose = new Pose(hit.pose.position, FaceCameraRotation(hit.pose.position));
                return true;
            }
        }
        return false;
    }

    /// <summary>Rotation that stands upright and turns the object's +Z towards the player.</summary>
    private Quaternion FaceCameraRotation(Vector3 position)
    {
        Vector3 toCamera = arCamera.transform.position - position;
        toCamera.y = 0f;
        if (toCamera.sqrMagnitude < 0.0001f)
        {
            toCamera = -arCamera.transform.forward;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude < 0.0001f) return Quaternion.identity;
        }
        return Quaternion.LookRotation(toCamera.normalized, Vector3.up);
    }

    private void PlaceHoop(Pose pose)
    {
        DetachHoop();
        RemoveAnchor();

        // The anchor keeps this point fixed in the real world while ARCore refines its map.
        anchorObject = new GameObject("HoopAnchor");
        anchorObject.transform.SetPositionAndRotation(pose.position, pose.rotation);
        if (desktopFloor == null) anchorObject.AddComponent<ARAnchor>(); // the virtual court never drifts

        if (hoopInstance == null)
        {
            hoopInstance = Instantiate(hoopPrefab, anchorObject.transform);
            hoopInstance.name = "BasketballHoop";
        }
        else
        {
            hoopInstance.transform.SetParent(anchorObject.transform, false);
        }

        hoopInstance.transform.localPosition = Vector3.zero;
        hoopInstance.transform.localRotation = Quaternion.Euler(0f, hoopYawOffset, 0f);
        hoopInstance.SetActive(true);

        IsHoopPlaced = true;
        SetIndicatorVisible(false);
        if (hidePlanesAfterPlacement) SetPlanesVisible(false);
        if (repositionButton != null) repositionButton.SetActive(true);
        SetStatus("Hoop placed! Walk around - it stays in place.");

        HoopPlaced?.Invoke(hoopInstance.transform);
    }

    /// <summary>Moves the hoop out of the anchor so destroying the anchor doesn't destroy the hoop.</summary>
    private void DetachHoop()
    {
        if (hoopInstance != null) hoopInstance.transform.SetParent(null, true);
    }

    private void RemoveAnchor()
    {
        if (anchorObject != null)
        {
            Destroy(anchorObject);
            anchorObject = null;
        }
    }

    // ---------------------------------------------------------------- visuals & helpers

    private void SetIndicatorVisible(bool visible)
    {
        if (indicatorInstance != null && indicatorInstance.activeSelf != visible)
        {
            indicatorInstance.SetActive(visible);
        }
    }

    private void SetPlanesVisible(bool visible)
    {
        planesHidden = !visible;
        SetPlaneRenderers(visible);
    }

    /// <summary>
    /// Only the renderers are switched off. The plane GameObjects and their
    /// MeshColliders stay active so the basketball can bounce on the floor later.
    /// </summary>
    private void SetPlaneRenderers(bool visible)
    {
        if (planeManager == null) return;

        foreach (ARPlane plane in planeManager.trackables)
        {
            bool show = visible && plane.subsumedBy == null;
            plane.GetComponentsInChildren(planeRenderers);
            foreach (Renderer r in planeRenderers)
            {
                if (r.enabled != show) r.enabled = show;
            }
        }
    }

    /// <summary>True if the screen position is over a UI button/toggle/slider, so taps on buttons don't place the hoop.</summary>
    private bool IsPointerOverButton(Vector2 screenPosition)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) return false;

        PointerEventData data = new PointerEventData(eventSystem) { position = screenPosition };
        uiHits.Clear();
        eventSystem.RaycastAll(data, uiHits);

        foreach (RaycastResult result in uiHits)
        {
            if (result.gameObject != null && result.gameObject.GetComponentInParent<Selectable>() != null) return true;
        }
        return false;
    }

    private static string GetSessionMessage(ARSessionState state)
    {
        switch (state)
        {
            case ARSessionState.Unsupported: return "Sorry, this device does not support ARCore.";
            case ARSessionState.NeedsInstall:
            case ARSessionState.Installing: return "Installing Google Play Services for AR...";
            case ARSessionState.Ready: return "Starting the AR camera...";
            case ARSessionState.SessionInitializing: return "Move your phone slowly in a well-lit area";
            default: return "Checking AR support...";
        }
    }

    private void SetStatus(string message)
    {
        if (statusText == null || message == lastStatus) return;
        statusText.text = message;
        lastStatus = message;
    }
}
