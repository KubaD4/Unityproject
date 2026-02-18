using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

/// <summary>
/// Periodically scans for colliders created at runtime by Scene Understanding
/// (Effect Mesh, scene anchors, etc.) and adds carving NavMeshObstacle components
/// so the NavMeshAgent steers around real-world furniture/walls in AR.
///
/// Attach this to the ARRoot GameObject. It scans the entire scene for new colliders.
/// </summary>
public class ARObstacleDetector : MonoBehaviour
{
    [Header("Scan Settings")]
    [Tooltip("How often (seconds) to scan for new colliders")]
    public float scanInterval = 1.0f;

    [Tooltip("Enable carving so NavMesh is cut around obstacles in real-time")]
    public bool carve = true;

    [Tooltip("Extra margin around obstacles for safer navigation")]
    public float carvingMoveThreshold = 0.1f;

    [Header("Filtering")]
    [Tooltip("Skip colliders on GameObjects with these tags")]
    public string[] ignoreTags = { "Floor", "Ground", "Player" };

    [Tooltip("Skip colliders on GameObjects whose name contains any of these strings (case-insensitive)")]
    public string[] ignoreNameContains = { "floor", "ground", "plane", "navmesh" };

    [Tooltip("Minimum collider bounds volume to consider (filters out tiny fragments)")]
    public float minBoundsVolume = 0.01f;

    // Track which GameObjects we've already processed
    private HashSet<int> processedObjects = new HashSet<int>();
    private float lastScanTime = -999f;
    private int totalAdded = 0;

    void Update()
    {
        if (Time.time - lastScanTime < scanInterval) return;
        lastScanTime = Time.time;

        ScanForNewObstacles();
    }

    /// <summary>
    /// Finds all colliders in the scene that don't have a NavMeshObstacle yet
    /// and adds one. Filters out floors, agents, triggers, and tiny objects.
    /// </summary>
    void ScanForNewObstacles()
    {
        Collider[] allColliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);
        int addedThisScan = 0;

        foreach (Collider col in allColliders)
        {
            if (col == null || !col.gameObject.activeInHierarchy) continue;

            int instanceId = col.gameObject.GetInstanceID();

            // Already processed this object
            if (processedObjects.Contains(instanceId)) continue;

            // Mark as processed regardless of outcome (avoid re-checking)
            processedObjects.Add(instanceId);

            // --- Filtering ---

            // Skip triggers
            if (col.isTrigger) continue;

            // Skip objects that are NavMeshAgents
            if (col.GetComponent<NavMeshAgent>() != null) continue;

            // Skip objects that already have a NavMeshObstacle
            if (col.GetComponent<NavMeshObstacle>() != null) continue;

            // Skip by tag
            if (ShouldIgnoreByTag(col.gameObject)) continue;

            // Skip by name
            if (ShouldIgnoreByName(col.gameObject)) continue;

            // Skip very small colliders (debris, fragments)
            Bounds bounds = col.bounds;
            float volume = bounds.size.x * bounds.size.y * bounds.size.z;
            if (volume < minBoundsVolume) continue;

            // Skip objects that are clearly flat ground (much wider than tall)
            if (bounds.size.y < 0.05f && (bounds.size.x > 1f || bounds.size.z > 1f)) continue;

            // --- Add NavMeshObstacle ---
            NavMeshObstacle obstacle = col.gameObject.AddComponent<NavMeshObstacle>();
            obstacle.carving = carve;
            obstacle.carvingMoveThreshold = carvingMoveThreshold;

            // Size obstacle to match collider
            if (col is BoxCollider box)
            {
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.size = box.size;
                obstacle.center = box.center;
            }
            else if (col is SphereCollider sphere)
            {
                obstacle.shape = NavMeshObstacleShape.Capsule;
                obstacle.radius = sphere.radius;
                obstacle.center = sphere.center;
                obstacle.height = sphere.radius * 2f;
            }
            else if (col is CapsuleCollider capsule)
            {
                obstacle.shape = NavMeshObstacleShape.Capsule;
                obstacle.radius = capsule.radius;
                obstacle.height = capsule.height;
                obstacle.center = capsule.center;
            }
            else if (col is MeshCollider mesh)
            {
                // Use world-space bounds converted to local space for MeshColliders
                obstacle.shape = NavMeshObstacleShape.Box;
                Transform t = col.transform;
                Vector3 localSize = t.InverseTransformVector(bounds.size);
                localSize = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
                Vector3 localCenter = t.InverseTransformPoint(bounds.center);
                obstacle.size = localSize;
                obstacle.center = localCenter;
            }

            addedThisScan++;
            totalAdded++;
            Debug.Log($"[ARObstacle] Added NavMeshObstacle to '{col.gameObject.name}' " +
                      $"(bounds: {bounds.size}, total: {totalAdded})");
        }

        if (addedThisScan > 0)
        {
            Debug.Log($"[ARObstacle] Scan complete: added {addedThisScan} new obstacle(s), " +
                      $"{totalAdded} total");
        }
    }

    bool ShouldIgnoreByTag(GameObject go)
    {
        foreach (string tag in ignoreTags)
        {
            try
            {
                if (go.CompareTag(tag)) return true;
            }
            catch
            {
                // Tag doesn't exist — ignore
            }
        }
        return false;
    }

    bool ShouldIgnoreByName(GameObject go)
    {
        string lowerName = go.name.ToLower();

        // Check the object itself and all parents
        Transform current = go.transform;
        while (current != null)
        {
            string name = current.gameObject.name.ToLower();
            foreach (string ignore in ignoreNameContains)
            {
                if (name.Contains(ignore)) return true;
            }
            current = current.parent;
        }
        return false;
    }

    /// <summary>
    /// Call this to force an immediate re-scan (e.g. after toggling AR mode).
    /// </summary>
    public void ForceRescan()
    {
        processedObjects.Clear();
        totalAdded = 0;
        lastScanTime = -999f;
        Debug.Log("[ARObstacle] Force rescan triggered");
    }
}
