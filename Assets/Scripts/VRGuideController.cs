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
    public float waitDistance = 5.0f;
    public float continueDistance = 2.0f;
    public float wanderRadius = 10.0f;
    public float rotationSpeed = 5.0f;
    
    [Header("Two-Phase Navigation")]
    public float reachPlayerDistance = 1.8f;
    private Vector3 finalDestination;
    private bool isGoingToPlayer = false;
    private bool hasReachedPlayer = false;
    private bool isWaitingNearPlayer = false;

    private bool isGuideMode = false;
    private float moveStartTime = -99f;
    public bool isEating = false;
    
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
        if (agent == null || !agent.gameObject.activeInHierarchy) return;

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
                targetVisual.position = finalDestination + Vector3.up * 1.87f;
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
            Debug.LogWarning("Current Agent invalid; searching for an active Agent...");
            var allAgents = FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);
            foreach (var a in allAgents)
            {
                if (a.gameObject.activeInHierarchy)
                {
                    agent = a;
                    break;
                }
            }
        }

        if (agent == null)
        {
            Debug.LogError("No active Agent found in the scene!");
            return;
        }

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

        // Step 4: Position safety fix
        if (!agent.isOnNavMesh)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(agent.transform.position, out hit, 2.0f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);

            }
        }

        // Step 5: Start moving
        isGuideMode = true;
        agent.isStopped = false;
        agent.updateRotation = true;
        moveStartTime = Time.time;
        lastPathUpdateTime = Time.time;
        stuckCheckTime = Time.time;

        // Step 6: Generate random destination on the existing NavMesh
        Vector3 foundDestination = Vector3.zero;
        bool foundValid = false;

        // Try to pick a random point on the actual NavMesh triangulation
        NavMeshTriangulation navData = NavMesh.CalculateTriangulation();
        if (navData.indices != null && navData.indices.Length >= 3)
        {
            // Attempt several random triangles to find a reachable spot
            for (int attempt = 0; attempt < 30 && !foundValid; attempt++)
            {
                // Pick a random triangle
                int triIndex = Random.Range(0, navData.indices.Length / 3) * 3;
                Vector3 v0 = navData.vertices[navData.indices[triIndex]];
                Vector3 v1 = navData.vertices[navData.indices[triIndex + 1]];
                Vector3 v2 = navData.vertices[navData.indices[triIndex + 2]];

                // Random point inside the triangle (barycentric coords)
                float r1 = Random.value;
                float r2 = Random.value;
                if (r1 + r2 > 1f) { r1 = 1f - r1; r2 = 1f - r2; }
                Vector3 randomPoint = v0 + r1 * (v1 - v0) + r2 * (v2 - v0);

                // Snap to NavMesh surface to be safe
                NavMeshHit snapHit;
                if (NavMesh.SamplePosition(randomPoint, out snapHit, 1.0f, NavMesh.AllAreas))
                {
                    // Verify the agent can actually path there
                    NavMeshPath testPath = new NavMeshPath();
                    if (agent.CalculatePath(snapHit.position, testPath) &&
                        testPath.status == NavMeshPathStatus.PathComplete)
                    {
                        foundDestination = snapHit.position;
                        foundValid = true;
                    }
                }
            }
        }

        // Fallback: if no NavMesh triangulation or all attempts failed,
        // use the old random sphere approach near the player
        if (!foundValid)
        {
            Vector3 center = (playerHead != null) ? playerHead.position : agent.transform.position;
            Vector3 randomPos = Random.insideUnitSphere * wanderRadius;
            randomPos.y = 0f;
            randomPos += center;
            NavMeshHit destinationHit;

            if (NavMesh.SamplePosition(randomPos, out destinationHit, wanderRadius, NavMesh.AllAreas))
            {
                foundDestination = destinationHit.position;
                foundValid = true;
            }
        }

        if (foundValid)
        {
            finalDestination = foundDestination;

            
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
            
            // Show target visual at the goal immediately so the player can see where it is
            if (targetVisual != null)
            {
                targetVisual.position = finalDestination + Vector3.up * 1.87f;
                targetVisual.gameObject.SetActive(true);
            }
        }
        else
        {
            Debug.LogError("Failed to find valid destination on NavMesh!");
        }
    }
}