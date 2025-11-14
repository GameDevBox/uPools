// =======================================================
//  GameDevBox – YouTube
//  Author: Arian
//  Link: https://www.youtube.com/@GameDevBox
// =======================================================

using UnityEngine;

public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    public static T Instance { get; private set; }

    protected virtual bool ShouldPersist => true; // Override this in derived class to control persistence

    protected virtual void Awake()
    {
        if (Instance == null)
        {
            Instance = this as T;
            if (ShouldPersist)
                DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    protected virtual void OnDestroy()
    {

    }
}
