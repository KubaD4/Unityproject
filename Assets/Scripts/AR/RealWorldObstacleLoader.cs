using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using System.Collections.Generic;
using Meta.XR.MRUtilityKit;


public class RealWorldObstacleLoader : MonoBehaviour
{
    [Header("NavMesh Settings")]
    [Tooltip("Enable carving so dynamic obstacles cut through the baked NavMesh.")]
    public bool enableCarving = true;

    [Tooltip("Optional: NavMeshSurface to rebake at runtime after obstacles load. " +
             "Leave null to rely solely on NavMeshObstacle carving.")]
    public NavMeshSurface navMeshSurface;

    [Header("Which scene labels become obstacles (block avatar movement)")]
    [Tooltip("Furniture, walls, and objects that the avatar should navigate around.")]
    public MRUKAnchor.SceneLabels obstacleLabels =
        MRUKAnchor.SceneLabels.TABLE |
        MRUKAnchor.SceneLabels.COUCH |
        MRUKAnchor.SceneLabels.WALL_FACE |
        MRUKAnchor.SceneLabels.STORAGE |
        MRUKAnchor.SceneLabels.BED |
        MRUKAnchor.SceneLabels.SCREEN |
        MRUKAnchor.SceneLabels.LAMP |
        MRUKAnchor.SceneLabels.PLANT |
        MRUKAnchor.SceneLabels.OTHER;

    [Header("Which labels get colliders only (no NavMesh obstacle)")]
    [Tooltip("Floor/ceiling get colliders for physics but should not block the NavMesh.")]
    public MRUKAnchor.SceneLabels colliderOnlyLabels =
        MRUKAnchor.SceneLabels.FLOOR |
        MRUKAnchor.SceneLabels.CEILING;

    [Header("Global Mesh")]
    [Tooltip("Also add a MeshCollider from the room's global mesh scan for highest " +
             "fidelity physics (covers irregular shapes EffectMesh can't capture).")]
    public bool useGlobalMeshCollider = false;

    [Header("Wall Thickness")]
    [Tooltip("Walls are 2D planes — this adds thickness to the collider (meters).")]
    public float wallThickness = 0.15f;

    [Tooltip("Default thickness for other plane-only anchors (meters).")]
    public float defaultPlaneThickness = 0.05f;

    [Header("Debug")]
    [Tooltip("Show semi-transparent debug cubes on each obstacle.")]
    public bool showDebugVisuals = false;

    // ─── Internal state ──────────────────────────────────────────────
    private readonly List<GameObject> _spawnedObjects = new List<GameObject>();
    private bool _isLoaded = false;

    // ─── Lifecycle ───────────────────────────────────────────────────

    void OnEnable()
    {
        // Register with MRUK — fires immediately if scene is already loaded
        if (MRUK.Instance != null)
        {
            MRUK.Instance.RegisterSceneLoadedCallback(OnSceneLoaded);
        }
        else
        {
            Debug.LogWarning("[RealWorldObstacleLoader] MRUK.Instance not found. " +
                "Make sure an MRUK component exists in the scene and is configured " +
                "with DataSource = Device.");
        }
    }

    void OnDisable()
    {
        ClearObstacles();
    }

    // ─── Public API ──────────────────────────────────────────────────

    /// <summary>
    /// Manually trigger loading obstacles. Useful when re-entering AR mode.
    /// </summary>
    public void LoadObstacles()
    {
        if (MRUK.Instance != null && MRUK.Instance.IsInitialized)
        {
            OnSceneLoaded();
        }
        else if (MRUK.Instance != null)
        {
            // Scene not loaded yet — request load from device
            MRUK.Instance.LoadSceneFromDevice();
        }
        else
        {
            Debug.LogError("[RealWorldObstacleLoader] Cannot load — MRUK.Instance is null.");
        }
    }

    /// <summary>
    /// Remove all generated colliders and NavMeshObstacles.
    /// </summary>
    public void ClearObstacles()
    {
        foreach (var obj in _spawnedObjects)
        {
            if (obj != null) Destroy(obj);
        }
        _spawnedObjects.Clear();
        _isLoaded = false;

        Debug.Log("[RealWorldObstacleLoader] Cleared all real-world obstacles.");
    }

