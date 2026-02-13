using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class ARFloorAlign : MonoBehaviour
{
    [Header("Assign your XR Rig or Player Root")]
    public Transform player;

    [Header("Floor Y position relative to world")]
    public float floorHeight = 0f;

    void LateUpdate()
    {
        if (player == null) return;

        // Align floor horizontally with player
        Vector3 pos = transform.position;
        pos.x = player.position.x;
        pos.z = player.position.z;
        pos.y = floorHeight;
        transform.position = pos;
    }
}
