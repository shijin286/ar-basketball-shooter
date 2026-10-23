using System.Collections;
using UnityEngine;

/// <summary>
/// STAGE 7 - visual celebration of a basket:
///  - a confetti burst (Prefabs/BasketEffect particle system) just under the rim,
///  - the net stretches down and springs back, as if the ball pulled it,
///  - a short phone vibration (Android).
///
/// Attach to: GameSystems. Listens to GameManager.BasketScored and finds the
/// hoop's Rim and Net when ARPlanePlacement places it. One particle system is
/// created once and reused for every basket.
/// </summary>
[DisallowMultipleComponent]
public class ScoreEffects : MonoBehaviour
{
    [Header("References (found automatically if empty)")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private ARPlanePlacement placement;

    [Header("Particles")]
    [Tooltip("Prefabs/BasketEffect (made with Tools > AR Basketball > Create Basket Effect).")]
    [SerializeField] private ParticleSystem basketEffectPrefab;

    [Tooltip("The burst starts this far below the rim centre (metres).")]
    [SerializeField] private float effectBelowRim = 0.15f;

    [Header("Net")]
    [SerializeField] private bool wobbleNet = true;
    [SerializeField, Range(1f, 2f)] private float netStretch = 1.3f;
    [SerializeField] private float netWobbleTime = 0.35f;

    [Header("Haptics")]
    [SerializeField] private bool vibrateOnBasket = true;

    private ParticleSystem effect;
    private Transform rim;
    private Transform net;
    private Vector3 netBaseScale = Vector3.one;
    private Coroutine wobbleRoutine;

    private void Awake()
    {
        if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();
        if (placement == null) placement = FindFirstObjectByType<ARPlanePlacement>();

        if (gameManager == null) Debug.LogError("[ScoreEffects] GameManager not found.", this);
        if (basketEffectPrefab == null) Debug.LogWarning("[ScoreEffects] Basket Effect Prefab is not assigned - no confetti.", this);
    }

    private void OnEnable()
    {
        if (gameManager != null) gameManager.BasketScored += OnBasketScored;
        if (placement != null)
        {
            placement.HoopPlaced += OnHoopPlaced;
            if (placement.IsHoopPlaced) OnHoopPlaced(placement.HoopTransform);
        }
    }

    private void OnDisable()
    {
        if (gameManager != null) gameManager.BasketScored -= OnBasketScored;
        if (placement != null) placement.HoopPlaced -= OnHoopPlaced;
        ResetNet();
    }

    private void OnDestroy()
    {
        if (effect != null) Destroy(effect.gameObject);
    }

    // ---------------------------------------------------------------- public API

    /// <summary>Plays the confetti burst at a world position.</summary>
    public void PlayBurst(Vector3 position)
    {
        if (basketEffectPrefab == null) return;
        if (effect == null)
        {
            effect = Instantiate(basketEffectPrefab);
            effect.name = "BasketEffect";
        }

        effect.transform.position = position;
        effect.Clear(true);
        effect.Play(true);
    }

    // ---------------------------------------------------------------- events

    private void OnHoopPlaced(Transform hoop)
    {
        ResetNet();
        rim = hoop != null ? hoop.Find("Rim") : null;
        net = rim != null ? rim.Find("Net") : null;
        if (net != null) netBaseScale = net.localScale;
    }

    private void OnBasketScored(BasketballController ball)
    {
        Vector3 position = rim != null
            ? rim.position - rim.up * effectBelowRim
            : (ball != null ? ball.transform.position : transform.position);
        PlayBurst(position);

        if (wobbleNet && net != null)
        {
            if (wobbleRoutine != null) StopCoroutine(wobbleRoutine);
            wobbleRoutine = StartCoroutine(WobbleNet());
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        if (vibrateOnBasket) Handheld.Vibrate();
#endif
    }

    // ---------------------------------------------------------------- net animation

    private IEnumerator WobbleNet()
    {
        // The net hangs down from the rim, so stretching Y pulls it downwards.
        for (float time = 0f; time < netWobbleTime; time += Time.deltaTime)
        {
            float k = time / netWobbleTime;
            float stretch = 1f + (netStretch - 1f) * Mathf.Sin(k * Mathf.PI) * (1f - 0.5f * k);
            net.localScale = new Vector3(netBaseScale.x / Mathf.Sqrt(stretch), netBaseScale.y * stretch, netBaseScale.z / Mathf.Sqrt(stretch));
            yield return null;
        }
        ResetNet();
    }

    private void ResetNet()
    {
        if (wobbleRoutine != null)
        {
            StopCoroutine(wobbleRoutine);
            wobbleRoutine = null;
        }
        if (net != null) net.localScale = netBaseScale;
    }
}
