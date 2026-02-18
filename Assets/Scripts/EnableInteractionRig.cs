using UnityEngine;
using System.Collections;

public class EnableInteractionRig : MonoBehaviour
{
    [Tooltip("Assign the OVRInteractionComprehensive object here")]
    public GameObject rig;

    IEnumerator Start()
    {
        // Wait for OVRManager to initialize
        int frames = 0;
        while (OVRManager.instance == null && frames < 60)
        {
            yield return null;
            frames++;
        }

        if (OVRManager.instance == null)
        {
             Debug.LogWarning("[EnableInteractionRig] OVRManager not found after wait! Enabling rig anyway (might crash).");
        }
        else
        {
             Debug.Log("[EnableInteractionRig] OVRManager found. Enabling Interaction Rig.");
        }
        
        // Small delay to ensure HMD tracking is established
        yield return new WaitForSeconds(0.5f);

        if (rig != null)
        {
            rig.SetActive(true);
            Debug.Log("[EnableInteractionRig] Rig activated set to TRUE.");
        }
        else
        {
            Debug.LogError("[EnableInteractionRig] Rig reference is missing!");
        }
    }
}
