using UnityEngine;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;

public class SceneLoaderByName : MonoBehaviour
{
    [SerializeField] private int targetSceneIndex;

    // קריאה מכפתור / אירוע אחר
    public void LoadScene()
    {
        // כרגע טעינה סינכרונית ופשוטה לפי שם
        SceneManager.LoadScene(targetSceneIndex);
    }

    // אם תרצה בעתיד להפוך את זה לאסינכרוני עם UniTask ו-LoadSceneAsync:
    public async UniTask LoadSceneAsyncByName()
    {
        if (targetSceneIndex < 0 || targetSceneIndex >= SceneManager.sceneCountInBuildSettings)
        {
            Debug.LogError($"Target scene index {targetSceneIndex} is out of range");
            return;
        }

        AsyncOperation op = SceneManager.LoadSceneAsync(targetSceneIndex);
        op.allowSceneActivation = true;

        while (!op.isDone)
        {
            // כאן אפשר לעדכן Loading Bar לפי op.progress אם תרצה
            await UniTask.Yield(PlayerLoopTiming.Update);
        }
    }
}
