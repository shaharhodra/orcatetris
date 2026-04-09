using UnityEngine;
using TMPro;
using DG.Tweening;

public class CoinUIController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _coinsText;

    [Header("Punch Animation")]
    [SerializeField] private float punchScale = 0.3f;
    [SerializeField] private float punchDuration = 0.3f;

    private Tween punchTween;
    private bool initialized;

    void Start()
    {
        if (PlayerManeger.instance != null)
        {
            PlayerManeger.instance.OnCoinsUpdatedEvent += HandleCoinsUpdated;
            HandleCoinsUpdated(PlayerManeger.instance.GetCoins());
        }
        initialized = true;
    }

    void OnDestroy()
    {
        punchTween?.Kill();
        if (PlayerManeger.instance != null)
            PlayerManeger.instance.OnCoinsUpdatedEvent -= HandleCoinsUpdated;
    }

    private void HandleCoinsUpdated(int coins)
    {
        if (_coinsText == null)
            return;

        _coinsText.text = coins.ToString();

        if (initialized)
        {
            punchTween?.Kill(true);
            _coinsText.rectTransform.localScale = Vector3.one;
            punchTween = _coinsText.rectTransform
                .DOPunchScale(Vector3.one * punchScale, punchDuration, 1, 0.5f)
                .SetEase(Ease.OutBack);
        }
    }
}
