using UnityEngine;
using UnityEngine.AI;

public class VRGuideController : MonoBehaviour
{
    [Header("Components")]
    public NavMeshAgent agent;
    public Animator animator;

    [Header("Global components")]
    public Transform playerHead;  
    public Transform targetVisual; 

    [Header("Parameters")]
    public float waitDistance = 3.0f;
    public float continueDistance = 2.0f;
    public float wanderRadius = 10.0f;
    public float rotationSpeed = 5.0f;

    [Header("Smart Spawn")]
    [Tooltip("Minimum distance from avatar for random destination")]
    public float minSpawnDistance = 2.0f;
    [Tooltip("Max attempts to find a valid position")]
    public int maxSpawnAttempts = 10;
    [Tooltip("Radius to check for colliders at spawn point (avoid spawning inside objects)")]
    public float spawnCollisionCheckRadius = 0.5f;
    [Tooltip("Layers to check for obstacles when spawning (default: everything)")]
    public LayerMask obstacleLayerMask = ~0;
    
    [Header("Debug")]
    public bool showDebugVisuals = true;

    private void SpawnDebugMarker(Vector3 pos, Color color)
    {
        if (!showDebugVisuals) return;
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.transform.position = pos;
        marker.transform.localScale = Vector3.one * 0.2f;
        marker.GetComponent<Collider>().enabled = false; // Don't block NavMesh!
        var renderer = marker.GetComponent<Renderer>();
        renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit")); // Or Standard
        renderer.material.color = color;
        Destroy(marker, 5.0f); // Auto-cleanup
    }

    public float reachPlayerDistance = 1.8f;
    private Vector3 finalDestination;
    private bool isGoingToPlayer = false;
    private bool hasReachedPlayer = false;
    private bool isWaitingNearPlayer = false;

    private bool isGuideMode = false;
    private float moveStartTime = -99f;
    public bool isEating = false;
    
    // 🆕 NEW: Anti-stuck system
    private float lastPathUpdateTime = 0f;
    private Vector3 lastPosition;
    private float stuckCheckTime = 0f;
    private const float PATH_UPDATE_INTERVAL = 0.5f; // Update path every 0.5 seconds
    private const float STUCK_THRESHOLD = 0.1f; // Consider stuck if speed < 0.1 for 1 second
    private const float STUCK_CHECK_DURATION = 1.0f;

    void Start()
    {
        lastPosition = transform.position;
    }

    void Update()
    {
        // Safety check
        if (agent == null || !agent.gameObject.activeInHierarchy || !agent.isOnNavMesh) return;

        // 1. Data gathering
        float speed = agent.velocity.magnitude;
        float distToTarget = (agent.hasPath && !agent.pathPending) ? agent.remainingDistance : 9999f;
        float distToPlayer = (playerHead != null) ? Vector3.Distance(agent.transform.position, playerHead.position) : 0f;

        // 2. Pass parameters to Animator
        if (animator != null)
        {
            animator.SetFloat("Speed", speed);
            animator.SetFloat("DistToTarget", distToTarget);
            animator.SetBool("IsGuideMode", isEating ? false : isGuideMode);
            animator.SetBool("IsEating", isEating);
        }

        // 3. Intercept logic: if eating, skip
        if (isEating) return;

        // 4. CHECK IF AGENT IS STUCK (moving but velocity ≈ 0)
        bool isStuck = DetectAndHandleStuck(speed);

        // 5. TWO-PHASE LOGIC: Phase 1 - Going to player
        if (isGoingToPlayer && !hasReachedPlayer)
        {
            HandlePhaseOneGoingToPlayer(distToPlayer, isStuck);
            return; // Skip the rest of Update while in phase 1
        }

        // 6. Original guide logic (Phase 2 - guiding to destination)
        if (isGuideMode && agent.hasPath)
        {
            if (distToTarget > agent.stoppingDistance + 0.1f)
            {
                // Start protection
                bool isStarting = Time.time < moveStartTime + 0.5f;
                if (distToTarget < 1.0f) isStarting = false;

                // Stop logic: player too far
                if (distToPlayer > waitDistance && !isStarting)
                {
                    agent.isStopped = true;
                    RotateTowardsUser();
                }
                // Walking logic: player is near
                else if (distToPlayer < continueDistance || isStarting)
                {
                    agent.isStopped = false;
                    agent.updateRotation = true;
                    
                    // If stuck in Phase 2, recalculate path
                    if (isStuck)
                    {
                        agent.SetDestination(finalDestination);
                    }
                }
            }
        }

        // 7. Arrival cleanup
        if (agent.hasPath && distToTarget <= agent.stoppingDistance + 0.1f)
        {
            agent.ResetPath();
            isGuideMode = false;
            isGoingToPlayer = false;
            hasReachedPlayer = false;
            isWaitingNearPlayer = false;
            
            if (targetVisual != null) targetVisual.gameObject.SetActive(false);
        }
    }

