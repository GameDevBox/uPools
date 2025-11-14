// =======================================================
//  GameDevBox – YouTube
//  Author: Arian
//  Link: https://www.youtube.com/@GameDevBox
// =======================================================

using System.Collections.Generic;
using UnityEngine;
using uPools;


public class PoolSystemManager : Singleton<PoolSystemManager>
{
    protected override bool ShouldPersist => false;

    [SerializeField] private List<PoolConfig> poolConfigs = new List<PoolConfig>();

    [Header("Pool Parent Settings")]
    [SerializeField] private Transform poolParent;
    [SerializeField] private bool createPoolParentIfNull = true;
    [SerializeField] private string poolParentName = "PooledObjects";

    [Header("Pool Grouping")]
    [SerializeField] private bool groupPoolsByCategory = true;
    [SerializeField] private string defaultCategoryName = "Default";

    private Dictionary<string, TrackedObjectPool> poolDictionary;
    private Dictionary<string, PoolConfig> configDictionary;
    private Dictionary<string, int> prefabIndices;
    private Dictionary<GameObject, string> objectToPoolKeyMap;
    private Dictionary<string, Transform> poolCategoryParents;
    private Dictionary<string, System.Random> weightedRandomGenerators;
    private Dictionary<string, LinkedList<GameObject>> activeObjectsByPool;
    private Dictionary<GameObject, (Vector3 position, Quaternion rotation, Vector3 scale)> prefabDefaults;
    private Dictionary<GameObject, GameObject> instanceToPrefabMap;

    protected override void Awake()
    {
        base.Awake();
        InitializePoolParent();
        InitializePools();
    }

    private void InitializePoolParent()
    {
        if (poolParent == null && createPoolParentIfNull)
        {
            poolParent = new GameObject(poolParentName).transform;
            poolParent.SetParent(transform);
            poolParent.localPosition = Vector3.zero;
            poolParent.localRotation = Quaternion.identity;
        }

        poolCategoryParents = new Dictionary<string, Transform>();
        weightedRandomGenerators = new Dictionary<string, System.Random>();
    }

    public void InitializePools()
    {
        poolDictionary = new Dictionary<string, TrackedObjectPool>();
        configDictionary = new Dictionary<string, PoolConfig>();
        prefabIndices = new Dictionary<string, int>();
        objectToPoolKeyMap = new Dictionary<GameObject, string>();
        activeObjectsByPool = new Dictionary<string, LinkedList<GameObject>>();
        prefabDefaults = new Dictionary<GameObject, (Vector3, Quaternion, Vector3)>();
        instanceToPrefabMap = new Dictionary<GameObject, GameObject>();

        foreach (var config in poolConfigs)
        {
            RegisterPool(config);
        }
    }

    public void RegisterPool(PoolConfig config)
    {
        if (poolDictionary.ContainsKey(config.poolKey))
        {
            Debug.LogWarning($"Pool with key '{config.poolKey}' already exists!");
            return;
        }

        prefabIndices[config.poolKey] = 0;
        configDictionary[config.poolKey] = config;
        activeObjectsByPool[config.poolKey] = new LinkedList<GameObject>();

        if (config.instantiationMode == InstantiationMode.WeightedRandom)
        {
            weightedRandomGenerators[config.poolKey] = new System.Random();
            ValidateAndSetupWeights(config);
        }

        ObjectPool<GameObject> objectPool = new ObjectPool<GameObject>(
            createFunc: () => CreatePooledObject(config),
            onRent: obj => OnObjectGet(obj, config),
            onReturn: obj => OnObjectRelease(obj, config),
            onDestroy: obj => OnObjectDestroy(obj, config)
        );


        objectPool.Initialize(config.initialPoolSize);

        var trackedPool = new TrackedObjectPool(objectPool, config.poolKey, config.maxPoolSize);
        poolDictionary[config.poolKey] = trackedPool;

        if (config.prewarmOnStart)
        {
            objectPool.Prewarm(config.initialPoolSize);
        }

        if (config.logPoolActivity)
        {
            Debug.Log($"Pool registered: {config.poolKey} (Size: {config.initialPoolSize}, Max: {config.maxPoolSize})");
        }
    }

