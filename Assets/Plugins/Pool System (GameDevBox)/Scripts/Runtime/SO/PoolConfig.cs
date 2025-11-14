using System.Collections.Generic;
using UnityEngine;
// =======================================================
//  GameDevBox � YouTube
//  Author: Arian
//  Link: https://www.youtube.com/@GameDevBox
// =======================================================

using uPools;

public enum InstantiationMode
{
    Sequential,     // Cycle through prefabs in order
    Random,         // Randomly select from prefabs
    WeightedRandom, // Random with weighted probabilities
    FirstOnly       // Always use the first prefab
}

public enum PoolOverflowBehavior
{
    /// <summary>
    /// Don't allow creating more objects when pool is full
    /// </summary>
    Block,
    
    /// <summary>
    /// Automatically release the oldest active object and reuse it
    /// </summary>
    ReuseOldest,
    
    /// <summary>
    /// Automatically release a random active object and reuse it
    /// </summary>
    ReuseRandom
}

public enum TransformResetMode
{
    UsePrefabDefaults,    // Reset to prefab's original transform
    UseCustomDefaults,    // Reset to custom values defined in PoolConfig
    UseProvidedValues,    // Use values provided when calling Get()
    KeepCurrent          // Don't modify transform on reset
}

[CreateAssetMenu(fileName = "PoolConfig", menuName = "Pooling/Pool Config")]
public class PoolConfig : ScriptableObject
{
    [Header("Pool Settings")]
    public string poolKey;
    public string poolCategory;
    public GameObject[] prefabs;
    public int initialPoolSize = 10;
    public int maxPoolSize = 100;

    [Header("Instantiation Settings")]
    public InstantiationMode instantiationMode = InstantiationMode.Sequential;
    public float[] prefabWeights;

    [Header("Transform Settings")]
    public TransformResetMode transformResetMode = TransformResetMode.UsePrefabDefaults;
    public Vector3 defaultPosition = Vector3.zero;
    public Quaternion defaultRotation = Quaternion.identity;
    public Vector3 defaultScale = Vector3.one;

    [Header("Overflow Behavior")]
    [Tooltip("What happens when the pool reaches maximum capacity")]
    public PoolOverflowBehavior overflowBehavior = PoolOverflowBehavior.ReuseOldest;

    [Header("Advanced Settings")]
    public bool prewarmOnStart = true;
    public bool logPoolActivity = false;

    [Tooltip("Path to the PoolKeys.cs file")]
    public string poolKeysPath = "Assets/_Content/_Script/Runtime/Others/PoolKeys.cs";

    private void OnValidate()
    {
        SyncWeightsWithPrefabs();
    }

    public void SyncWeightsWithPrefabs()
    {
        if (prefabs == null)
        {
            prefabWeights = null;
            return;
        }

        if (prefabWeights == null || prefabWeights.Length != prefabs.Length)
        {
            float[] newWeights = new float[prefabs.Length];

            if (prefabWeights != null)
            {
                for (int i = 0; i < Mathf.Min(prefabWeights.Length, newWeights.Length); i++)
                {
                    newWeights[i] = prefabWeights[i];
                }
            }

            for (int i = prefabWeights?.Length ?? 0; i < newWeights.Length; i++)
            {
                newWeights[i] = 1.0f;
            }

            prefabWeights = newWeights;
        }

        for (int i = 0; i < prefabWeights.Length; i++)
        {
            prefabWeights[i] = Mathf.Clamp01(prefabWeights[i]);
        }
    }

    public float GetTotalWeight()
    {
        if (prefabWeights == null) return 0f;

        float total = 0f;
        foreach (float weight in prefabWeights)
        {
            total += weight;
        }
        return total;
    }

    public float GetNormalizedWeight(int index)
    {
        if (prefabWeights == null || index < 0 || index >= prefabWeights.Length)
            return 0f;

        float total = GetTotalWeight();
        return total > 0 ? prefabWeights[index] / total : 0f;
    }
}

public class TrackedObjectPool
{
    private readonly ObjectPool<GameObject> objectPool;
    public readonly string PoolKey;
    public readonly int MaxPoolSize;
    public int TotalCreated { get; private set; }

    public int ActiveCount => objectPool.ActiveCount;
    public int InactiveCount => objectPool.Count;
    public IReadOnlyCollection<GameObject> ActiveObjects => objectPool.ActiveObjects;

    public TrackedObjectPool(ObjectPool<GameObject> pool, string poolKey, int maxPoolSize)
    {
        objectPool = pool;
        PoolKey = poolKey;
        MaxPoolSize = maxPoolSize;
        TotalCreated = 0;
    }

    public bool CanCreateMore()
    {
        return TotalCreated < MaxPoolSize;
    }

    public GameObject Rent()
    {
        if (TotalCreated >= MaxPoolSize && objectPool.Count == 0)
        {
            Debug.LogWarning($"Max pool size ({MaxPoolSize}) reached for {PoolKey}. Cannot rent more objects.");
            return null;
        }

        var obj = objectPool.Rent();
        return obj;
    }

    public void Return(GameObject obj)
    {
        objectPool.Return(obj);
    }

    public void ReturnAll()
    {
        objectPool.ReturnAll();
    }

    public void Return(int count)
    {
        objectPool.Return(count);
    }

    public void Clear()
    {
        objectPool.Clear();
        TotalCreated = 0;
    }

    public void Dispose()
    {
        objectPool.Dispose();
    }

    public void IncrementCreatedCount()
    {
        TotalCreated++;
    }
}