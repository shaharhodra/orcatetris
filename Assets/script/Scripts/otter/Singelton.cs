using UnityEngine;

public class Singleton<T> : MonoBehaviour where T : Singleton<T>
{
    public bool IsDestroyOnLoad = true;
	public static T instance { get; protected set; }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        else
        {
            instance = (T)this;
            if (!IsDestroyOnLoad)
            {
                DontDestroyOnLoad(gameObject);
            }
        }
    }
}