    private void ValidateAndSetupWeights(PoolConfig config)
    {
        if (config.prefabWeights == null || config.prefabWeights.Length != config.prefabs.Length)
        {
            config.prefabWeights = new float[config.prefabs.Length];
            for (int i = 0; i < config.prefabWeights.Length; i++)
            {
                config.prefabWeights[i] = 1f;
            }
            Debug.LogWarning($"Weights not properly configured for pool '{config.poolKey}'. Using equal weights.");
        }

        for (int i = 0; i < config.prefabWeights.Length; i++)
        {
            if (config.prefabWeights[i] <= 0)
            {
                config.prefabWeights[i] = 0.1f;
                Debug.LogWarning($"Invalid weight at index {i} for pool '{config.poolKey}'. Setting to 0.1");
            }
        }
    }

    private void CapturePrefabDefaults(GameObject prefab)
    {
        if (prefab == null) return;

        if (!prefabDefaults.ContainsKey(prefab))
        {
            prefabDefaults[prefab] = (
                prefab.transform.localPosition,
                prefab.transform.localRotation,
                prefab.transform.localScale
            );
        }
    }

    private Transform GetPoolParent(PoolConfig config)
    {
        if (!groupPoolsByCategory || poolParent == null)
        {
            return poolParent;
        }

        string category = GetPoolCategory(config);

        if (!poolCategoryParents.TryGetValue(category, out Transform categoryParent))
        {
            categoryParent = CreateCategoryParent(category);
            poolCategoryParents[category] = categoryParent;
        }

        return categoryParent;
    }

    private string GetPoolCategory(PoolConfig config)
    {
        if (!string.IsNullOrEmpty(config.poolCategory))
        {
            return config.poolCategory;
        }
        return defaultCategoryName;
    }

    private Transform CreateCategoryParent(string category)
    {
        GameObject categoryGO = new GameObject(category);
        Transform categoryTransform = categoryGO.transform;
        categoryTransform.SetParent(poolParent);
        categoryTransform.localPosition = Vector3.zero;
        categoryTransform.localRotation = Quaternion.identity;
        return categoryTransform;
    }

    private GameObject CreatePooledObject(PoolConfig config)
    {
        if (config.prefabs == null || config.prefabs.Length == 0)
        {
            Debug.LogError($"No prefabs assigned to pool: {config.poolKey}");
            return null;
        }

        if (!prefabIndices.ContainsKey(config.poolKey))
        {
            Debug.LogWarning($"prefabIndices missing key '{config.poolKey}', initializing to 0");
            prefabIndices[config.poolKey] = 0;
        }

        // Check if we've reached max pool size
        if (poolDictionary.ContainsKey(config.poolKey) &&
            !poolDictionary[config.poolKey].CanCreateMore())
        {
            if (config.logPoolActivity)
            {
                Debug.LogWarning($"Max pool size ({config.maxPoolSize}) reached for {config.poolKey}. Cannot create more objects.");
            }
            return null;
        }

        GameObject selectedPrefab = SelectPrefabByMode(config);

        if (selectedPrefab == null)
        {
            Debug.LogError($"Failed to select prefab for pool: {config.poolKey}");
            return null;
        }

        // Capture prefab defaults before instantiating
        CapturePrefabDefaults(selectedPrefab);

        GameObject obj = Instantiate(selectedPrefab);

        instanceToPrefabMap[obj] = selectedPrefab;

        if (obj == null)
        {
            Debug.LogError($"Failed to instantiate prefab for pool: {config.poolKey}");
            return null;
        }

        objectToPoolKeyMap[obj] = config.poolKey;
        obj.name = $"{obj.name} | KEY:{config.poolKey}";

        Transform parentTransform = GetPoolParent(config);
        if (parentTransform != null)
        {
            obj.transform.SetParent(parentTransform);
        }

        // APPLY TRANSFORM RESET WHEN OBJECT IS FIRST CREATED
        ApplyTransformReset(obj, config, Vector3.zero, Quaternion.identity, Vector3.one);

        var poolables = obj.GetComponentsInChildren<IPoolCallbackReceiver>();
        foreach (var poolable in poolables)
        {
            poolable?.OnInitialize();
        }

        if (poolDictionary.ContainsKey(config.poolKey))
        {
            poolDictionary[config.poolKey].IncrementCreatedCount();
        }

        obj.SetActive(false);
        return obj;
    }

