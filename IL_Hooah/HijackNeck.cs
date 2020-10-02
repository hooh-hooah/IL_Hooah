using System.Collections;
using AIChara;
using UnityEngine;

public class HijackNeck : MonoBehaviour
{
    private ChaControl chaControl;
    private NeckLookControllerVer2 lookAtController;
    private Transform originalTransform;

    private void Awake()
    {
        StartCoroutine("FindTarget");
    }

    private void OnDestroy()
    {
        StopCoroutine("FindTarget");
    }

    private IEnumerator FindTarget()
    {
        while (true)
        {
            yield return new WaitForSeconds(.5f);
            chaControl = GetComponentInParent<ChaControl>();

            if (chaControl != null)
            {
                lookAtController = chaControl.neckLookCtrl;
                if (lookAtController != null)
                {
                    if (originalTransform == null)
                    {
                        originalTransform = lookAtController.target;
                    }

                    lookAtController.target = enabled ? transform : Camera.main.transform;
                }
            }
            else
            {
                if (originalTransform != null && lookAtController != null)
                {
                    lookAtController.target = originalTransform;
                }

                lookAtController = null;
                originalTransform = null;
            }
        }
    }

    private void Update()
    {
    }
}