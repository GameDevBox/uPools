// =======================================================
//  GameDevBox – YouTube
//  Author: Arian
//  Link: https://www.youtube.com/@GameDevBox
// =======================================================

using UnityEngine;
using uPools;

public class DefaultPoolable : MonoBehaviour, IPoolCallbackReceiver
{
    public void OnInitialize()
    {
        Debug.Log($"OnInitialize - Object initialized for pooling");
    }

    public void OnRent()
    {
        Debug.Log($"OnRent - Object taken from pool and activated");
    }

    public void OnReturn()
    {
        Debug.Log($"OnReturn - Object returned to pool and deactivated");
    }

    public void OnPoolDestroy()
    {
        Debug.Log($"OnDestroy - Object Destroyed from pool");
    }
}