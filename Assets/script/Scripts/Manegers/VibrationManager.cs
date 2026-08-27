using UnityEngine;

// VibrationManager – Singleton for global haptic feedback.
// Self-bootstrapping (no scene wiring needed) so it works from every scene, unlike
// SoundManager which only lives in gameScene.unity.
public class VibrationManager : MonoBehaviour
{
    public static VibrationManager instance;

    private const float MinIntervalSeconds = 0.08f;
    private float lastVibrateTime = -1f;

    // Handheld.Vibrate() always fires the OS's default full-strength buzz — there's no
    // intensity knob on it. On Android we bypass it and drive the Vibrator service
    // directly with VibrationEffect.createOneShot(duration, amplitude), which is the only
    // way to actually turn the strength down. Amplitude is 1-255; tune this to taste.
    private const int AndroidAmplitude = 80;
    private const long AndroidDurationMs = 30;

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject androidVibrator;
    private AndroidJavaObject vibrationEffect;
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
            return;

        var go = new GameObject("VibrationManager");
        go.AddComponent<VibrationManager>();
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            InitAndroidVibrator();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitAndroidVibrator()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            androidVibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");

            using var effectClass = new AndroidJavaClass("android.os.VibrationEffect");
            vibrationEffect = effectClass.CallStatic<AndroidJavaObject>(
                "createOneShot", AndroidDurationMs, AndroidAmplitude);
        }
        catch
        {
            // Older/OEM devices without VibrationEffect support fall through to Handheld.Vibrate().
            androidVibrator = null;
            vibrationEffect = null;
        }
#endif
    }

    public void Vibrate()
    {
        if (SettingsManager.instance != null && !SettingsManager.instance.VibrationEnabled)
            return;

        if (Time.unscaledTime - lastVibrateTime < MinIntervalSeconds)
            return;

        lastVibrateTime = Time.unscaledTime;

#if UNITY_ANDROID && !UNITY_EDITOR
        if (androidVibrator != null && vibrationEffect != null)
        {
            androidVibrator.Call("vibrate", vibrationEffect);
            return;
        }
#endif

        Handheld.Vibrate();
    }
}
