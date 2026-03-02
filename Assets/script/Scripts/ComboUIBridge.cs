using UnityEngine;
using TMPro;
using DG.Tweening;

public class ComboUIBridge : MonoBehaviour
{
    [SerializeField] private ComboController comboController;

    [Header("Debug")]
    [SerializeField] private bool debugLogs;

    [Header("Popup")]
    [SerializeField] private RectTransform popupRoot;
    [SerializeField] private TextMeshProUGUI popupText;
    [SerializeField] private float popupDuration = 0.6f;
    [SerializeField] private float popupTweenDuration = 0.15f;

    [Header("Screen Shake")]
    [SerializeField] private Transform shakeTarget;
    [SerializeField] private float baseShakeDuration = 0.15f;
    [SerializeField] private float baseShakeStrength = 0.15f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip combo1Clip;
    [SerializeField] private AudioClip combo2Clip;
    [SerializeField] private AudioClip combo3Clip;
    [SerializeField] private AudioClip combo4Clip;
    [SerializeField] private AudioClip combo5PlusClip;

    private Tween popupTween;
    private Tween shakeTween;
    private bool subscribed;

    private void Awake()
    {
        if (comboController == null)
            comboController = FindFirstObjectByType<ComboController>();

        if (shakeTarget == null && Camera.main != null)
            shakeTarget = Camera.main.transform;

        if (audioSource == null)
            audioSource = FindFirstObjectByType<AudioSource>();

        if (popupRoot != null)
            popupRoot.gameObject.SetActive(false);
    }

    private void Start()
    {
        TrySubscribe();
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void OnDisable()
    {
        TryUnsubscribe();
    }

    private void TrySubscribe()
    {
        if (subscribed)
            return;

        if (comboController == null)
            comboController = FindFirstObjectByType<ComboController>();

        if (comboController == null || comboController.ComboManager == null)
            return;

        comboController.ComboManager.OnComboStep += HandleComboStep;
        comboController.ComboManager.OnComboEnded += HandleComboEnded;
        subscribed = true;

        if (debugLogs)
            Debug.Log("[ComboUIBridge] Subscribed to ComboManager events");
    }

    private void TryUnsubscribe()
    {
        if (!subscribed)
            return;

        if (comboController != null && comboController.ComboManager != null)
        {
            comboController.ComboManager.OnComboStep -= HandleComboStep;
            comboController.ComboManager.OnComboEnded -= HandleComboEnded;
        }

        subscribed = false;

        if (debugLogs)
            Debug.Log("[ComboUIBridge] Unsubscribed from ComboManager events");
    }

    private void HandleComboStep(ComboEventArgs args)
    {
        if (debugLogs)
            Debug.Log($"[ComboUIBridge] Combo step: combo={args.ComboCount}, linesThisStep={args.LinesClearedThisStep}, tier={args.Tier}");
        ShowPopup(args);
        PlayComboSound(args.Tier);
        DoShake(args.Tier);
    }

    private void HandleComboEnded()
    {
        if (debugLogs)
            Debug.Log("[ComboUIBridge] Combo ended");
        HidePopupImmediate();
    }

    private void ShowPopup(ComboEventArgs args)
    {
        if (popupRoot == null || popupText == null)
            return;

        popupText.text = $"COMBO x{args.ComboCount}";

        popupTween?.Kill();
        popupRoot.gameObject.SetActive(true);
        popupRoot.localScale = Vector3.zero;

        popupTween = popupRoot
            .DOScale(1f, popupTweenDuration)
            .SetEase(Ease.OutBack)
            .OnComplete(() =>
            {
                popupTween = popupRoot
                    .DOScale(0f, popupTweenDuration)
                    .SetDelay(popupDuration)
                    .SetEase(Ease.InBack)
                    .OnComplete(() => popupRoot.gameObject.SetActive(false));
            });
    }

    private void HidePopupImmediate()
    {
        popupTween?.Kill();
        if (popupRoot != null)
            popupRoot.gameObject.SetActive(false);
    }

    private void DoShake(ComboTier tier)
    {
        if (shakeTarget == null)
            return;

        float strength = baseShakeStrength * GetShakeMultiplier(tier);
        float duration = baseShakeDuration * GetShakeDurationMultiplier(tier);

        shakeTween?.Kill();
        shakeTween = shakeTarget.DOShakePosition(duration, strength, 20, 90f, false, true);
    }

    private void PlayComboSound(ComboTier tier)
    {
        if (audioSource == null)
            return;

        var clip = GetClipForTier(tier);
        if (clip == null)
            return;

        audioSource.PlayOneShot(clip);
    }

    private AudioClip GetClipForTier(ComboTier tier)
    {
        switch (tier)
        {
            case ComboTier.Combo1: return combo1Clip;
            case ComboTier.Combo2: return combo2Clip;
            case ComboTier.Combo3: return combo3Clip;
            case ComboTier.Combo4: return combo4Clip;
            case ComboTier.Combo5Plus: return combo5PlusClip;
            default: return null;
        }
    }

    private static float GetShakeMultiplier(ComboTier tier)
    {
        switch (tier)
        {
            case ComboTier.Combo1: return 1f;
            case ComboTier.Combo2: return 1.35f;
            case ComboTier.Combo3: return 1.7f;
            case ComboTier.Combo4: return 2.1f;
            case ComboTier.Combo5Plus: return 2.6f;
            default: return 0f;
        }
    }

    private static float GetShakeDurationMultiplier(ComboTier tier)
    {
        switch (tier)
        {
            case ComboTier.Combo1: return 1f;
            case ComboTier.Combo2: return 1.1f;
            case ComboTier.Combo3: return 1.2f;
            case ComboTier.Combo4: return 1.35f;
            case ComboTier.Combo5Plus: return 1.5f;
            default: return 0f;
        }
    }
}