    private GameObject SelectPrefabByMode(PoolConfig config)
    {
        if (config.prefabs.Length == 0) return null;

        switch (config.instantiationMode)
        {
            case InstantiationMode.Sequential:
                return GetSequentialPrefab(config);

            case InstantiationMode.Random:
                return GetRandomPrefab(config);

            case InstantiationMode.WeightedRandom:
                return GetWeightedRandomPrefab(config);

            case InstantiationMode.FirstOnly:
                return config.prefabs[0];

            default:
                Debug.LogWarning($"Unknown instantiation mode: {config.instantiationMode}. Using Sequential.");
                return GetSequentialPrefab(config);
        }
    }

    private GameObject GetSequentialPrefab(PoolConfig config)
    {
        int index = prefabIndices[config.poolKey];
        GameObject prefab = config.prefabs[index];

        prefabIndices[config.poolKey] = (index + 1) % config.prefabs.Length;

        return prefab;
    }

    private GameObject GetRandomPrefab(PoolConfig config)
    {
        int randomIndex = UnityEngine.Random.Range(0, config.prefabs.Length);
        return config.prefabs[randomIndex];
    }

    private GameObject GetWeightedRandomPrefab(PoolConfig config)
    {
        if (!weightedRandomGenerators.TryGetValue(config.poolKey, out System.Random random))
        {
            random = new System.Random();
            weightedRandomGenerators[config.poolKey] = random;
        }

        float totalWeight = 0f;
        foreach (float weight in config.prefabWeights)
        {
            totalWeight += weight;
        }

        float randomValue = (float)(random.NextDouble() * totalWeight);

        float currentWeight = 0f;
        for (int i = 0; i < config.prefabs.Length; i++)
        {
            currentWeight += config.prefabWeights[i];
            if (randomValue <= currentWeight)
            {
                return config.prefabs[i];
            }
        }

        return config.prefabs[0];
    }

    private void OnObjectGet(GameObject obj, PoolConfig config)
    {
        obj.SetActive(true);

        // Track active object
        if (activeObjectsByPool.TryGetValue(config.poolKey, out LinkedList<GameObject> activeObjects))
        {
            activeObjects.AddLast(obj);
        }

        var poolables = obj.GetComponentsInChildren<IPoolCallbackReceiver>();
        foreach (var poolable in poolables)
        {
            poolable?.OnRent();
        }

        if (config.logPoolActivity)
        {
            Debug.Log($"Object rented from pool: {config.poolKey} (Active: {GetActiveCount(config.poolKey)})");
        }
    }

    private void OnObjectRelease(GameObject obj, PoolConfig config)
    {
        obj.SetActive(false);

        // Remove from active tracking
        if (activeObjectsByPool.TryGetValue(config.poolKey, out LinkedList<GameObject> activeObjects))
        {
            activeObjects.Remove(obj);
        }

        Transform parentTransform = GetPoolParent(config);
        if (parentTransform != null)
        {
            obj.transform.SetParent(parentTransform);
        }

        var poolables = obj.GetComponentsInChildren<IPoolCallbackReceiver>();
        foreach (var poolable in poolables)
        {
            poolable?.OnReturn();
        }

        if (config.logPoolActivity)
        {
            Debug.Log($"Object returned to pool: {config.poolKey} (Active: {GetActiveCount(config.poolKey)})");
        }
    }

