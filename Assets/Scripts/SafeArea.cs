using UnityEngine;

/// <summary>
/// STAGE 6 - keeps the UI inside the phone's "safe area", away from the camera
/// notch, rounded corners and the gesture bar.
///
/// Attach to: a full-screen RectTransform called "SafeArea" directly under the
/// Canvas. Put every UI element inside it. It resizes itself whenever the safe
/// area, screen size or orientation changes.
/// </summary>
[RequireComponent(typeof(RectTransform))]
[DisallowMultipleComponent]
public class SafeArea : MonoBehaviour
{
    /// <summary>
    /// Widest allowed UI (width / height). 0 = no limit (phone). The laptop version (Stage 10)
    /// sets 9/16 so the phone layout stays a centred portrait column on a wide screen.
    /// </summary>
    public static float MaxAspect { get; set; }

    private RectTransform rectTransform;
    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;
    private ScreenOrientation lastOrientation;
    private float lastMaxAspect;

    private void Awake()
    {
        rectTransform = (RectTransform)transform;
        Apply();
    }

    private void Update()
    {
        if (Screen.safeArea != lastSafeArea ||
            Screen.width != lastScreenSize.x || Screen.height != lastScreenSize.y ||
            Screen.orientation != lastOrientation || MaxAspect != lastMaxAspect)
        {
            Apply();
        }
    }

    private void Apply()
    {
        lastSafeArea = Screen.safeArea;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        lastOrientation = Screen.orientation;
        lastMaxAspect = MaxAspect;

        Rect area = LimitAspect(lastSafeArea, MaxAspect);
        if (!CalculateAnchors(area, new Vector2(Screen.width, Screen.height), out Vector2 min, out Vector2 max)) return;

        rectTransform.anchorMin = min;
        rectTransform.anchorMax = max;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    /// <summary>Turns the safe area in pixels into anchors from 0 to 1. Returns false for an invalid screen.</summary>
    public static bool CalculateAnchors(Rect safeArea, Vector2 screenSize, out Vector2 anchorMin, out Vector2 anchorMax)
    {
        anchorMin = Vector2.zero;
        anchorMax = Vector2.one;
        if (screenSize.x <= 0f || screenSize.y <= 0f || safeArea.width <= 0f || safeArea.height <= 0f) return false;

        anchorMin = new Vector2(Mathf.Clamp01(safeArea.xMin / screenSize.x), Mathf.Clamp01(safeArea.yMin / screenSize.y));
        anchorMax = new Vector2(Mathf.Clamp01(safeArea.xMax / screenSize.x), Mathf.Clamp01(safeArea.yMax / screenSize.y));
        return true;
    }

    /// <summary>Narrows an area that is wider than maxAspect (width / height) to a centred column. 0 = unchanged.</summary>
    public static Rect LimitAspect(Rect area, float maxAspect)
    {
        if (maxAspect <= 0f || area.height <= 0f || area.width / area.height <= maxAspect) return area;

        float width = area.height * maxAspect;
        return new Rect(area.center.x - width * 0.5f, area.y, width, area.height);
    }
}
