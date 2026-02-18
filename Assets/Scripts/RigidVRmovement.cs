using UnityEngine;

public class RigidbodyVRMovement : MonoBehaviour
{
    [Header("Move Setting")]
    public float moveSpeed = 5.0f;

    [Header("Rotate Speed")]
    public float rotateSpeed = 100.0f;

    [Header("Component Reference")]
    public Transform cameraTransform;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (rb != null)
            rb.freezeRotation = true;
    }

    void Update()
    {
        bool leftConnected  = OVRInput.IsControllerConnected(OVRInput.Controller.LTouch);
        bool rightConnected = OVRInput.IsControllerConnected(OVRInput.Controller.RTouch);

        Debug.Log("Left: " + leftConnected + " | Right: " + rightConnected);

        Vector2 stick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);
        Debug.Log("Stick: " + stick);
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        HandleRotation();
        HandleMovement();
    }

    void HandleRotation()
    {
        float rotateDir = 0f;

        // VR: joystick destro sull'asse X
        rotateDir = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick).x;

        // Fallback keyboard se il joystick non viene premuto
        if (Mathf.Abs(rotateDir) < 0.01f)
        {
            if (Input.GetKey(KeyCode.Q)) rotateDir = -1f;
            if (Input.GetKey(KeyCode.E)) rotateDir =  1f;
        }

        // Fallback mouse (tasto destro tenuto premuto)
        if (Mathf.Abs(rotateDir) < 0.01f && Input.GetMouseButton(1))
        {
            rotateDir = Input.GetAxis("Mouse X") * 0.5f;
        }

        if (Mathf.Abs(rotateDir) > 0.01f)
        {
            float turnAmount = rotateDir * rotateSpeed * Time.fixedDeltaTime;
            Quaternion turnOffset = Quaternion.Euler(0, turnAmount, 0);
            rb.MoveRotation(rb.rotation * turnOffset);
        }
    }

    void HandleMovement()
    {
        float x = 0f;
        float z = 0f;

        // VR: joystick sinistro
        Vector2 stick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);
        x = stick.x;
        z = stick.y;

        // Fallback keyboard WASD se il joystick non viene premuto
        if (Mathf.Approximately(x, 0f) && Mathf.Approximately(z, 0f))
        {
            x = Input.GetAxis("Horizontal"); // A/D
            z = Input.GetAxis("Vertical");   // W/S
        }

        Vector3 moveDirection = Vector3.zero;

        if (cameraTransform != null)
        {
            Vector3 forward = cameraTransform.forward;
            Vector3 right   = cameraTransform.right;

            forward.y = 0;
            right.y   = 0;
            forward.Normalize();
            right.Normalize();

            moveDirection = forward * z + right * x;
        }

        Vector3 finalVelocity = moveDirection * moveSpeed;
        rb.linearVelocity = new Vector3(finalVelocity.x, rb.linearVelocity.y, finalVelocity.z);
    }
}