    // ─── Core Logic ──────────────────────────────────────────────────

    private void OnSceneLoaded()
    {
        // Avoid duplicate processing
        if (_isLoaded) return;

        MRUKRoom room = MRUK.Instance.GetCurrentRoom();
        if (room == null)
        {
            Debug.LogWarning("[RealWorldObstacleLoader] No room found. " +
                "Has the user completed Space Setup on the headset?");
            return;
        }

        int obstacleCount = 0;
        int colliderOnlyCount = 0;

        foreach (MRUKAnchor anchor in room.Anchors)
        {
            bool isObstacle = anchor.HasAnyLabel(obstacleLabels);
            bool isColliderOnly = anchor.HasAnyLabel(colliderOnlyLabels);

            if (!isObstacle && !isColliderOnly) continue;

            // Create the collider
            Collider col = CreateColliderForAnchor(anchor);
            if (col == null) continue;

            // Add NavMeshObstacle only for obstacle types (not floor/ceiling)
            if (isObstacle)
            {
                AddNavMeshObstacle(col);
                obstacleCount++;
            }
            else
            {
                colliderOnlyCount++;
            }

            // Debug visuals
            if (showDebugVisuals)
            {
                AddDebugVisual(col, anchor.Label);
            }
        }

        // Optional: global mesh collider for high-fidelity room geometry
        if (useGlobalMeshCollider && room.GlobalMeshAnchor != null)
        {
            AddGlobalMeshCollider(room.GlobalMeshAnchor);
        }

        _isLoaded = true;

        Debug.Log($"[RealWorldObstacleLoader] Loaded {obstacleCount} obstacles " +
                  $"+ {colliderOnlyCount} collider-only surfaces from room '{room.name}'.");

        // Optionally rebake NavMesh at runtime
        if (navMeshSurface != null)
        {
            navMeshSurface.BuildNavMesh();
            Debug.Log("[RealWorldObstacleLoader] NavMeshSurface rebaked at runtime.");
        }
    }

    /// <summary>
    /// Creates a BoxCollider (for volumes and planes) or MeshCollider (for global mesh)
    /// on the anchor's GameObject, sized to match the real-world object.
    /// </summary>
    private Collider CreateColliderForAnchor(MRUKAnchor anchor)
    {
        GameObject go = anchor.gameObject;

        // VOLUMES (tables, couches, storage, etc.) → BoxCollider from 3D bounds
        if (anchor.VolumeBounds.HasValue)
        {
            // Skip if a BoxCollider already exists with the right size
            BoxCollider existing = go.GetComponent<BoxCollider>();
            if (existing != null) return existing;

            BoxCollider box = go.AddComponent<BoxCollider>();
            Bounds bounds = anchor.VolumeBounds.Value;
            box.center = bounds.center;
            box.size = bounds.size;
            return box;
        }

        // PLANES (walls, floor, ceiling) → BoxCollider with thickness
        if (anchor.PlaneRect.HasValue)
        {
            BoxCollider existing = go.GetComponent<BoxCollider>();
            if (existing != null) return existing;

            BoxCollider box = go.AddComponent<BoxCollider>();
            Rect rect = anchor.PlaneRect.Value;

            // Determine thickness based on label
            float thickness = defaultPlaneThickness;
            if (anchor.HasAnyLabel(MRUKAnchor.SceneLabels.WALL_FACE))
                thickness = wallThickness;

            // PlaneRect.size gives 2D width/height in local XY.
            // Z axis is the plane normal → we add thickness along it.
            box.center = new Vector3(rect.center.x, rect.center.y, 0f);
            box.size = new Vector3(rect.size.x, rect.size.y, thickness);
            return box;
        }

        return null;
    }

