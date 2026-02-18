using UnityEngine;

public class VRFloatingMenu : MonoBehaviour
{
    public GameObject floatingMenu;      
    public Transform head;                
    public float distance = 0.8f;

    private bool isOpen = false;

    void Update()
    {
        // Toggle menu with Meta Quest "B" button (right controller)
        // B = Button.Two on right controller; Y = Button.Four on left controller
        if (OVRInput.GetDown(OVRInput.Button.Two))
        {
            ToggleMenu();
        }
    }

    void ToggleMenu()
    {
        Debug.Log("Toggling menu. Current state: " + (isOpen ? "Open" : "Closed"));
        if (!isOpen)
        {
            OpenMenu();
        }
        else
        {
            CloseMenu();
        }
    }

    void OpenMenu()
    {
        // Posizione davanti alla testa
        floatingMenu.transform.position =
            head.position + head.forward * distance;

        // Rotazione verso il giocatore
        floatingMenu.transform.rotation =
            Quaternion.LookRotation(floatingMenu.transform.position - head.position);

        floatingMenu.SetActive(true);
        isOpen = true;
    }

    void CloseMenu()
    {
        floatingMenu.SetActive(false);
        isOpen = false;
    }
}