    private bool DetectAndHandleStuck(float currentSpeed)
    {
        // Only check if agent should be moving
        if (agent.isStopped || !agent.hasPath)
        {
            stuckCheckTime = Time.time; // Reset timer
            return false;
        }

        // Check if moving very slowly
        if (currentSpeed < STUCK_THRESHOLD)
        {
            // Agent is stuck - increment timer
            if (Time.time - stuckCheckTime >= STUCK_CHECK_DURATION)
            {
                stuckCheckTime = Time.time; // Reset timer to avoid spamming
                return true;
            }
        }
        else
        {
            // Agent is moving fine - reset timer
            stuckCheckTime = Time.time;
        }

        return false;
    }

    private void HandlePhaseOneGoingToPlayer(float distToPlayer, bool isStuck)
    {
        // Hysteresis thresholds
        float stopDistance = reachPlayerDistance + 0.2f;
        float resumeDistance = reachPlayerDistance + 0.7f;
        
        // Check if we reached the player (transition to Phase 2)
        if (distToPlayer <= reachPlayerDistance)
        {
            hasReachedPlayer = true;
            isGoingToPlayer = false;
            isWaitingNearPlayer = false;
            
            // Force clean path reset
            agent.ResetPath();
            agent.isStopped = true;
            
            // Set new destination to final target
            agent.isStopped = false;
            agent.SetDestination(finalDestination);
            
            if (targetVisual != null)
            {
                targetVisual.position = finalDestination + Vector3.up * 0.05f;
                targetVisual.gameObject.SetActive(true);
            }
            return;
        }

        bool hasValidPath = agent.hasPath && 
                           !agent.pathPending && 
                           agent.pathStatus == NavMeshPathStatus.PathComplete;
        
        bool shouldUpdatePath = false;
        
        // Determine if we need to update the path
        if (!hasValidPath)
        {
            shouldUpdatePath = true;
        }
        else if (isStuck)
        {
            shouldUpdatePath = true;
        }
        else if (Time.time - lastPathUpdateTime > PATH_UPDATE_INTERVAL)
        {
            // Periodic update to handle moving player
            shouldUpdatePath = true;
        }

        // WAITING NEAR PLAYER logic
        if (isWaitingNearPlayer)
        {
            if (distToPlayer > resumeDistance)
            {
                // Player moved far enough - resume following
                isWaitingNearPlayer = false;
                shouldUpdatePath = true;
            }
            else if (shouldUpdatePath)
            {
                // Update path even while waiting (player might have moved slightly)
            }
        }
        // ACTIVE PURSUIT logic
        else
        {
            if (distToPlayer <= stopDistance)
            {
                // Got close - stop and wait
                isWaitingNearPlayer = true;
                agent.ResetPath();
                agent.isStopped = true;
                return; // Don't update path, just wait
            }
        }

        // UPDATE PATH TO PLAYER (if needed)
        if (shouldUpdatePath && playerHead != null)
        {
            // Only update if player moved significantly OR path is invalid OR stuck
            float distToCurrentDest = agent.hasPath ? 
                Vector3.Distance(agent.destination, playerHead.position) : 999f;
            
            if (!hasValidPath || isStuck || distToCurrentDest > 0.3f)
            {
                agent.SetDestination(playerHead.position);
                agent.isStopped = false;
                agent.updateRotation = true;
                lastPathUpdateTime = Time.time;
                
            }
        }
    }

