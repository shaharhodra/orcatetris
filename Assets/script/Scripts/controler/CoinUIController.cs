using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using DG.Tweening;

public class CoinUIController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _coinsText;

    [Header("Coins SFX")]
    [SerializeField, Min(0f)] private float coinsSfxCooldown = 1.0f;

    [Header("Punch Animation")]
    [SerializeField] private float punchScale = 0.3f;
    [SerializeField] private float punchDuration = 0.3f;

    private Tween punchTween;
    private bool initialized;
    private float lastCoinsSfxTime = -999f;

    void Start()
    {
        var app = AppManager.instance;
        var scene = SceneManager.GetActiveScene();

        // Hide coins UI only when we are in Adventure mode AND on the main gameplay scene.
        // In the menu scene, the top bar should remain visible even if game mode is Adventure.
        if (app != null &&
            app.CurrentGameMode == AppManager.GameMode.Adventure &&
            scene.buildIndex == app.ClassicGameSceneBuildIndex)
        {
            gameObject.SetActive(false);
            return;
        }

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
            if (SoundManager.instance != null)
            {
                float now = Time.unscaledTime;
                if (coinsSfxCooldown <= 0f || now - lastCoinsSfxTime >= coinsSfxCooldown)
                {
                    lastCoinsSfxTime = now;
                    SoundManager.instance.PlayCoins();
                }
            }

            punchTween?.Kill(true);
            _coinsText.rectTransform.localScale = Vector3.one;
            punchTween = _coinsText.rectTransform
                .DOPunchScale(Vector3.one * punchScale, punchDuration, 1, 0.5f)
                .SetEase(Ease.OutBack);
        }
    }
}
