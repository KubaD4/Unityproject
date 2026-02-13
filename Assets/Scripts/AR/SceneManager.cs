using UnityEngine;

public class SceneManager : MonoBehaviour
{   
    [Header("Controllers")]

    public GameObject NormalSceneRoot;
    public GameObject ARSceneRoot;

    public OVRPassthroughLayer passthroughLayer;

    //Normal mode by default
    private bool isAR = true;


    // For whatever reason, on simulator (maybe also metaquest), buttons are triggered two times
    // This is probably due to there being two input systems active at the same time, or something similar
    // Therefore we ignore every second call to ToggleAR, which should be sufficient for testing purposes.
    private bool skip = false;

    void Start()
    {           
        if (ARSceneRoot != null)
            passthroughLayer = ARSceneRoot.GetComponentInChildren<OVRPassthroughLayer>();

        NormalSceneRoot.SetActive(!isAR);
        ARSceneRoot.SetActive(isAR);
        if (passthroughLayer != null)
           passthroughLayer.enabled = isAR;
    }

    public void ToggleAR()
    {   
        if (skip)
        {
            skip = false;
            return;
        }
        else
        {
            skip = true;
        }

        isAR = !isAR;

        // Toggle scene roots based on the current mode
        NormalSceneRoot.SetActive(!isAR);
        ARSceneRoot.SetActive(isAR);
        if (passthroughLayer != null)
            passthroughLayer.enabled = isAR;

        Debug.Log("ToggleAR called! isAR = " + isAR);
    }


}