    private void OnObjectDestroy(GameObject obj, PoolConfig config)
    {
        if (objectToPoolKeyMap.ContainsKey(obj))
        {
            // Remove from active tracking
            if (objectToPoolKeyMap.TryGetValue(obj, out string poolKey) &&
                activeObjectsByPool.TryGetValue(poolKey, out LinkedList<GameObject> activeObjects))
            {
                activeObjects.Remove(obj);
            }

            var poolables = obj.GetComponentsInChildren<IPoolCallbackReceiver>();
            foreach (var poolable in poolables)
            {
                poolable?.OnPoolDestroy();
            }
            objectToPoolKeyMap.Remove(obj);
            instanceToPrefabMap.Remove(obj);
        }
    }

    public GameObject Get(string poolKey, Vector3 position = default, Quaternion rotation = default, Vector3 scale = default)
    {
        if (!poolDictionary.ContainsKey(poolKey))
        {
            Debug.LogError($"Pool not found: {poolKey}");
            return null;
        }

        var config = configDictionary[poolKey];
        var pool = poolDictionary[poolKey];
        var overflowBehavior = GetOverflowBehavior(config);

        // Check if we need to handle overflow
        if (pool.ActiveCount >= config.maxPoolSize && pool.InactiveCount == 0)
        {
            return HandlePoolOverflow(poolKey, config, overflowBehavior, position, rotation, scale);
        }

        GameObject obj = pool.Rent();
        if (obj == null)
            return null;

        if (!obj.activeSelf)
            OnObjectGet(obj, config);

        // Apply transform based on the configured reset mode
        ApplyTransformReset(obj, config, position, rotation, scale);

        return obj;
    }

    public T Get<T>(string poolKey, Vector3 position = default, Quaternion rotation = default, Vector3 scale = default) where T : Component
    {
        GameObject obj = Get(poolKey, position, rotation, scale);
        return obj != null ? obj.GetComponent<T>() : null;
    }
    
    public void ReleaseAll(string poolKey)
    {
        if (!poolDictionary.ContainsKey(poolKey))
        {
            Debug.LogError($"Pool not found: {poolKey}");
            return;
        }

        poolDictionary[poolKey].ReturnAll();
        Debug.Log($"Released all objects from pool: {poolKey}");
    }

    public void Release(string poolKey, int count = 1)
    {
        if (!poolDictionary.ContainsKey(poolKey) || count <= 0)
            return;

        poolDictionary[poolKey].Return(count);
    }

    public void ReleaseAll()
    {
        foreach (var poolKey in poolDictionary.Keys)
        {
            poolDictionary[poolKey].ReturnAll();
        }
        Debug.Log("Released all objects from all pools");
    }

    public void Release(GameObject obj)
    {
        if (obj != null && objectToPoolKeyMap.TryGetValue(obj, out string poolKey))
        {
            if (poolDictionary.ContainsKey(poolKey))
            {
                poolDictionary[poolKey].Return(obj);
            }
        }
        else
        {
            Debug.LogWarning($"Object {obj.name} is not tracked by any pool, destroying instead.");
            Destroy(obj);
        }
    }

    public void Release(Component component)
    {
        if (component != null)
        {
            Release(component.gameObject);
        }
    }

    private GameObject FindOriginalPrefab(GameObject obj, PoolConfig config)
    {
        return instanceToPrefabMap.TryGetValue(obj, out GameObject prefab) ? prefab :
               (config.prefabs.Length > 0 ? config.prefabs[0] : null);
    }