    void RotateTowardsUser()
    {
        if (playerHead == null || agent == null) return;
        agent.updateRotation = false;
        Vector3 direction = playerHead.position - agent.transform.position;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            agent.transform.rotation = Quaternion.Slerp(
                agent.transform.rotation, 
                Quaternion.LookRotation(direction), 
                Time.deltaTime * rotationSpeed
            );
        }
    }

    public void SetRandomDestination()
    {
        Debug.Log("========== RANDOM DESTINATION CLICKED ==========");
        
        // Step 1: Ensure the Agent is alive
        if (agent == null || !agent.gameObject.activeInHierarchy)
        {
            Debug.LogWarning("[VRGuide] Current Agent invalid; searching for an active Agent...");
            var allAgents = FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);
            foreach (var a in allAgents)
            {
                if (a.gameObject.activeInHierarchy)
                {
                    agent = a;
                    Debug.Log($"[VRGuide] Found active agent: '{a.gameObject.name}' at {a.transform.position}");
                    break;
                }
            }
        }

        if (agent == null)
        {
            Debug.LogError("[VRGuide] No active Agent found in the scene!");
            return;
        }

        Debug.Log($"[VRGuide] Using agent '{agent.gameObject.name}' at pos={agent.transform.position}, " +
                  $"isOnNavMesh={agent.isOnNavMesh}, enabled={agent.enabled}");

        // Make sure the agent component itself is enabled
        if (!agent.enabled) agent.enabled = true;

        // Step 2: Force-refresh Animator 
        animator = agent.GetComponentInChildren<Animator>();

        // Step 3: Cleanup the scene 
        var agents = FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);
        foreach (var a in agents)
        {
            if (a != agent && a.gameObject.activeInHierarchy)
            {
                a.gameObject.SetActive(false);
            }
        }

        // Step 4: Position safety fix — ensure agent is on NavMesh
        if (!agent.isOnNavMesh)
        {
            Debug.Log("[VRGuide] Agent NOT on NavMesh, attempting to fix...");

            // In AR the NavMesh may need a rebake first
            var arFloor = FindFirstObjectByType<ARFloorAlign>();
            if (arFloor != null)
            {
                Debug.Log("[VRGuide] AR mode detected, forcing NavMesh rebake...");
                arFloor.ForceRebake();
            }

            // Try to find the nearest NavMesh point (large radius to handle AR floor offset)
            NavMeshHit hit;
            if (NavMesh.SamplePosition(agent.transform.position, out hit, 50.0f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
                Debug.Log($"[VRGuide] Warped agent to NavMesh at {hit.position} " +
                          $"(was at {agent.transform.position})");
            }
            else
            {
                // Last resort: try player position as the search origin
                if (playerHead != null)
                {
                    Debug.Log($"[VRGuide] Trying from playerHead position: {playerHead.position}");
                    if (NavMesh.SamplePosition(playerHead.position, out hit, 50.0f, NavMesh.AllAreas))
                    {
                        agent.Warp(hit.position);
                        Debug.Log($"[VRGuide] Warped agent to NavMesh near player at {hit.position}");
                    }
                    else
                    {
                        Debug.LogError("[VRGuide] Cannot find any NavMesh surface anywhere!");
                        return;
                    }
                }
                else
                {
                    Debug.LogError("[VRGuide] Cannot find any NavMesh surface and no playerHead!");
                    return;
                }
            }
        }

        // Step 5: Start moving
        isGuideMode = true;
        agent.isStopped = false;
        agent.updateRotation = true;
        moveStartTime = Time.time;
        lastPathUpdateTime = Time.time;
        stuckCheckTime = Time.time;

        // Step 6: Generate a smart random destination (avoids objects, validates path)
        Vector3? validDest = FindValidDestination();

        if (validDest.HasValue)
        {
            finalDestination = validDest.Value;

            // PHASE 1: First go to player
            if (playerHead != null)
            {
                isGoingToPlayer = true;
                hasReachedPlayer = false;
                isWaitingNearPlayer = false;
                agent.SetDestination(playerHead.position);
            }
            else
            {
                // Fallback: if no playerHead, go directly to destination
                Debug.LogWarning("⚠️ No playerHead found, going directly to destination");
                agent.SetDestination(finalDestination);
                hasReachedPlayer = true; // Skip phase 1
            }

            // Hide target visual until phase 2
            if (targetVisual != null)
            {
                targetVisual.gameObject.SetActive(false);
            }
        }
        else
        {
            Debug.LogError("[VRGuide] Failed to find ANY valid destination on NavMesh!");
        }
    }

    /// <summary>
    /// Tries multiple random positions to find one that is:
    ///   1. On the NavMesh
    ///   2. Reachable via a complete path
    ///   3. Not overlapping any physics colliders (walls/furniture/AR objects)
    ///   4. At least minSpawnDistance away from the avatar
    /// Falls back to a simple NavMesh sample if all attempts fail.
    /// </summary>
    private Vector3? FindValidDestination()
    {
        if (agent == null) return null;

        Debug.Log($"[VRGuide] FindValidDestination: agent pos={agent.transform.position}, " +
                  $"isOnNavMesh={agent.isOnNavMesh}, wanderRadius={wanderRadius}");

        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            // Random direction on the XZ plane (flat circle, avoids vertical bias)
            Vector2 rndCircle = Random.insideUnitCircle * wanderRadius;
            Vector3 randomPos = agent.transform.position + new Vector3(rndCircle.x, 0f, rndCircle.y);

            // Snap to nearest NavMesh surface (small radius — stay near the random point)
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(randomPos, out hit, 2.0f, NavMesh.AllAreas))
            {
                Debug.Log($"[VRGuide] Attempt {attempt + 1}: no NavMesh near {randomPos}");
                SpawnDebugMarker(randomPos, Color.red);
                continue;
            }

            Vector3 candidate = hit.position;

            // --- Check 1: must be within wanderRadius ---
            float dist = Vector3.Distance(agent.transform.position, candidate);
            if (dist > wanderRadius)
            {
                Debug.Log($"[VRGuide] Attempt {attempt + 1}: too far ({dist:F1}m > {wanderRadius}m), skipping");
                SpawnDebugMarker(candidate, Color.red);
                continue;
            }

            // --- Check 2: minimum distance ---
            if (dist < minSpawnDistance)
            {
                Debug.Log($"[VRGuide] Attempt {attempt + 1}: too close ({dist:F1}m), skipping");
                SpawnDebugMarker(candidate, Color.red);
                continue;
            }

            // --- Check 2: physics overlap (avoid spawning inside objects) ---
            // Check slightly above ground level to catch furniture/walls
            Collider[] overlaps = Physics.OverlapSphere(
                candidate + Vector3.up * 0.5f,
                spawnCollisionCheckRadius,
                obstacleLayerMask,
                QueryTriggerInteraction.Ignore
            );

            // Filter out floor/ground colliders — only reject actual obstacles
            bool hasRealObstacle = false;
            foreach (var col in overlaps)
            {
                // Skip the agent itself
                if (col.gameObject == agent.gameObject) continue;
                if (col.GetComponent<NavMeshAgent>() != null) continue;

                // Skip floor/ground/plane objects by name (case-insensitive)
                string objName = col.gameObject.name.ToLower();
                if (objName.Contains("floor") || objName.Contains("ground") ||
                    objName.Contains("plane") || objName.Contains("navmesh") ||
                    objName.Contains("surface"))
                    continue;

                // Skip parent hierarchy floor objects (e.g. Plane under ARFloor)
                bool isFloorChild = false;
                Transform parent = col.transform.parent;
                while (parent != null)
                {
                    string parentName = parent.name.ToLower();
                    if (parentName.Contains("floor") || parentName.Contains("ground"))
                    {
                        isFloorChild = true;
                        break;
                    }
                    parent = parent.parent;
                }
                if (isFloorChild) continue;

                // Skip very flat colliders (likely ground/floor)
                if (col.bounds.size.y < 0.15f) continue;

                hasRealObstacle = true;
                Debug.Log($"[VRGuide] Attempt {attempt + 1}: blocked by '{col.gameObject.name}' " +
                          $"(bounds: {col.bounds.size}), skipping");
                SpawnDebugMarker(candidate, Color.red);
                break;
            }
            if (hasRealObstacle) continue;

            // --- Check 3: full path validation (can the agent actually walk there?) ---
            NavMeshPath testPath = new NavMeshPath();
            if (!agent.CalculatePath(candidate, testPath) ||
                testPath.status != NavMeshPathStatus.PathComplete)
            {
                Debug.Log($"[VRGuide] Attempt {attempt + 1}: path incomplete to {candidate}");
                SpawnDebugMarker(candidate, Color.red);
                continue;
            }

            Debug.Log($"[VRGuide] ✅ Valid destination on attempt {attempt + 1}: {candidate}");
            SpawnDebugMarker(candidate, Color.green);
            return candidate;
        }

        // --- Fallback: relax ALL checks, just use a NavMesh sample ---
        Debug.LogWarning("[VRGuide] Could not find ideal position, using fallback (NavMesh sample only)");
        Vector2 fallbackCircle = Random.insideUnitCircle * wanderRadius;
        Vector3 fallbackPos = agent.transform.position + new Vector3(fallbackCircle.x, 0f, fallbackCircle.y);
        NavMeshHit fallbackHit;
        if (NavMesh.SamplePosition(fallbackPos, out fallbackHit, wanderRadius, NavMesh.AllAreas))
        {
            Debug.Log($"[VRGuide] Fallback destination: {fallbackHit.position}");
            SpawnDebugMarker(fallbackHit.position, Color.yellow); // Yellow for fallback
            return fallbackHit.position;
        }

        Debug.LogError("[VRGuide] Fallback also failed — no NavMesh found at all!");
        return null;
    }
}
