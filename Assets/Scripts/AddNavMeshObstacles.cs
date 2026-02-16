using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Attach this to any GameObject in your scene and press the context-menu item
/// "Setup NavMesh Obstacles" (or let it run automatically on Awake) to add
/// NavMeshObstacle components to every child that has a Collider but no
/// NavMeshAgent. This makes boxes, walls, etc. dynamically carve holes in
/// the NavMesh so the avatar steers around them.
///
/// Usage:
///   1. Add this script to a root GameObject (e.g. "Environment" or "Obstacles").
///   2. All children with BoxCollider / SphereCollider / CapsuleCollider /
///      MeshCollider will get a NavMeshObstacle that matches the collider size.
///   3. "Carve" is enabled so the NavMeshAgent replans paths around them.
/// </summary>
public class AddNavMeshObstacles : MonoBehaviour
{
    [Tooltip("Automatically add NavMeshObstacle components when the scene starts.")]
    public bool setupOnAwake = true;

    [Tooltip("Enable carving so the NavMesh is cut in real-time around obstacles.")]
    public bool carve = true;

    [Tooltip("Only process children — skip the root GameObject itself.")]
    public bool skipRoot = true;

    void Awake()
    {
        if (setupOnAwake)
        {
            SetupObstacles();
        }
    }

    [ContextMenu("Setup NavMesh Obstacles")]
    public void SetupObstacles()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        int added = 0;

        foreach (Collider col in colliders)
        {
            // Skip the root if requested
            if (skipRoot && col.gameObject == gameObject) continue;

            // Don't add obstacles to objects that are NavMeshAgents themselves
            if (col.GetComponent<NavMeshAgent>() != null) continue;

            // Skip if already has a NavMeshObstacle
            if (col.GetComponent<NavMeshObstacle>() != null) continue;

            // Skip triggers — they aren't physical obstacles
            if (col.isTrigger) continue;

            NavMeshObstacle obstacle = col.gameObject.AddComponent<NavMeshObstacle>();
            obstacle.carving = carve;

            // Match obstacle shape to collider
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
                // Approximate with the mesh bounds
                obstacle.shape = NavMeshObstacleShape.Box;
                Bounds b = mesh.sharedMesh != null ? mesh.sharedMesh.bounds : col.bounds;
                obstacle.size = b.size;
                obstacle.center = b.center;
            }

            added++;
        }

        Debug.Log($"AddNavMeshObstacles: Added {added} NavMeshObstacle component(s) under '{gameObject.name}'.");
    }
}
