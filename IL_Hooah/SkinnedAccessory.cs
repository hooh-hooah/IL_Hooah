using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AIChara;
using HooahComponents.Utility;
using JetBrains.Annotations;
using UnityEngine;

[DisallowMultipleComponent]
public class SkinnedAccessory : MonoBehaviour
{
    private static readonly Bounds bound = new Bounds(new Vector3(0f, 10f, 0f), new Vector3(20f, 20f, 20f));
    private ChaControl _chaControl;
    private int _done;
    public List<SkinnedMeshRenderer> meshRenderers;
    public GameObject skeleton;

    private void Start()
    {
        _chaControl = GetComponentInParent<ChaControl>();

        // When it's initialized without character control. 
        if (ReferenceEquals(null, _chaControl))
        {
            enabled = false;
            return;
        }

        TryMerge();
    }

    private void TryMerge()
    {
        // TryGetSkinnedBones includes dictionary check and chaControl checks.
        if (ReferenceEquals(_chaControl, null) || !SkinnedBones.TryGetSkinnedBones(_chaControl, out var dict)) return;
        meshRenderers.ForEach(smr =>
        {
            smr.enabled = false;
            smr.rootBone = _chaControl.objBodyBone.transform;
            StartCoroutine(MergeCoroutine(smr, dict));
        });
    }

    private IEnumerator MergeCoroutine(SkinnedMeshRenderer smr, [NotNull] IReadOnlyDictionary<string, Transform> dict)
    {
        // Sometimes it's not really null.
        // So gotta double check object wise and unity wise. it looks dumb tbh
        smr.bones = smr.bones
            .Select(boneTransform => dict.TryGetValue(boneTransform.name, out var bone) ? bone : null)
            .ToArray();
        smr.enabled = true;
        smr.localBounds = bound;

        // well shit if i could track coroutines like god damn async
        _done++;
        if (_done == meshRenderers.Count) DestroyImmediate(skeleton);
        yield break;
    }
}