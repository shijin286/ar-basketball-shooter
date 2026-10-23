using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// STAGE 7 - makes a button feel alive: it shrinks a little while pressed,
/// springs back on release, and plays the click sound (AudioManager).
///
/// Attach to: every UI Button (Start, Pause, Resume, Restart, Reset Ball, Reposition Hoop).
/// Uses unscaled time, so it also animates on the pause screen.
/// </summary>
[RequireComponent(typeof(Button))]
[DisallowMultipleComponent]
public class ButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Tooltip("Size while pressed (1 = normal).")]
    [SerializeField, Range(0.7f, 1f)] private float pressedScale = 0.92f;

    [Tooltip("Seconds to shrink or grow back.")]
    [SerializeField] private float animationTime = 0.08f;

    [SerializeField] private bool playClickSound = true;

    private Button button;
    private Vector3 baseScale = Vector3.one;
    private float currentScale = 1f;
    private float targetScale = 1f;

    private void Awake()
    {
        button = GetComponent<Button>();
        baseScale = transform.localScale;
        button.onClick.AddListener(OnClicked);
    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(OnClicked);
    }

    private void OnDisable()
    {
        // A hidden button must come back at its normal size.
        currentScale = targetScale = 1f;
        transform.localScale = baseScale;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (button.interactable) targetScale = pressedScale;
    }

    public void OnPointerUp(PointerEventData eventData) => targetScale = 1f;

    public void OnPointerExit(PointerEventData eventData) => targetScale = 1f;

    private void Update()
    {
        if (Mathf.Approximately(currentScale, targetScale)) return;

        float speed = (1f - pressedScale) / Mathf.Max(0.01f, animationTime);
        currentScale = Mathf.MoveTowards(currentScale, targetScale, speed * Time.unscaledDeltaTime);
        transform.localScale = baseScale * currentScale;
    }

    private void OnClicked()
    {
        if (playClickSound && AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
    }
}
