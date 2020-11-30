using System.Collections.Generic;
using HarmonyLib;
using AIChara;
using Studio;
using UnityEngine;

namespace HooahComponents.Hooks
{
    public partial class Hooks
    {
        [HarmonyPrefix, HarmonyPatch(typeof(Studio.Studio), "OnParentage")]
        public static void OnParentChanged(Studio.Studio __instance, TreeNodeObject _parent, TreeNodeObject _child)
        {
            if (!_parent) OnDetach(__instance, _child);
            else OnAttach(__instance, _parent, _child);
        }

        private static bool TryFindCharacter(Studio.Studio studioInstance, TreeNodeObject node, out ChaControl result)
        {
            var findNode = node;

            // Find until it hits the target.
            while (!ReferenceEquals(null, findNode))
            {
                if (studioInstance.dicInfo.TryGetValue(findNode, out var info) && (info is OCICharFemale || info is OCICharMale))
                {
                    if (info is OCICharFemale female) result = female.female;
                    else if (info is OCICharMale male) result = male.male;
                    else result = null; // impossible but mandatory.

                    return true;
                }

                findNode = findNode.parent;
            }

            result = null;
            return false;
        }

        public static bool TryGetAnimator(ChaControl chaControl, out Animator animator)
        {
            animator = null;
            if (chaControl == null || chaControl.objHead == null || chaControl.objHead.transform == null)
                return false;

            var transform = chaControl.objHead.transform;
            // TODO: find N_ something if it failed to find something.
            var target = transform.GetChild(0);
            if (target == null)
                return false;

            if (Animators.ContainsKey(target.gameObject))
                return false;
            animator = target.GetOrAddComponent<Animator>();
            Animators.Add(target.gameObject, animator);
            return true;
        }

        public static bool TryGetItem(Studio.Studio instance, TreeNodeObject child, out Transform transform)
        {
            if (!instance.dicInfo.TryGetValue(child, out var info))
            {
                transform = null;
                return false;
            }

            switch (info)
            {
                case OCIItem ociItem when ociItem.itemComponent != null:
                    transform = ociItem.itemComponent.transform;
                    return true;
                default:
                    transform = null;
                    return false;
            }
        }

        // TODO: it's not getting called - despite of conditions.
        public static void OnDetach(Studio.Studio instance, TreeNodeObject child)
        {
            // on parent is cleared... 
            // check child's parent.
            // find character and clear animation controller on the face.
            if (!TryFindCharacter(instance, child, out var chaControl) || !TryGetAnimator(chaControl, out var animator)) return;
            animator.runtimeAnimatorController = null;
            animator.enabled = false;
        }

        public static void OnAttach(Studio.Studio instance, TreeNodeObject parent, TreeNodeObject child)
        {
            // on being parented.
            // take care if parent is not part of god damn character.
            if (!TryFindCharacter(instance, parent, out var chaControl) || !TryGetAnimator(chaControl, out var animator) || !TryGetItem(instance, child, out var transform)) return;
            // transfer serialized controller
            animator.enabled = false;
            if (!transform.name.Contains("container_")) return;
            var comAnim = transform.GetComponent<Animator>();
            if (comAnim != null) animator.runtimeAnimatorController = comAnim.runtimeAnimatorController;
        }

        private static readonly Dictionary<GameObject, Animator> Animators = new Dictionary<GameObject, Animator>();

        [HarmonyPostfix]
        [HarmonyPatch(typeof(FaceBlendShape), "OnLateUpdate")]
        public static void LateUpdateLate(FaceBlendShape __instance)
        {
            if (__instance.gameObject == null || __instance.gameObject.transform.GetChild(0) == null) return;
            var headObject = __instance.gameObject.transform.GetChild(0).gameObject;
            if (Animators.TryGetValue(headObject, out var animator) && animator.runtimeAnimatorController != null)
                animator.Update(Time.deltaTime);
        }
    }
}