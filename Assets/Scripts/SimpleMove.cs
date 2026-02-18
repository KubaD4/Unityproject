using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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

        // if no VR input, try keyboard (WASD or arrow keys) via new Input System
        if (moveInput == Vector2.zero)
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed) moveInput.x -= 1f;
                if (keyboard.dKey.isPressed) moveInput.x += 1f;
                if (keyboard.wKey.isPressed) moveInput.y += 1f;
                if (keyboard.sKey.isPressed) moveInput.y -= 1f;
            }
#endif
        }

        // B. enter turn input
        float turnInput = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick).x; // VR right

        if (Mathf.Abs(turnInput) < 0.01f)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.qKey.isPressed) turnInput = -1;
                else if (kb.eKey.isPressed) turnInput = 1;
            }
#endif
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