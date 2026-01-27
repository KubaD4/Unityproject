using UnityEngine;
using Oculus.Interaction;
using System;

public class flag : MonoBehaviour
{
    [Header("reference settings")]
    public CakePathFollower avatarScript; // auto-find the active Avatar if null or inactive

    [Header("Pointable Element")]
    public PointableElement pointableElement; 

    private void OnEnable()
    {
        if (pointableElement != null)
        {
            pointableElement.WhenPointerEventRaised += HandlePointerEventRaised;
        }
    }

    private void OnDisable()
    {
        if (pointableElement != null)
        {
            pointableElement.WhenPointerEventRaised -= HandlePointerEventRaised;
        }
    }

    // Parameter type PointerEvent
    private void HandlePointerEventRaised(PointerEvent evt)
    {
        // Logic triggered when the grab is released
        if (evt.Type == PointerEventType.Unselect)
        {
            Debug.Log("Flag released, instructing Avatar to move to: " + transform.position);

            // 1. If the current avatarScript is null, or its GameObject is inactive
            if (avatarScript == null || !avatarScript.gameObject.activeInHierarchy)
            {
                Debug.Log("⚠️ Current bound Avatar invalid; searching for an active Avatar...");

                // 2. Brutally search all CakePathFollower in the scene
                var allFollowers = FindObjectsByType<CakePathFollower>(FindObjectsSortMode.None);

                foreach (var follower in allFollowers)
                {
                    // 3. find the active avater in hierachy
                    if (follower.gameObject.activeInHierarchy)
                    {
                        avatarScript = follower; // rebind reference
                        Debug.Log($"✅ rebind successfully: {follower.gameObject.name}");
                        break; 
                    }
                }
            }

            if (avatarScript != null && avatarScript.gameObject.activeInHierarchy)
            {
                avatarScript.MoveAndSpawnCakes(transform.position);
            }
            else
            {
                Debug.LogWarning("❌ error：no live CakePathFollower！");
            }
        }
    }
}