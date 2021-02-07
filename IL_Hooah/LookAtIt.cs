using System.Collections;
using UnityEngine;

public class LookAtIt : MonoBehaviour
{
    public GameObject turnObject;
    private GameObject target;

    // Use this for initialization
    private void Awake()
    {
        StartCoroutine(nameof(FindTarget));
    }

    // Update is called once per frame
    private void Update()
    {
        if (target != null) turnObject.transform.LookAt(target.transform);
    }

    private void OnDestroy()
    {
        StopCoroutine(nameof(FindTarget));
    }

    private IEnumerator FindTarget()
    {
        while (true)
        {
            yield return new WaitForSeconds(.5f);

            if (target != null)
            {
                var lam = gameObject.GetComponentInChildren<LookAtMe>();
                if (lam == null || lam.gameObject != target) target = null;
            }

            if (target == null)
            {
                var lam = gameObject.GetComponentInChildren<LookAtMe>();
                if (lam != null) target = lam.gameObject;
            }
        }
    }
}
