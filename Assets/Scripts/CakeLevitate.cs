using UnityEngine;

/// <summary>
/// Gives a cake a gentle low-level levitation bobbing effect after spawning.
/// Attach via code — CakePathFollower adds this automatically.
/// </summary>
public class CakeLevitate : MonoBehaviour
{
    [Tooltip("How far (units) the cake bobs up and down from its spawn point")]
    public float bobHeight = 0.08f;

    [Tooltip("How fast the cake bobs (cycles per second)")]
    public float bobSpeed = 1.5f;

    [Tooltip("Degrees per second the cake slowly rotates")]
    public float rotateSpeed = 30f;

    private Vector3 startPos;
    private float randomOffset;

    void Start()
    {
        startPos = transform.position;
        // Random phase offset so not all cakes bob in sync
        randomOffset = Random.Range(0f, Mathf.PI * 2f);
    }

    void Update()
    {
        // Gentle sine-wave bob
        float newY = startPos.y + Mathf.Sin((Time.time * bobSpeed) + randomOffset) * bobHeight;
        transform.position = new Vector3(startPos.x, newY, startPos.z);

        // Slow rotation for a magical floating look
        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
    }
}
