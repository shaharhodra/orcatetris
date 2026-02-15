using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using UnityEngine.UI;
using TMPro;

public class LoadingScenesManeger : MonoBehaviour
{

    [Header("Loading UI")]
    [SerializeField] private Slider loadingBar;
    [SerializeField] private TMP_Text loadingText;

    [Header("Scene Settings")]
    [SerializeField] public int targetSceneIndex = 0;
    
    void Start()
    {
        StartLoadingSequence().Forget();
    }
    void Update()
    {
        
    }
   
    public async UniTask StartLoadingSequence()
    {
        if (targetSceneIndex < 0 || targetSceneIndex >= SceneManager.sceneCountInBuildSettings)
        {
            return;
        }

        float duration = 8.0f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float percent = t * 100f;

            if (loadingBar != null)
            {
                loadingBar.value = t;
            }

            if (loadingText != null)
            {
                loadingText.text = $"{Mathf.RoundToInt(percent)}%";
            }

            await UniTask.Yield(PlayerLoopTiming.Update);
        }


        SceneManager.LoadScene(targetSceneIndex, LoadSceneMode.Single);
    }
}
