using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

public class CakePathFollower : MonoBehaviour
{
    public NavMeshAgent agent;
    public GameObject cakePrefab;
    public string eatAnimationTag = "Eat";
    public Animator animator;
    public float cakeInterval = 2f;

    [Header("Cake Height")]
    [Tooltip("How high above the NavMesh ground the cakes float")]
    public float cakeHeightOffset = 0.4f;

    [Header("Stop Distance")]
    public float stopDistance = 1.2f;

    private List<GameObject> spawnedCakes = new List<GameObject>();

    public void MoveAndSpawnCakes(Vector3 targetPosition)
    {
        StopAllCoroutines();
        ClearCakes();

        // Always re-grab the active avatar's agent and animator
        // (in case avatar was switched since this component was set up)
        RefreshReferences();

        Debug.Log($"[CakePath] Avatar: {gameObject.name}");
        Debug.Log($"[CakePath] Agent: {(agent != null ? agent.name : "NULL")}");
        Debug.Log($"[CakePath] Animator: {(animator != null ? animator.name : "NULL")}");
        Debug.Log($"[CakePath] Agent isOnNavMesh: {(agent != null ? agent.isOnNavMesh.ToString() : "N/A")}");
        Debug.Log($"[CakePath] Target: {targetPosition}");

        if (agent == null)
        {
            Debug.LogError("[CakePath] No NavMeshAgent found! Aborting.");
            return;
        }

        // Force agent onto NavMesh if needed
        if (!agent.isOnNavMesh)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(agent.transform.position, out hit, 10.0f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
                Debug.Log($"[CakePath] WARPED agent to: {hit.position}");
            }
            else
            {
                Debug.LogError("[CakePath] CANNOT find NavMesh! Aborting.");
                return;
            }
        }

        NavMeshPath path = new NavMeshPath();
        if (agent.CalculatePath(targetPosition, path))
        {
            Debug.Log($"[CakePath] Path calculated! Corners: {path.corners.Length}");
            SpawnCakesAlongPath(path.corners);
            StartCoroutine(FollowPathAndEat());
        }
        else
        {
            Debug.LogError("[CakePath] CalculatePath FAILED!");
        }
    }

    /// <summary>
    /// Refreshes agent and animator references to match the currently active avatar.
    /// </summary>
    void RefreshReferences()
    {
        // If current agent is null or inactive, find an active one
        if (agent == null || !agent.gameObject.activeInHierarchy)
        {
            var agents = FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);
            foreach (var a in agents)
            {
                if (a.gameObject.activeInHierarchy)
                {
                    agent = a;
                    Debug.Log($"[CakePath] Re-bound agent to: {a.name}");
                    break;
                }
            }
        }

        // Always refresh animator from the agent's hierarchy
        if (agent != null)
        {
            animator = agent.GetComponentInChildren<Animator>();
            if (animator != null)
                Debug.Log($"[CakePath] Animator bound: {animator.name}");
            else
                Debug.LogWarning("[CakePath] No Animator found on agent!");
        }
    }

    void SpawnCakesAlongPath(Vector3[] corners)
    {
        if (corners.Length < 2) return;

        // Use the NavMesh Y level + offset so cakes float visibly above ground
        float groundY = corners[0].y + cakeHeightOffset;

        // generate cakes along the path
        for (int i = 0; i < corners.Length - 1; i++)
        {
            Vector3 start = corners[i];
            Vector3 end = corners[i + 1];
            float segmentLength = Vector3.Distance(start, end);

            for (float d = 0; d < segmentLength; d += cakeInterval)
            {
                Vector3 spawnPos = Vector3.Lerp(start, end, d / segmentLength);
                spawnPos.y = groundY;
                GameObject cake = Instantiate(cakePrefab, spawnPos, Quaternion.identity);
                FreezeInPlace(cake);
                spawnedCakes.Add(cake);
            }
        }

        // add the final cake at the end position
        Vector3 finalPos = corners[corners.Length - 1];
        finalPos.y = groundY;
        GameObject finalCake = Instantiate(cakePrefab, finalPos, Quaternion.identity);
        FreezeInPlace(finalCake);
        spawnedCakes.Add(finalCake);
    }

    IEnumerator FollowPathAndEat()
    {
        VRGuideController guideController = FindFirstObjectByType<VRGuideController>();
        if (guideController != null) guideController.isEating = true;

        // Re-grab animator in case it changed
        if (agent != null)
            animator = agent.GetComponentInChildren<Animator>();

        float originalStoppingDist = agent.stoppingDistance;

        // loop through each cake
        for (int i = 0; i < spawnedCakes.Count; i++)
        {
            GameObject currentCake = spawnedCakes[i];
            if (currentCake == null) continue;

            Vector3 cakePos = currentCake.transform.position;

            // stopping distance
            agent.stoppingDistance = stopDistance;
            agent.SetDestination(cakePos);
            agent.isStopped = false;

            // wait until reach the cake — update Speed for walk animation
            while (agent.pathPending || agent.remainingDistance > agent.stoppingDistance)
            {
                // Drive the walking animation
                if (animator != null)
                    animator.SetFloat("Speed", agent.velocity.magnitude);

                yield return null;
            }

            // stop and ready to eat
            agent.isStopped = true;
            agent.velocity = Vector3.zero;

            // Stop walk animation
            if (animator != null)
                animator.SetFloat("Speed", 0f);

            transform.LookAt(new Vector3(cakePos.x, transform.position.y, cakePos.z));

            // Play eat animation — use CrossFade to force it regardless of current state
            if (animator != null)
                animator.CrossFade("Eat", 0.25f);

            // time to eat
            yield return new WaitForSeconds(3.0f);

            Destroy(currentCake);

            // check if it's the last cake
            if (i == spawnedCakes.Count - 1)
            {
                Debug.Log("Finished the last cake, Arrive!");

                if (animator != null)
                {
                    animator.ResetTrigger(eatAnimationTag);
                    animator.CrossFade("Arrive", 0.1f);
                }

                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                agent.ResetPath();

                if (guideController != null) guideController.isEating = false;
                agent.stoppingDistance = originalStoppingDist;
                yield break;
            }
        }

        // Safety: if we exit the loop normally, also reset eating state
        if (guideController != null)
        {
            var gc = FindFirstObjectByType<VRGuideController>();
            if (gc != null) gc.isEating = false;
        }
        agent.stoppingDistance = originalStoppingDist;
    }

    /// <summary>
    /// Disables gravity/physics on a cake so it floats in place.
    /// </summary>
    void FreezeInPlace(GameObject cake)
    {
        foreach (var rb in cake.GetComponentsInChildren<Rigidbody>(true))
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    void ClearCakes()
    {
        foreach (var cake in spawnedCakes) if (cake != null) Destroy(cake);
        spawnedCakes.Clear();
    }
}