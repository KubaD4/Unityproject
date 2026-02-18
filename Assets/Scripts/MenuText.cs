using UnityEngine;

public class MenuText : MonoBehaviour
{
    public Transform head;
    public float distance = 0.8f;
    public float duration = 0.8f;

    private GameObject textObj;
    private float timer;

    void Start()
    {
        // Crea GameObject
        textObj = new GameObject("MenuText");

        TextMesh textMesh = textObj.AddComponent<TextMesh>();
        textMesh.text = "Press B to turn menu on/off";
        textMesh.fontSize = 64;
        textMesh.characterSize = 0.01f;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;

        timer = duration;
    }

    void Update()
    {
        if (textObj == null) return;

        if (timer > 0f)
        {
            timer -= Time.deltaTime;

            Vector3 forward = head.forward;
            forward.y = 0;
            forward.Normalize();

            textObj.transform.position =
                head.position + forward * distance;

            textObj.transform.rotation =
                Quaternion.LookRotation(forward);

            // Optional: make text fade out 
            if (timer < duration / 2f)
            {
                TextMesh textMesh = textObj.GetComponent<TextMesh>();
                Color color = textMesh.color;
                color.a = timer / (duration / 2f);
                textMesh.color = color;
            }
        }
        else
        {
            Destroy(textObj);
        }
    }
}