using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

[RequireComponent(typeof(BoxCollider))]
public class ARFloorAlign : MonoBehaviour
{
    [Header("Assign your XR Rig or Player Root")]
    public Transform player;

    [Header("Floor Y position relative to world")]
    public float floorHeight = 0f;

    [Header("NavMesh Rebaking")]
    [Tooltip("How far the floor must move before rebaking the NavMesh")]
    public float rebakeThreshold = 1.0f;
    [Tooltip("Minimum seconds between NavMesh rebakes")]
    public float rebakeCooldown = 0.5f;

    private NavMeshSurface navMeshSurface;
    private Vector3 lastBakePosition;
    private float lastBakeTime = -999f;
    private bool hasInitialBake = false;

    /// <summary>
    /// Ensures navMeshSurface is found. Can be called multiple times safely.
    /// Uses Awake-like lazy init because this object may start inactive
    /// (under ARRoot) so Start()/Awake() don't run until activation.
    /// </summary>
    void EnsureInitialized()
    {
        if (navMeshSurface == null)
        {
            navMeshSurface = GetComponentInChildren<NavMeshSurface>();
            if (navMeshSurface == null)
            {
                Debug.LogWarning("[ARFloor] No NavMeshSurface found in children!");
            }
            else
            {
                Debug.Log($"[ARFloor] Found NavMeshSurface on '{navMeshSurface.gameObject.name}'");
            }
            lastBakePosition = transform.position;
        }

        // AUTO-REPAIR: Ensure ARObstacleDetector exists on ARRoot (parent)
        if (transform.parent != null)
        {
            var obsDetector = transform.parent.GetComponent<ARObstacleDetector>();
            if (obsDetector == null)
            {
                Debug.LogWarning("[ARFloor] ARObstacleDetector missing from ARRoot! Adding it automatically.");
                transform.parent.gameObject.AddComponent<ARObstacleDetector>();
            }
        }

        // AUTO-DEBUG: Add NavMeshDebug component for runtime diagnostics
        if (GetComponent<NavMeshDebug>() == null)
        {
            var debug = gameObject.AddComponent<NavMeshDebug>();
            debug.player = player;
            // Try to find agent to assign
            var agent = FindFirstObjectByType<NavMeshAgent>();
            if (agent != null) debug.agent = agent;
            Debug.Log("[ARFloor] Added NavMeshDebug component for diagnostics.");
        }
    }

    void OnEnable()
    {
        // Force initialization + rebake when the AR floor becomes active
        EnsureInitialized();
        hasInitialBake = false;
        Debug.Log("[ARFloor] OnEnable — will rebake on next LateUpdate");
    }

    void LateUpdate()
    {
        if (player == null) return;

        // Align floor horizontally with player
        Vector3 pos = transform.position;
        pos.x = player.position.x;
        pos.z = player.position.z;
        pos.y = floorHeight;
        transform.position = pos;

        // Rebake NavMesh if the floor has moved significantly
        EnsureInitialized();
        if (navMeshSurface != null)
        {
            float movedDistance = Vector3.Distance(transform.position, lastBakePosition);

            if (!hasInitialBake || movedDistance > rebakeThreshold)
            {
                if (Time.time - lastBakeTime > rebakeCooldown)
                {
                    RebakeNavMesh();
                }
            }
        }
    }

    void RebakeNavMesh()
    {
        if (navMeshSurface == null)
        {
            Debug.LogError("[ARFloor] RebakeNavMesh called but navMeshSurface is null!");
            return;
        }

        navMeshSurface.BuildNavMesh();
        lastBakePosition = transform.position;
        lastBakeTime = Time.time;
        hasInitialBake = true;

        Debug.Log($"[ARFloor] ✅ NavMesh rebaked at position {transform.position}");

        // After rebaking, warp any active NavMeshAgent onto the new NavMesh
        WarpAgentsToNavMesh();
    }

    void WarpAgentsToNavMesh()
    {
        var agents = FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);
        foreach (var agent in agents)
        {
            if (!agent.gameObject.activeInHierarchy) continue;
            // Retry warp even if isOnNavMesh is true, in case the NavMesh moved under feet
            // But usually NavMeshAgent handles small moves. If large move, it might be detached.
            
            NavMeshHit hit;
            if (NavMesh.SamplePosition(agent.transform.position, out hit, 50f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
                Debug.Log($"[ARFloor] Warped agent '{agent.name}' to NavMesh at {hit.position}");
            }
            else
            {
                Debug.LogWarning($"[ARFloor] Could not warp agent '{agent.name}' — no NavMesh near {agent.transform.position}");
            }
        }
    }

    /// <summary>
    /// Force an immediate NavMesh rebake. Call from SceneManager or VRGuideController.
    /// </summary>
    public void ForceRebake()
    {
        Debug.Log("[ARFloor] ForceRebake called");

        // Initialize if needed (this object may have just been activated)
        EnsureInitialized();

        // Update position first
        if (player != null)
        {
            Vector3 pos = transform.position;
            pos.x = player.position.x;
            pos.z = player.position.z;
            pos.y = floorHeight;
            transform.position = pos;
        }

        // Bake immediately
        RebakeNavMesh();
    }
}
