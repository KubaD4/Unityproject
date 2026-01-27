using UnityEngine;

public class RigidbodyVRMovement : MonoBehaviour
{
    [Header("movesetting")]
    public float moveSpeed = 5.0f;

    [Header("rotatespeed")]
    public float rotateSpeed = 100.0f; 

    [Header("Component reference")]
    public Transform cameraTransform;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        // find the camera
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        // Ensure the rigid body is set up correctly.
        if (rb != null)
        {
            rb.freezeRotation = true; 
        }
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        // 1. Q/E rotation
        float rotateDir = 0f;
        if (Input.GetKey(KeyCode.Q)) rotateDir = -1f; // left
        if (Input.GetKey(KeyCode.E)) rotateDir = 1f;  // right

        // press Q or E
        if (rotateDir != 0)
        {
            // Rotate the body around the Y-axis
            float turnAmount = rotateDir * rotateSpeed * Time.fixedDeltaTime;
            Quaternion turnOffset = Quaternion.Euler(0, turnAmount, 0);
            rb.MoveRotation(rb.rotation * turnOffset);
        }

        // 2.  WASD 
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 moveDirection = Vector3.zero;

        // If you don't have a headset, use the right mouse button to assist with steering (optional).
        if (Input.GetMouseButton(1))
        {
            float mouseX = Input.GetAxis("Mouse X");
            float mouseTurn = mouseX * 2.0f; // Mouse sensitivity
            Quaternion mouseTurnOffset = Quaternion.Euler(0, mouseTurn, 0);
            rb.MoveRotation(rb.rotation * mouseTurnOffset);
        }

        // Calculate movement direction
        if (cameraTransform != null)
        {
            // get camera forward and right vectors
            Vector3 forward = cameraTransform.forward;
            Vector3 right = cameraTransform.right;

            forward.y = 0;
            right.y = 0;
            forward.Normalize();
            right.Normalize();

            moveDirection = forward * z + right * x;
        }

        // gravity compensation
        Vector3 finalVelocity = moveDirection * moveSpeed;
        rb.linearVelocity = new Vector3(finalVelocity.x, rb.linearVelocity.y, finalVelocity.z);
    }
}