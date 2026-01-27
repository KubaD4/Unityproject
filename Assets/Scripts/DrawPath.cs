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

    private LineRenderer currentLine;
    private List<Vector3> pathPoints = new List<Vector3>();
    private bool isDrawing = false;

    void Update()
    {
        if (drawingHand == null) return;

        // 1. Trigger pressed
        if (OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger))
        {
            StartDrawing();
        }

        // 2. Holding trigger
        if (OVRInput.Get(OVRInput.Button.SecondaryIndexTrigger) && isDrawing)
        {
            UpdateDrawing();
        }

        // 3. Release trigger
        if (OVRInput.GetUp(OVRInput.Button.SecondaryIndexTrigger) && isDrawing)
        {
            StopDrawingAndMove();
        }
    }
    void StartDrawing()
    {
        isDrawing = true;
        pathPoints.Clear();

        // Create new line
        if (linePrefab != null)
        {
            GameObject lineObj = Instantiate(linePrefab);
            currentLine = lineObj.GetComponent<LineRenderer>();
            currentLine.positionCount = 0;
        }

        // Try to reset path (if Agent exists)
        if (IsValidAgent())
        {
            currentAgent.isStopped = true;
            currentAgent.ResetPath();
        }
    }


    void UpdateDrawing()
    {
        // 1. Find the RayInteractor component in the scene
        var interactor = drawingHand.GetComponentInParent<RayInteractor>();

        if (interactor != null && interactor.HasSelectedInteractable)
        {
            // 2. Directly get the current physics hit point of the Meta ray
            Vector3 point = interactor.End;
            point.y = 0.05f;

            // 3. These points will vary as the hand moves
            if (Vector3.Distance(pathPoints[pathPoints.Count - 1], point) > 0.02f)
            {
                pathPoints.Add(point);
                currentLine.positionCount = pathPoints.Count;
                currentLine.SetPositions(pathPoints.ToArray());
                Debug.Log("Point obtained via Meta interface: " + point + " Count: " + pathPoints.Count);
            }
        }
    }

    void StopDrawingAndMove()
    {
        isDrawing = false;

        // Only move if the Agent is healthy and there are path points
        if (IsValidAgent() && pathPoints.Count > 0)
        {
            currentAgent.isStopped = false;
            currentAgent.SetDestination(pathPoints[pathPoints.Count - 1]);
        }
    }

    // check whether the Agent is healthy
    bool IsValidAgent()
    {
        if (currentAgent == null) return false;
        if (!currentAgent.gameObject.activeInHierarchy) return false;
        if (!currentAgent.isOnNavMesh) return false;
        return true;
    }

    public void StopPathLogicOnly()
    {
        // Force stop drawing state
        isDrawing = false;
        pathPoints.Clear();

        // If a line is currently being drawn
        if (currentLine != null)
        {
            Destroy(currentLine.gameObject);
        }

        // If the Agent is running, stop it
        if (IsValidAgent())
        {
            currentAgent.isStopped = true;
            currentAgent.ResetPath();
        }
    }
}   