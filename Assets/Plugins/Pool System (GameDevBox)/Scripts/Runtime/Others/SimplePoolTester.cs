// =======================================================
//  GameDevBox – YouTube
//  Author: Arian
//  Link: https://www.youtube.com/@GameDevBox
// =======================================================

using UnityEngine;

public class UltraSimpleShowcase : MonoBehaviour
{
    public string poolKey = "Cube01";
    public Transform spawnPoint;

    private void Update()
    {
        // 🎯 BASIC USAGE
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            // Spawn object
            GameObject obj = PoolSystemManager.Instance.Get(poolKey, spawnPoint != null ? spawnPoint.position : Vector3.zero, Quaternion.identity, Vector3.one);
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            // Release last spawned object
            PoolSystemManager.Instance.Release(poolKey, 1);
        }

        // 🗑️ BULK OPERATIONS
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            // Release all from this pool
            PoolSystemManager.Instance.ReleaseAll(poolKey);
        }

        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            // Release all from all pools
            PoolSystemManager.Instance.ReleaseAll();
        }

        // 📊 INFO & STATS
        if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            // Check if pool exists
            bool exists = PoolSystemManager.Instance.PoolExists(poolKey);
            Debug.Log($"Pool exists: {exists}");
        }

        if (Input.GetKeyDown(KeyCode.Alpha6))
        {
            // Get active count
            int active = PoolSystemManager.Instance.GetActiveCount(poolKey);
            Debug.Log($"Active objects: {active}");
        }

        // 🎛️ ADVANCED
        if (Input.GetKeyDown(KeyCode.Alpha7))
        {
            // Spawn with custom transform
            Vector3 pos = spawnPoint.position + Vector3.up * 2f;
            Quaternion rot = Quaternion.Euler(Random.Range(0, 90), Random.Range(0, 90), Random.Range(0, 90));
            Vector3 scale = Vector3.one * 2f;

            PoolSystemManager.Instance.Get(poolKey, pos, rot, scale);
            Debug.Log("Spawned with custom transform");
        }

        if (Input.GetKeyDown(KeyCode.Alpha8))
        {
            // Get as component
            Rigidbody rb = PoolSystemManager.Instance.Get<Rigidbody>(poolKey, spawnPoint != null ? spawnPoint.position : Vector3.zero);
            if (rb != null)
            {
                rb.AddForce(Vector3.up * 10f, ForceMode.Impulse);
                Debug.Log("Spawned with physics");
            }
        }
    }
}