    /// <summary>
    /// Adds a NavMeshObstacle with carving to the object so the NavMesh
    /// dynamically adjusts around real-world obstacles.
    /// </summary>
    private void AddNavMeshObstacle(Collider col)
    {
        if (col == null) return;
        GameObject go = col.gameObject;

        if (go.GetComponent<NavMeshObstacle>() != null) return;

        NavMeshObstacle obstacle = go.AddComponent<NavMeshObstacle>();
        obstacle.carving = enableCarving;
        obstacle.shape = NavMeshObstacleShape.Box;

        if (col is BoxCollider box)
        {
            obstacle.size = box.size;
            obstacle.center = box.center;
        }
        else if (col is MeshCollider mesh && mesh.sharedMesh != null)
        {
            // Approximate with mesh bounds
            Bounds b = mesh.sharedMesh.bounds;
            obstacle.size = b.size;
            obstacle.center = b.center;
        }
    }

    /// <summary>
    /// Adds a MeshCollider using the room's scanned global mesh for
    /// highest-fidelity collision with irregular real-world geometry.
    /// </summary>
    private void AddGlobalMeshCollider(MRUKAnchor globalMeshAnchor)
    {
        Mesh globalMesh = globalMeshAnchor.GlobalMesh;
        if (globalMesh == null)
        {
            // Try loading from device
            globalMesh = globalMeshAnchor.LoadGlobalMeshTriangles();
        }

        if (globalMesh == null)
        {
            Debug.LogWarning("[RealWorldObstacleLoader] Global mesh not available.");
            return;
        }

        MeshCollider existing = globalMeshAnchor.gameObject.GetComponent<MeshCollider>();
        if (existing != null) return;

        MeshCollider mc = globalMeshAnchor.gameObject.AddComponent<MeshCollider>();
        mc.sharedMesh = globalMesh;

        Debug.Log($"[RealWorldObstacleLoader] Global mesh collider added " +
                  $"({globalMesh.vertexCount} vertices, {globalMesh.triangles.Length / 3} triangles).");
    }

    // ─── Debug Visuals ───────────────────────────────────────────────

    private void AddDebugVisual(Collider col, MRUKAnchor.SceneLabels label)
    {
        if (col == null) return;

        // Create a child cube to visualize the collider bounds
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = $"Debug_{label}";
        Destroy(visual.GetComponent<Collider>()); // Remove the primitive's collider

        visual.transform.SetParent(col.transform, false);

        if (col is BoxCollider box)
        {
            visual.transform.localPosition = box.center;
            visual.transform.localScale = box.size;
        }

        // Semi-transparent color based on label
        Renderer rend = visual.GetComponent<Renderer>();
        if (rend != null)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetFloat("_Surface", 1); // Transparent
            mat.SetFloat("_Blend", 0);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3000;
            mat.color = GetColorForLabel(label);
            rend.material = mat;
        }

        _spawnedObjects.Add(visual);
    }

    private Color GetColorForLabel(MRUKAnchor.SceneLabels label)
    {
        if ((label & MRUKAnchor.SceneLabels.WALL_FACE) != 0)
            return new Color(0.4f, 0.4f, 1.0f, 0.25f); // Blue walls
        if ((label & MRUKAnchor.SceneLabels.TABLE) != 0)
            return new Color(0.9f, 0.6f, 0.2f, 0.3f);  // Orange tables
        if ((label & MRUKAnchor.SceneLabels.COUCH) != 0)
            return new Color(0.2f, 0.8f, 0.3f, 0.3f);  // Green couches
        if ((label & MRUKAnchor.SceneLabels.FLOOR) != 0)
            return new Color(0.3f, 0.3f, 0.3f, 0.1f);  // Gray floor
        if ((label & MRUKAnchor.SceneLabels.CEILING) != 0)
            return new Color(0.5f, 0.5f, 0.5f, 0.1f);  // Gray ceiling
        if ((label & MRUKAnchor.SceneLabels.STORAGE) != 0)
            return new Color(0.7f, 0.3f, 0.7f, 0.3f);  // Purple storage
        if ((label & MRUKAnchor.SceneLabels.BED) != 0)
            return new Color(0.9f, 0.3f, 0.3f, 0.3f);  // Red bed

        return new Color(1f, 1f, 0f, 0.25f);            // Yellow default
    }
}
