using Oculus.Interaction;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class DrawPathMovement : MonoBehaviour
{
    [Header("Core Settings")]
    public NavMeshAgent currentAgent; // avatar
    public Transform drawingHand;
    public GameObject linePrefab;

    [Header("Waypoint Following")]
    [Tooltip("How close the agent must get to a waypoint before moving to the next one.")]
    public float waypointReachThreshold = 0.4f;

    [Tooltip("Reduce drawn points to one every N metres to keep the path clean.")]
    public float waypointSpacing = 0.3f;

    [Tooltip("Max distance to search for a valid NavMesh position near each drawn point.")]
    public float navMeshSampleRadius = 2.0f;

    [Header("Obstacle Avoidance")]
    [Tooltip("Extra radius the agent keeps from NavMesh obstacles (added on top of the agent radius).")]
    public float obstacleAvoidanceMargin = 0.2f;

    [Tooltip("Quality of the built-in avoidance. Higher = more accurate but heavier.")]
    public ObstacleAvoidanceType avoidanceQuality = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

    // ── private state ──
    private LineRenderer currentLine;
    private List<Vector3> pathPoints = new List<Vector3>();   // raw drawn points
    private List<Vector3> waypoints  = new List<Vector3>();   // cleaned / NavMesh-sampled waypoints
    private int currentWaypointIndex = 0;
    private bool isDrawing  = false;
    private bool isFollowing = false;

    void Start()
    {
        // Apply avoidance settings once at start
        if (currentAgent != null)
        {
            currentAgent.obstacleAvoidanceType = avoidanceQuality;
        }
    }

    void Update()
    {
        if (drawingHand == null) return;

        // ── Drawing input ──
        if (OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger))
        {
            StartDrawing();
        }

        if (OVRInput.Get(OVRInput.Button.SecondaryIndexTrigger) && isDrawing)
        {
            UpdateDrawing();
        }

        if (OVRInput.GetUp(OVRInput.Button.SecondaryIndexTrigger) && isDrawing)
        {
            StopDrawingAndMove();
        }

        // ── Waypoint-following loop ──
        if (isFollowing)
        {
            FollowWaypoints();
        }
    }

    // ─────────────────────────────────────────────
    //  DRAWING
    // ─────────────────────────────────────────────

    void StartDrawing()
    {
        // Stop any in-progress following
        StopFollowing();

        isDrawing = true;
        pathPoints.Clear();
        waypoints.Clear();
        currentWaypointIndex = 0;

        // Create new line
        if (linePrefab != null)
        {
            GameObject lineObj = Instantiate(linePrefab);
            currentLine = lineObj.GetComponent<LineRenderer>();
            currentLine.positionCount = 0;
        }

        // Reset agent path
        if (IsValidAgent())
        {
            currentAgent.isStopped = true;
            currentAgent.ResetPath();
        }
    }

    void UpdateDrawing()
    {
        var interactor = drawingHand.GetComponentInParent<RayInteractor>();

        if (interactor != null && interactor.HasSelectedInteractable)
        {
            Vector3 point = interactor.End;
            point.y = 0.05f;

            if (pathPoints.Count == 0)
            {
                pathPoints.Add(point);
                UpdateLineRenderer();
                Debug.Log("First point added: " + point);
            }
            else if (Vector3.Distance(pathPoints[pathPoints.Count - 1], point) > 0.02f)
            {
                pathPoints.Add(point);
                UpdateLineRenderer();
                Debug.Log("Point added: " + point + " Count: " + pathPoints.Count);
            }
        }
    }

    void UpdateLineRenderer()
    {
        if (currentLine == null) return;
        currentLine.positionCount = pathPoints.Count;
        currentLine.SetPositions(pathPoints.ToArray());
    }

    // ─────────────────────────────────────────────
    //  PATH BUILDING  &  MOVEMENT
    // ─────────────────────────────────────────────

    void StopDrawingAndMove()
    {
        isDrawing = false;

        if (!IsValidAgent() || pathPoints.Count == 0) return;

        // 1. Simplify / resample the raw drawn points
        waypoints = ResamplePath(pathPoints, waypointSpacing);

        // 2. Snap every waypoint to the NavMesh so the agent can actually reach it
        waypoints = SnapToNavMesh(waypoints, navMeshSampleRadius);

        if (waypoints.Count == 0)
        {
            Debug.LogWarning("DrawPath: No valid NavMesh waypoints could be found from the drawn path.");
            return;
        }

        // 3. Optionally redraw the line along the cleaned waypoints
        RedrawLineOnWaypoints();

        // 4. Start following
        currentWaypointIndex = 0;
        isFollowing = true;
        currentAgent.isStopped = false;
        currentAgent.obstacleAvoidanceType = avoidanceQuality;
        currentAgent.SetDestination(waypoints[0]);
        Debug.Log("DrawPath: Following " + waypoints.Count + " waypoints with obstacle avoidance.");
    }

    // ─────────────────────────────────────────────
    //  WAYPOINT FOLLOWING  (runs every frame)
    // ─────────────────────────────────────────────

    void FollowWaypoints()
    {
        if (!IsValidAgent() || waypoints.Count == 0)
        {
            StopFollowing();
            return;
        }

        // Wait until the agent finishes computing its path
        if (currentAgent.pathPending) return;

        // Check if agent reached the current waypoint
        float dist = Vector3.Distance(currentAgent.transform.position, waypoints[currentWaypointIndex]);
        if (dist <= waypointReachThreshold)
        {
            currentWaypointIndex++;

            if (currentWaypointIndex >= waypoints.Count)
            {
                // All waypoints reached
                Debug.Log("DrawPath: Path complete.");
                StopFollowing();
                return;
            }

            // Navigate to next waypoint (NavMesh pathfinding avoids obstacles automatically)
            currentAgent.SetDestination(waypoints[currentWaypointIndex]);
        }

        // ── Stuck detection: if velocity is near zero for too long, skip to next reachable waypoint ──
        if (!currentAgent.pathPending && currentAgent.remainingDistance > waypointReachThreshold
            && currentAgent.velocity.sqrMagnitude < 0.01f)
        {
            // The agent might be stuck; try the next waypoint
            if (currentAgent.pathStatus == NavMeshPathStatus.PathPartial ||
                currentAgent.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                Debug.LogWarning("DrawPath: Waypoint " + currentWaypointIndex + " unreachable, skipping.");
                currentWaypointIndex++;
                if (currentWaypointIndex < waypoints.Count)
                    currentAgent.SetDestination(waypoints[currentWaypointIndex]);
                else
                    StopFollowing();
            }
        }
    }

    void StopFollowing()
    {
        isFollowing = false;
        if (IsValidAgent())
        {
            currentAgent.isStopped = true;
            currentAgent.ResetPath();
        }
    }

    // ─────────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────────

    /// <summary>Resample the raw drawn points so they are roughly <paramref name="spacing"/> apart.</summary>
    List<Vector3> ResamplePath(List<Vector3> raw, float spacing)
    {
        if (raw.Count == 0) return new List<Vector3>();

        List<Vector3> result = new List<Vector3> { raw[0] };
        float accumulated = 0f;

        for (int i = 1; i < raw.Count; i++)
        {
            accumulated += Vector3.Distance(raw[i - 1], raw[i]);
            if (accumulated >= spacing)
            {
                result.Add(raw[i]);
                accumulated = 0f;
            }
        }

        // Always include the last point
        if (Vector3.Distance(result[result.Count - 1], raw[raw.Count - 1]) > 0.05f)
            result.Add(raw[raw.Count - 1]);

        return result;
    }

    /// <summary>Project every point onto the nearest valid NavMesh position.</summary>
    List<Vector3> SnapToNavMesh(List<Vector3> points, float sampleRadius)
    {
        List<Vector3> valid = new List<Vector3>();
        NavMeshHit hit;

        foreach (Vector3 p in points)
        {
            if (NavMesh.SamplePosition(p, out hit, sampleRadius, NavMesh.AllAreas))
            {
                valid.Add(hit.position);
            }
            else
            {
                Debug.LogWarning("DrawPath: Point " + p + " is not near the NavMesh, skipping.");
            }
        }

        return valid;
    }

    /// <summary>Redraw the visual line along the cleaned waypoints.</summary>
    void RedrawLineOnWaypoints()
    {
        if (currentLine == null || waypoints.Count == 0) return;
        currentLine.positionCount = waypoints.Count;
        currentLine.SetPositions(waypoints.ToArray());
    }

    bool IsValidAgent()
    {
        if (currentAgent == null) return false;
        if (!currentAgent.gameObject.activeInHierarchy) return false;
        if (!currentAgent.isOnNavMesh) return false;
        return true;
    }

    // ─────────────────────────────────────────────
    //  PUBLIC API  (called from other scripts)
    // ─────────────────────────────────────────────

    public void StopPathLogicOnly()
    {
        isDrawing  = false;
        isFollowing = false;
        pathPoints.Clear();
        waypoints.Clear();
        currentWaypointIndex = 0;

        if (currentLine != null)
        {
            Destroy(currentLine.gameObject);
        }

        if (IsValidAgent())
        {
            currentAgent.isStopped = true;
            currentAgent.ResetPath();
        }
    }
}   