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
        
        UpdateBoundaries();
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

    // --- AR Boundary Logic ---

    [Header("AR Boundaries")]
    public bool enableBoundaries = true;
    public Vector2 boundarySize = new Vector2(10f, 10f); // 10x10 meter box
    public float boundaryHeight = 3.0f;
    public bool visibleBoundaries = true; // User requested visible first

    private GameObject boundaryRoot;

    void CreateBoundaries()
    {
        if (boundaryRoot != null) return; // Already created

        boundaryRoot = new GameObject("BoundaryWalls");
        boundaryRoot.transform.SetParent(transform, false); // Local position
        
        // Material for walls
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (!shader) shader = Shader.Find("Standard");
        Material wallMat = new Material(shader);
        wallMat.color = new Color(1f, 0f, 0f, 0.3f); // Red

        // Create 4 walls
        float w = boundarySize.x;
        float d = boundarySize.y;
        float h = boundaryHeight;
        float t = 0.2f; // Thickness

        CreateWall("Wall_N", new Vector3(0, h/2, d/2), new Vector3(w, h, t), wallMat);
        CreateWall("Wall_S", new Vector3(0, h/2, -d/2), new Vector3(w, h, t), wallMat);
        CreateWall("Wall_E", new Vector3(w/2, h/2, 0), new Vector3(t, h, d), wallMat);
        CreateWall("Wall_W", new Vector3(-w/2, h/2, 0), new Vector3(t, h, d), wallMat);
        
        Debug.Log($"[ARFloor] Created boundaries: {w}x{d}m");
    }

    void CreateWall(string name, Vector3 localPos, Vector3 size, Material mat)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(boundaryRoot.transform, false);
        wall.transform.localPosition = localPos;
        wall.transform.localScale = size;
        
        // Add Carving Obstacle
        var obs = wall.AddComponent<NavMeshObstacle>();
        obs.carving = true;
        obs.carvingMoveThreshold = 0.1f;
        obs.shape = NavMeshObstacleShape.Box;
        obs.size = Vector3.one; // Primitive cube is 1x1x1, scaled by transform

        // Visibility
        var renderer = wall.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = mat;
            renderer.enabled = visibleBoundaries;
        }
    }

    void UpdateBoundaries()
    {
        if (!enableBoundaries) 
        {
            if (boundaryRoot != null) Destroy(boundaryRoot);
            return;
        }

        if (boundaryRoot == null)
        {
            CreateBoundaries();
        }
        else
        {
            // Update visibility at runtime
            var renderers = boundaryRoot.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers) r.enabled = visibleBoundaries;
        }
    }
}
