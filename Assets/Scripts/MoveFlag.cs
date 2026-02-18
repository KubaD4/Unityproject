using UnityEngine;

public class MoveFlag : MonoBehaviour
{   
    [Header("Flag")]
    public GameObject flag;
    public float distance = 0.8f; 

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void moveAndSpawn()
    {
        if (flag != null)
        {
            //move flag right beside user
            flag.transform.position =
                Camera.main.transform.position + Camera.main.transform.forward * distance;


            flag.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogError("Flag reference not set!");
        }
    }

    public void despawn()
    {
        if (flag != null)
        {
            flag.gameObject.SetActive(false);
        }
    }
}
