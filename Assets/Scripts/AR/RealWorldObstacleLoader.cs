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

    [Header("Debug Visualization")]
    [Tooltip("Show semi-transparent overlays on each detected real-world obstacle.\n" +
             "Can be toggled at runtime via ToggleVisuals().")]
    public bool showDebugVisuals = false;

    [Tooltip("Opacity of the debug overlays (0 = invisible, 1 = fully opaque).")]
    [Range(0.05f, 1f)]
    public float visualOpacity = 0.35f;

    [Header("Hierarchy")]
    [Tooltip("Parent transform for MRUK room objects. If set, room anchors and " +
             "EffectMesh children will be reparented here so they toggle with AR mode.\n" +
             "Typically set to your ARSceneRoot.")]
    public Transform arParent;

    // ─── Internal state ──────────────────────────────────────────────
    private readonly List<GameObject> _spawnedObjects = new List<GameObject>();
    private readonly List<GameObject> _visualObjects = new List<GameObject>();
    private bool _isLoaded = false;
    private bool _visualsVisible = false;

    // ─── Lifecycle ───────────────────────────────────────────────────

    void OnEnable()
    {
        if (MRUK.Instance != null)
        {
            // Catch rooms the moment they're created — reparent immediately
            MRUK.Instance.RoomCreatedEvent.AddListener(OnRoomCreated);
            // Also handle the case where scene is already loaded
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
        if (MRUK.Instance != null)
        {
            MRUK.Instance.RoomCreatedEvent.RemoveListener(OnRoomCreated);
        }
        ClearObstacles();
    }

    /// <summary>
    /// Called by MRUK the instant a new room GameObject is created.
    /// Reparents it under ARRoot so EffectMesh children end up there too.
    /// </summary>
    private void OnRoomCreated(MRUKRoom room)
    {
        if (arParent == null) return;
        room.transform.SetParent(arParent, true);
        Debug.Log($"[RealWorldObstacleLoader] Room '{room.name}' reparented under '{arParent.name}'");
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
    /// Remove all generated colliders, NavMeshObstacles, and visuals.
    /// </summary>
    public void ClearObstacles()
    {
        foreach (var obj in _visualObjects)
        {
            if (obj != null) Destroy(obj);
        }
        _visualObjects.Clear();
        _visualsVisible = false;

        foreach (var obj in _spawnedObjects)
        {
            if (obj != null) Destroy(obj);
        }
        _spawnedObjects.Clear();
        _isLoaded = false;

        Debug.Log("[RealWorldObstacleLoader] Cleared all real-world obstacles.");
    }

    /// <summary>
    /// Toggle debug visual overlays on/off. Call from a UI button.
    /// </summary>
    public void ToggleVisuals()
    {
        SetVisualsVisible(!_visualsVisible);
    }

    /// <summary>
    /// Show or hide the debug visual overlays.
    /// </summary>
    public void SetVisualsVisible(bool visible)
    {
        _visualsVisible = visible;
        showDebugVisuals = visible;

        // If obstacles are loaded but visuals haven't been created yet, create them now
        if (visible && _isLoaded && _visualObjects.Count == 0)
        {
            CreateVisualsForLoadedAnchors();
        }

        foreach (var obj in _visualObjects)
        {
            if (obj != null) obj.SetActive(visible);
        }

        Debug.Log($"[RealWorldObstacleLoader] Visuals {(visible ? "ON" : "OFF")} " +
                  $"({_visualObjects.Count} overlays)");
    }

    /// <summary>
    /// Are the debug visuals currently visible?
    /// </summary>
    public bool AreVisualsVisible => _visualsVisible;

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

        // Make sure the room (and all its anchor children + EffectMesh objects)
        // lives under ARRoot
        if (arParent != null && room.transform.parent != arParent)
        {
            room.transform.SetParent(arParent, true);
            Debug.Log($"[RealWorldObstacleLoader] Room '{room.name}' reparented under '{arParent.name}'");
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

        }

        // Optional: global mesh collider for high-fidelity room geometry
        if (useGlobalMeshCollider && room.GlobalMeshAnchor != null)
        {
            AddGlobalMeshCollider(room.GlobalMeshAnchor);
        }

        _isLoaded = true;

        // Create visuals if enabled (initially hidden unless showDebugVisuals is on)
        CreateVisualsForLoadedAnchors();
        if (showDebugVisuals)
        {
            SetVisualsVisible(true);
        }

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

    // ─── Visualization ─────────────────────────────────────────────

    /// <summary>
    /// Creates visual overlays for all loaded anchors that have colliders.
    /// Called once after scene loads; visuals start hidden unless showDebugVisuals is on.
    /// </summary>
    private void CreateVisualsForLoadedAnchors()
    {
        if (MRUK.Instance == null) return;
        MRUKRoom room = MRUK.Instance.GetCurrentRoom();
        if (room == null) return;

        foreach (MRUKAnchor anchor in room.Anchors)
        {
            bool isRelevant = anchor.HasAnyLabel(obstacleLabels) ||
                              anchor.HasAnyLabel(colliderOnlyLabels);
            if (!isRelevant) continue;

            BoxCollider box = anchor.gameObject.GetComponent<BoxCollider>();
            if (box != null)
            {
                CreateBoxVisual(box, anchor.Label);
            }
        }

        // Global mesh visual
        if (useGlobalMeshCollider && room.GlobalMeshAnchor != null)
        {
            MeshCollider mc = room.GlobalMeshAnchor.gameObject.GetComponent<MeshCollider>();
            if (mc != null && mc.sharedMesh != null)
            {
                CreateMeshVisual(mc, MRUKAnchor.SceneLabels.GLOBAL_MESH);
            }
        }
    }

    private void CreateBoxVisual(BoxCollider box, MRUKAnchor.SceneLabels label)
    {
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = $"Visual_{label}";
        Destroy(visual.GetComponent<Collider>()); // Remove the primitive's own collider

        visual.transform.SetParent(box.transform, false);
        visual.transform.localPosition = box.center;
        visual.transform.localScale = box.size;

        ApplyVisualMaterial(visual, label);

        visual.SetActive(false); // Start hidden
        _visualObjects.Add(visual);
    }

    private void CreateMeshVisual(MeshCollider mc, MRUKAnchor.SceneLabels label)
    {
        GameObject visual = new GameObject($"Visual_{label}");
        visual.transform.SetParent(mc.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;

        MeshFilter mf = visual.AddComponent<MeshFilter>();
        mf.sharedMesh = mc.sharedMesh;

        MeshRenderer mr = visual.AddComponent<MeshRenderer>();
        ApplyVisualMaterial(visual, label);

        visual.SetActive(false);
        _visualObjects.Add(visual);
    }

    private void ApplyVisualMaterial(GameObject visual, MRUKAnchor.SceneLabels label)
    {
        Renderer rend = visual.GetComponent<Renderer>();
        if (rend == null) return;

        // Use URP/Unlit for a clean overlay look in AR passthrough
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        Material mat = new Material(shader);

        // Enable transparency
        mat.SetFloat("_Surface", 1); // 1 = Transparent in URP
        mat.SetFloat("_Blend", 0);   // Alpha blend
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        Color baseColor = GetColorForLabel(label);
        baseColor.a = visualOpacity;
        mat.color = baseColor;

        // URP uses _BaseColor instead of _Color
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", baseColor);

        rend.material = mat;
    }

    private Color GetColorForLabel(MRUKAnchor.SceneLabels label)
    {
        // Walls — blue
        if ((label & MRUKAnchor.SceneLabels.WALL_FACE) != 0)
            return new Color(0.3f, 0.5f, 1.0f, 1f);
        // Tables — orange
        if ((label & MRUKAnchor.SceneLabels.TABLE) != 0)
            return new Color(1.0f, 0.6f, 0.1f, 1f);
        // Couches — green
        if ((label & MRUKAnchor.SceneLabels.COUCH) != 0)
            return new Color(0.2f, 0.9f, 0.3f, 1f);
        // Floor — gray
        if ((label & MRUKAnchor.SceneLabels.FLOOR) != 0)
            return new Color(0.4f, 0.4f, 0.4f, 1f);
        // Ceiling — light gray
        if ((label & MRUKAnchor.SceneLabels.CEILING) != 0)
            return new Color(0.6f, 0.6f, 0.6f, 1f);
        // Storage — purple
        if ((label & MRUKAnchor.SceneLabels.STORAGE) != 0)
            return new Color(0.7f, 0.2f, 0.9f, 1f);
        // Bed — red
        if ((label & MRUKAnchor.SceneLabels.BED) != 0)
            return new Color(0.9f, 0.2f, 0.2f, 1f);
        // Screen — cyan
        if ((label & MRUKAnchor.SceneLabels.SCREEN) != 0)
            return new Color(0.1f, 0.9f, 0.9f, 1f);
        // Lamp — yellow
        if ((label & MRUKAnchor.SceneLabels.LAMP) != 0)
            return new Color(1.0f, 0.95f, 0.3f, 1f);
        // Plant — dark green
        if ((label & MRUKAnchor.SceneLabels.PLANT) != 0)
            return new Color(0.1f, 0.6f, 0.1f, 1f);
        // Door — brown
        if ((label & MRUKAnchor.SceneLabels.DOOR_FRAME) != 0)
            return new Color(0.6f, 0.35f, 0.1f, 1f);
        // Window — light blue
        if ((label & MRUKAnchor.SceneLabels.WINDOW_FRAME) != 0)
            return new Color(0.5f, 0.8f, 1.0f, 1f);
        // Global mesh — wireframe white
        if ((label & MRUKAnchor.SceneLabels.GLOBAL_MESH) != 0)
            return new Color(1f, 1f, 1f, 1f);

        // Other — yellow
        return new Color(1f, 0.9f, 0.2f, 1f);
    }
}
