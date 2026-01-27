using UnityEngine;

public class SimpleLaser : MonoBehaviour
{
    public LineRenderer lineRenderer;
    public float maxDistance = 100f; // How far can a laser reach?

    void Update()
    {
        if (lineRenderer == null) return;

        // 1. starting point of the line
        lineRenderer.SetPosition(0, transform.position);

        RaycastHit hit;
        // 2. Emission X-ray Detection
        if (Physics.Raycast(transform.position, transform.forward, out hit, maxDistance))
        {
            // if the line hit something
            lineRenderer.SetPosition(1, hit.point);
        }
        else
        {
            // Shoot into the void: Draw a line of fixed length.
            lineRenderer.SetPosition(1, transform.position + transform.forward * maxDistance);
        }
    }
}