    private void ApplyTransformReset(GameObject obj, PoolConfig config, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        switch (config.transformResetMode)
        {
            case TransformResetMode.UsePrefabDefaults:
                // Reset to the original prefab's transform
                if (objectToPoolKeyMap.TryGetValue(obj, out string poolKey) &&
                    config.prefabs.Length > 0)
                {
                    // Find which prefab this object was created from
                    var originalPrefab = FindOriginalPrefab(obj, config);
                    if (originalPrefab != null && prefabDefaults.ContainsKey(originalPrefab))
                    {
                        var defaults = prefabDefaults[originalPrefab];
                        obj.transform.localPosition = defaults.position;
                        obj.transform.localRotation = defaults.rotation;
                        obj.transform.localScale = defaults.scale;
                    }
                }
                break;

            case TransformResetMode.UseCustomDefaults:
                // Reset to custom defaults defined in PoolConfig
                obj.transform.localPosition = config.defaultPosition;
                obj.transform.localRotation = config.defaultRotation;
                obj.transform.localScale = config.defaultScale;
                break;

            case TransformResetMode.UseProvidedValues:
                // Use the values provided in the Get() call
                obj.transform.position = position;
                obj.transform.rotation = rotation;
                obj.transform.localScale = scale;
                break;

            case TransformResetMode.KeepCurrent:
                // Don't modify the transform - keep whatever it had
                break;
        }
    }

    private GameObject HandlePoolOverflow(string poolKey, PoolConfig config, PoolOverflowBehavior overflowBehavior,
        Vector3 position, Quaternion rotation, Vector3 scale)
    {
        switch (overflowBehavior)
        {
            case PoolOverflowBehavior.Block:
                if (config.logPoolActivity)
                {
                    Debug.LogWarning($"Pool '{poolKey}' is at maximum capacity ({config.maxPoolSize}). Cannot create more objects.");
                }
                return null;

            case PoolOverflowBehavior.ReuseOldest:
                return ReuseOldestObject(poolKey, config, position, rotation, scale);

            case PoolOverflowBehavior.ReuseRandom:
                return ReuseRandomObject(poolKey, config, position, rotation, scale);

            default:
                Debug.LogWarning($"Unknown overflow behavior: {overflowBehavior}. Using ReuseOldest.");
                return ReuseOldestObject(poolKey, config, position, rotation, scale);
        }
    }

    private GameObject ReuseOldestObject(string poolKey, PoolConfig config, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        if (activeObjectsByPool.TryGetValue(poolKey, out LinkedList<GameObject> activeObjects) && activeObjects.Count > 0)
        {
            GameObject oldestObject = activeObjects.First.Value;
            if (oldestObject != null)
            {
                if (config.logPoolActivity)
                {
                    Debug.Log($"Reusing oldest object from pool '{poolKey}' (Max: {config.maxPoolSize})");
                }

                // Remove from tracking first
                activeObjects.RemoveFirst();

                // Return and immediately rent the same object
                poolDictionary[poolKey].Return(oldestObject);
                poolDictionary[poolKey].Rent();

                // Use the new transform reset system
                ApplyTransformReset(oldestObject, config, position, rotation, scale);

                // Add back to end of list (now it's the newest)
                activeObjects.AddLast(oldestObject);

                return oldestObject;
            }
        }

        if (config.logPoolActivity)
        {
            Debug.LogWarning($"No reusable objects found in pool '{poolKey}'");
        }
        return null;
    }

    private GameObject ReuseRandomObject(string poolKey, PoolConfig config, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        if (activeObjectsByPool.TryGetValue(poolKey, out LinkedList<GameObject> activeObjects) && activeObjects.Count > 0)
        {
            if (activeObjects.Count == 1)
            {
                return ReuseOldestObject(poolKey, config, position, rotation, scale);
            }

            var activeArray = new GameObject[activeObjects.Count];
            activeObjects.CopyTo(activeArray, 0);
            GameObject randomObject = activeArray[Random.Range(0, activeArray.Length)];
            return ProcessReusedObject(randomObject, poolKey, config, position, rotation, scale, activeObjects);

        }
        return null;
    }

