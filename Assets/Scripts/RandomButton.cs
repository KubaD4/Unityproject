using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;

public class RandomButton : MonoBehaviour
{
    [Header("Configuration")]
    public VRGuideController guideController;
    public DrawPathMovement pathManager;

    public void OnClickRandom()
    {
        Debug.Log("Button is pressed");

        // Auto-find guide controller if not assigned (e.g. AR button)
        if (guideController == null)
        {
            guideController = FindFirstObjectByType<VRGuideController>();
            if (guideController != null)
                Debug.Log($"[RandomButton] Auto-found VRGuideController on '{guideController.gameObject.name}'");
        }

        if (guideController != null)
        {
            // 1. move agent to random destination
            guideController.SetRandomDestination();

            // 2. connect agent to path manager
            ConnectAgentToPathManager();
        }
        else
        {
            Debug.LogError("【Error】Guide Controller not configured and none found in scene！");
        }
    }

    void ConnectAgentToPathManager()
    {
        if (pathManager == null)
        {
            pathManager = FindFirstObjectByType<DrawPathMovement>();
        }

        if (pathManager != null && guideController.agent != null)
        {
            pathManager.currentAgent = guideController.agent;
            Debug.Log($"PathManager start to controll: {guideController.agent.name}");
        }
        else
        {
            Debug.LogWarning("Connection failed");
        }
    }

}