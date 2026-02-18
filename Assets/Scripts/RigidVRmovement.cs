using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

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

        // ── VR: right thumbstick for rotation ──
        float rotateDir = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick).x;

        // Keyboard fallback (Q/E) via new Input System
#if ENABLE_INPUT_SYSTEM
        if (Mathf.Abs(rotateDir) < 0.01f)
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.qKey.isPressed) rotateDir = -1f;
                if (kb.eKey.isPressed) rotateDir = 1f;
            }
        }
#endif

        if (rotateDir != 0)
        {
            float turnAmount = rotateDir * rotateSpeed * Time.fixedDeltaTime;
            Quaternion turnOffset = Quaternion.Euler(0, turnAmount, 0);
            rb.MoveRotation(rb.rotation * turnOffset);
        }

        // ── VR: left thumbstick for movement ──
        Vector2 stick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);
        float x = stick.x;
        float z = stick.y;

        // Keyboard fallback (WASD) via new Input System
#if ENABLE_INPUT_SYSTEM
        if (Mathf.Approximately(x, 0f) && Mathf.Approximately(z, 0f))
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed) x -= 1f;
                if (kb.dKey.isPressed) x += 1f;
                if (kb.wKey.isPressed) z += 1f;
                if (kb.sKey.isPressed) z -= 1f;
            }
        }
#endif

        // Mouse look fallback via new Input System
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse != null && mouse.rightButton.isPressed)
        {
            float mouseX = mouse.delta.x.ReadValue() * 0.1f;
            Quaternion mouseTurnOffset = Quaternion.Euler(0, mouseX * 2.0f, 0);
            rb.MoveRotation(rb.rotation * mouseTurnOffset);
        }
#endif

        Vector3 moveDirection = Vector3.zero;

        // Calculate movement direction
        if (cameraTransform != null)
        {
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