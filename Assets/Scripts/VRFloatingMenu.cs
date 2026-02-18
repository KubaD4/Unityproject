using UnityEngine;
using UnityEngine.InputSystem;

public class VRFloatingMenu : MonoBehaviour
{
    public GameObject floatingMenu;      
    public Transform head;                
    public float distance = 0.8f;
    
    [Header("Input")]
    [Tooltip("Assign the menu button action here")]
    public InputActionProperty toggleAction = new InputActionProperty(new InputAction("Menu", binding: "<XRController>/menuButton"));

    private bool isOpen = false;

    void OnEnable()
    {
        if (toggleAction.action != null) toggleAction.action.Enable();
    }

    void OnDisable()
    {
        if (toggleAction.action != null) toggleAction.action.Disable();
    }

    void Update()
    {
        bool toggled = false;

        // 1. XR Controller Input
        if (toggleAction.action != null && toggleAction.action.WasPressedThisFrame())
        {
            toggled = true;
        }

        // 2. Keyboard Fallback (for Simulator/Editor) -> Press 'M'
        if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
        {
            toggled = true;
        }

        if (toggled)
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
        if (floatingMenu == null) return;
        if (head == null) head = Camera.main != null ? Camera.main.transform : transform;

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
        if (floatingMenu == null) return;
        floatingMenu.SetActive(false);
        isOpen = false;
    }
}