    private GameObject ProcessReusedObject(GameObject obj, string poolKey, PoolConfig config,
        Vector3 position, Quaternion rotation, Vector3 scale, LinkedList<GameObject> activeObjects)
    {
        if (obj != null)
        {
            if (config.logPoolActivity)
            {
                Debug.Log($"Reusing random object from pool '{poolKey}' (Max: {config.maxPoolSize})");
            }

            // Remove from current position
            activeObjects.Remove(obj);

            // Return and immediately rent the same object
            poolDictionary[poolKey].Return(obj);
            poolDictionary[poolKey].Rent();

            // Apply transform reset
            ApplyTransformReset(obj, config, position, rotation, scale);

            // Add back to end of list (now it's the newest)
            activeObjects.AddLast(obj);

            return obj;
        }
        return null;
    }

    private PoolOverflowBehavior GetOverflowBehavior(PoolConfig config)
    {
        // Use the pool-specific overflow behavior from PoolConfig
        return config.overflowBehavior;
    }

    protected override void OnDestroy()
    {
        if (poolDictionary != null)
        {
            foreach (var pool in poolDictionary.Values)
            {
                pool?.Dispose();
            }
        }
        base.OnDestroy();
    }

    #region Utilities

    public void SetPoolTransformDefaults(string poolKey, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        if (configDictionary.ContainsKey(poolKey))
        {
            var config = configDictionary[poolKey];
            config.defaultPosition = position;
            config.defaultRotation = rotation;
            config.defaultScale = scale;
            config.transformResetMode = TransformResetMode.UseCustomDefaults;
        }
    }

    public void SetPoolTransformMode(string poolKey, TransformResetMode mode)
    {
        if (configDictionary.ContainsKey(poolKey))
        {
            configDictionary[poolKey].transformResetMode = mode;
        }
    }

    public TransformResetMode GetPoolTransformMode(string poolKey)
    {
        return configDictionary.ContainsKey(poolKey) ? configDictionary[poolKey].transformResetMode : TransformResetMode.UsePrefabDefaults;
    }

    public int GetActiveCount(string poolKey)
    {
        return poolDictionary.ContainsKey(poolKey) ? poolDictionary[poolKey].ActiveCount : -1;
    }

    public int GetInactiveCount(string poolKey)
    {
        return poolDictionary.ContainsKey(poolKey) ? poolDictionary[poolKey].InactiveCount : -1;
    }

    public int GetTotalCreated(string poolKey)
    {
        return poolDictionary.ContainsKey(poolKey) ? poolDictionary[poolKey].TotalCreated : -1;
    }

    public bool PoolExists(string poolKey)
    {
        return poolDictionary != null && poolDictionary.ContainsKey(poolKey);
    }

    public Transform GetCurrentPoolParent()
    {
        return poolParent;
    }

    public Dictionary<string, Transform> GetCategoryParents()
    {
        return new Dictionary<string, Transform>(poolCategoryParents);
    }

    public void LogPoolStatistics()
    {
        if (poolDictionary == null) return;

        foreach (var kvp in poolDictionary)
        {
            Debug.Log($"Pool '{kvp.Key}': Active: {kvp.Value.ActiveCount}, Inactive: {kvp.Value.InactiveCount}, Total: {kvp.Value.TotalCreated}");
        }
    }

    public void ClearPool(string poolKey)
    {
        if (poolDictionary.ContainsKey(poolKey))
        {
            poolDictionary[poolKey].Clear();
        }
    }

    public IEnumerable<string> GetAllPoolKeys()
    {
        if (poolDictionary == null)
            return new string[0];

        return poolDictionary.Keys;
    }

    public string[] GetAllPoolKeysArray()
    {
        if (poolDictionary == null)
            return new string[0];

        var keys = new string[poolDictionary.Count];
        poolDictionary.Keys.CopyTo(keys, 0);
        return keys;
    }

    public InstantiationMode GetPoolInstantiationMode(string poolKey)
    {
        return configDictionary.ContainsKey(poolKey) ? configDictionary[poolKey].instantiationMode : InstantiationMode.Sequential;
    }

    #endregion
}