using UnityEngine;
using UnityEngine.AI;

public class NavMeshDebug : MonoBehaviour
{
    public NavMeshAgent agent;
    public Transform player;

    // Update removed to avoid Input System conflict


    void OnEnable() {
        InvokeRepeating(nameof(PrintDebugInfo), 1f, 5f);
    }

    void PrintDebugInfo()
    {
        var triangulation = NavMesh.CalculateTriangulation();
        Debug.Log($"[NavMeshDebug] NavMesh Vertices: {triangulation.vertices.Length}, Indices: {triangulation.indices.Length}");
        
        if (agent != null)
        {
            Debug.Log($"[NavMeshDebug] Agent Pos: {agent.transform.position}, OnNavMesh: {agent.isOnNavMesh}, Active: {agent.gameObject.activeInHierarchy}");
        }

        if (player != null)
        {
             Debug.Log($"[NavMeshDebug] Player Pos: {player.position}");
        }
        
        NavMeshHit hit;
        bool found = NavMesh.SamplePosition(transform.position, out hit, 100f, NavMesh.AllAreas);
        Debug.Log($"[NavMeshDebug] SamplePosition(at self) result: {found}, Hit Pos: {(found ? hit.position : Vector3.zero)}");
    }
}
