using UnityEngine;
using UnityEngine.AI;

public class AvatarSelector : MonoBehaviour
{
    [Header("Avatar model")]
    public GameObject bananaMan;
    public GameObject racer;

    [Header("Controller reference")]
    public VRGuideController guideController;

    public void ChooseBanana() { SwitchAvatar(bananaMan, racer); }
    public void ChooseRacer() { SwitchAvatar(racer, bananaMan); }

    private void SwitchAvatar(GameObject toShow, GameObject toHide)
    {   
        Debug.Log($"[Selector] {toShow.name}, {toHide.name}");
        toHide.SetActive(false);
        toShow.SetActive(true);

        // Automatically extract components and distribute them to the controller.
        if (guideController != null)
        {
            guideController.agent = toShow.GetComponent<NavMeshAgent>();

            guideController.animator = toShow.GetComponentInChildren<Animator>();

            guideController.isEating = false;

            Debug.Log($"[Selector] 已切换至 {toShow.name}，Animator 绑定: {(guideController.animator != null ? "成功" : "失败")}");

            // update NavMesh position
            if (guideController.agent != null)
            {
                NavMeshHit hit;
                if (NavMesh.SamplePosition(toShow.transform.position, out hit, 2.0f, NavMesh.AllAreas))
                {
                    guideController.agent.Warp(hit.position);
                }
            }
        }
    }
}