using UnityEngine;

public class SimpleMove : MonoBehaviour
{
    public float moveSpeed = 3.0f;   
    public float turnSpeed = 60.0f;  
    public Transform cameraHead;    

    void Update()
    {
        // --- 1. Get input (simultaneously listen to VR controllers and keyboard) ---

        // A. enter movement input
        Vector2 moveInput = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick); // VR left

        // if no VR input, try keyboard (WASD or arrow keys)
        if (moveInput == Vector2.zero)
        {
            moveInput.x = Input.GetAxis("Horizontal"); // keyboard A/D or ←/→
            moveInput.y = Input.GetAxis("Vertical");   // keyboard W/S or ↑/↓
        }

        // B. enter turn input
        float turnInput = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick).x; // VR right

        if (Mathf.Abs(turnInput) < 0.01f)
        {
            if (Input.GetKey(KeyCode.Q)) turnInput = -1;
            else if (Input.GetKey(KeyCode.E)) turnInput = 1;
        }

        // Execution logic

        // handle movement
        if (cameraHead != null && moveInput != Vector2.zero)
        {
            // get direction relative to camera head
            Vector3 dir = cameraHead.forward * moveInput.y + cameraHead.right * moveInput.x;
            dir.y = 0;
            dir.Normalize();

            transform.Translate(dir * moveSpeed * Time.deltaTime, Space.World);
        }

        // handle turning
        if (Mathf.Abs(turnInput) > 0.01f)
        {
            transform.Rotate(0, turnInput * turnSpeed * Time.deltaTime, 0);
        }
    }
}