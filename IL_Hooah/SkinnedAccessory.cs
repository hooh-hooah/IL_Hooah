using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AIChara;
using BepInEx.Harmony;
using HarmonyLib;
using HooahComponents.Utility;
using JetBrains.Annotations;
using UnityEngine;

public static class SkinnedAccessoryHook
{
    public static void RegisterHook()
    {
        var harmony = new Harmony("IL_HooahSkinnedAccessory");
        harmony.Patch(AccessTools.Method(typeof(ChaControl).GetNestedType("<ChangeAccessoryAsync>c__Iterator12", AccessTools.all), "MoveNext"),
            null,
            new HarmonyMethod(typeof(SkinnedAccessoryHook), nameof(RegisterQueue)),
            null);
    }

    public static void RegisterQueue(object __instance)
    {
        var traverse = Traverse.Create(__instance);
        var chaControl = traverse.Field("$this")?.GetValue<ChaControl>();
        if (chaControl == null) return;

        var slotField = Traverse.Create(__instance)?.Field("$locvar0");
        if (slotField == null) return;

        var slotValue = slotField.Field<int>("slotNo").Value;
        if (slotValue < 0) return;

        try
        {
            var accessory = chaControl.cmpAccessory[slotValue];
            if (accessory == null) return;

            var gameObject = accessory.gameObject;
            if (gameObject == null) return;

            var skinnedAccessory = gameObject.GetComponent<SkinnedAccessory>();
            if (skinnedAccessory == null) return;

            skinnedAccessory.Merge(chaControl);
        }
        finally
        {
            // register leftover 
        }
    }
}

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
        // StartCoroutine(nameof(TryMerge));
    }

    public void Merge(ChaControl chaControl)
    {
        StartCoroutine(TryMerge(chaControl));
    }

    private IEnumerator TryMerge(ChaControl _chaControl)
    {
        if (ReferenceEquals(_chaControl, null) || !SkinnedBones.TryGetSkinnedBones(_chaControl, out var dict)) yield break;
        meshRenderers.ForEach(smr =>
        {
            smr.enabled = false;
            smr.rootBone = _chaControl.objBodyBone.transform;
            StartCoroutine(MergeCoroutine(smr, dict));
        });
    }

    private IEnumerator MergeCoroutine(SkinnedMeshRenderer smr, [NotNull] IReadOnlyDictionary<string, Transform> dict)
    {
        try
        {
            // Sometimes it's not really null.
            // So gotta double check object wise and unity wise. it looks dumb tbh
            smr.bones = smr.bones
                .Select(boneTransform =>
                    !ReferenceEquals(boneTransform, null) && dict.TryGetValue(boneTransform.name, out var bone)
                        ? bone
                        : null
                )
                .ToArray();
            smr.enabled = true;
            smr.localBounds = bound;

            // well shit if i could track coroutines like god damn async
        }
        finally
        {
            _done++;
            if (_done == meshRenderers.Count) Destroy(skeleton);
        }

        yield break;
    }
}