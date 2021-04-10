using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AIChara;
using BepInEx.Logging;
using HarmonyLib;
using HooahComponents.Utility;
using JetBrains.Annotations;
using Studio;
using UnityEngine;

public static class SkinnedAccessoryHook
{
    private static CoroutineFields _fields;
    public static ManualLogSource Logger { get; set; }

    public static void YeetOutHierarchy(OCIChar _ociChar, Transform _transformRoot, Dictionary<int, Info.BoneInfo> _dicBoneInfo)
    {
        foreach (var accessory in _ociChar.charInfo.cmpAccessory)
        {
            if (accessory == null) continue;
            var skinnedAccessory = accessory.GetComponent<SkinnedAccessory>();
            if (skinnedAccessory == null) continue;
            skinnedAccessory.skeleton.transform.parent = null;
        }

        // The joint corrections/expressions initialized here are the one bone scan that happens before this hook can run
        // Rerunning the scan below fixes the issue. This corrects issues with joint correction and skinned accessory wearing characters.
        Component.Destroy(_ociChar.charInfo.objRoot.GetComponent(typeof(CharaUtils.Expression)));
        _ociChar.charInfo.InitializeExpression(_ociChar.sex);
    }

    // Much faster if we hang onto these
    private static Type MoreAccessoriesHookType;
    private static MethodInfo MoreAccessoriesGetCmpAccessoryMethodInfo;

    public static void RegisterHook()
    {
        var harmony = new Harmony("IL_HooahSkinnedAccessory");

        // To prevent accessory to slow down recursive hierarchy traversal.
        harmony.Patch(AccessTools.Method(typeof(AddObjectAssist), "InitBone"), new HarmonyMethod(typeof(SkinnedAccessoryHook), nameof(YeetOutHierarchy)));

        // Find Specific Coroutine Type with parameter.
        _fields = typeof(ChaControl)
            .GetNestedTypes(AccessTools.all)
            .Where(x => x.Name.StartsWith("<ChangeAccessoryAsync>"))
            .Select(x => new CoroutineFields(x)).FirstOrDefault(x => x.Valid);

        if (_fields == null || !_fields.Valid)
        {
#if DEBUG
            Logger.LogMessage("Failed to find Accessory Initialization Coroutine! Aborting Skinned Accessory Hooking Procedure.");
#endif
            return;
        }

             harmony.Patch(AccessTools.Method(_fields.Type, "MoveNext"), null,
                 new HarmonyMethod(typeof(SkinnedAccessoryHook), nameof(RegisterQueue)));

        // Hook More Accessories
        MoreAccessoriesHookType = AccessTools.TypeByName("MoreAccessoriesAI.Patches.ChaControl_Patches");
        if (MoreAccessoriesHookType != null)
        {
            MoreAccessoriesGetCmpAccessoryMethodInfo = AccessTools.Method(MoreAccessoriesHookType, "GetCmpAccessory");
            harmony.Patch(AccessTools.Method(MoreAccessoriesHookType, "ChangeAccessoryAsync_Prefix"), null, new HarmonyMethod(typeof(SkinnedAccessoryHook), nameof(ChangeAccessoryAsync_Prefix)));
#if DEBUG
            Logger.LogInfo("Hooked More Accessories");
#endif
        }
        else
        {
#if DEBUG
            Logger.LogInfo("More Accessories Not Found");
#endif
        }

#if DEBUG
        Logger.LogMessage("Successfully Hooked the Skinned Accessory Initializer.");
#endif
    }

    public static void ChangeAccessoryAsync_Prefix(ChaControl __0, int slotNo)
    {
        try
        {
            var accessory = (CmpAccessory)MoreAccessoriesGetCmpAccessoryMethodInfo.Invoke(null, new object[] { __0, slotNo + 20 });            
            ProcessForSkinnedAccessory(__0, accessory, slotNo + 20);
        }
        catch (Exception e)
        {
            // I hope you dont see this one ever again.
            Logger.LogError("Failed to attach More Accessory SkinnedAccessory to the character controller!");
            Logger.LogError(e.Message);
            Logger.LogError(e.StackTrace);
        }
    }

    public static void RegisterQueue(object __instance)
    {
        if (__instance.GetType() != _fields.Type)
        {
            Logger.LogError("SkinnedAccessory hook is not called by correct coroutine class!");
            return;
        }

        try
        {
            var chaControl = (ChaControl) _fields.ChaControl.GetValue(__instance);
            if (chaControl == null) throw new Exception("Unable to find character controller.");

            var slotId = (int) _fields.SlotNo.GetValue(__instance);
            if (slotId < 0) throw new Exception("Unable to obtain accessory slot id from the coroutine.");

            var accessory = chaControl.cmpAccessory[slotId];
            ProcessForSkinnedAccessory(chaControl, accessory, slotId);

        }
        catch (Exception e)
        {
            // I hope you dont see this one ever again.
            Logger.LogError("Failed to attach SkinnedAccessory to the character controller!");
            Logger.LogError(e.Message);
            Logger.LogError(e.StackTrace);
        }
    }

    private static void ProcessForSkinnedAccessory(ChaControl chaControl, CmpAccessory accessory, int slotId)
    {
        if (accessory == null)
        {
#if DEBUG
            throw new Exception($"Failed to find corrent accessory slot {slotId}.");
#endif
            return;
        }

        var gameObject = accessory.gameObject;
        if (gameObject == null)
        {
#if DEBUG
            throw new Exception($"Unable to find GameObject from the CmpAccessory Component {slotId}!");
#endif

            return;
        }

        var skinnedAccessory = gameObject.GetComponent<SkinnedAccessory>();
        if (skinnedAccessory == null)
        {
#if DEBUG
            throw new Exception($"Unable to find Skinned Accesory. {slotId}");
#endif
            return;
        }

        skinnedAccessory.Merge(chaControl);
    }

    private class CoroutineFields
    {
        public readonly FieldInfo ChaControl;
        public readonly FieldInfo SlotNo;
        public readonly Type Type;
        public readonly bool Valid;

        public CoroutineFields(Type type)
        {
            Type = type;
            var fields = type.GetFields(AccessTools.all);
            foreach (var fieldInfo in fields)
            {
                if (fieldInfo.Name == "slotNo" && fieldInfo.FieldType.Name == "Int32")
                {
                    SlotNo = fieldInfo;
                    if (ChaControl == null) continue;
                    Valid = true;
                    break;
                }

                if (fieldInfo.FieldType.Name == "ChaControl" && fieldInfo.Name.Contains("this"))
                {
                    ChaControl = fieldInfo;
                    if (SlotNo == null) continue;
                    Valid = true;
                    break;
                }
            }
        }
    }
}

[DisallowMultipleComponent]
public class SkinnedAccessory : MonoBehaviour
{
    private static readonly Bounds bound = new Bounds(new Vector3(0f, 10f, 0f), new Vector3(20f, 20f, 20f));
    public List<SkinnedMeshRenderer> meshRenderers;
    public GameObject skeleton;
    private ChaControl _chaControl;
    private int _done;

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
            smr.bones = smr.bones
                .Select(boneTransform =>
                    !ReferenceEquals(boneTransform, null) && dict.TryGetValue(boneTransform.name, out var bone) ? bone : null
                )
                .ToArray();
            smr.enabled = true;
            smr.localBounds = bound;
        }
        finally
        {
            _done++;
            if (_done == meshRenderers.Count) Destroy(skeleton); // 😂👌
        }

        yield break;
    }
}
