using System;
using Studio;
using UnityEngine;

namespace HooahComponents.Serialization.Target
{
    public class ResolverCharacterIK : BaseResolverClass
    {
        private OCIChar _charInfo;
        private OCIChar.IKInfo _info;

        public OCIChar.IKInfo GetTarget()
        {
            return _info;
        }

        public Transform GetTransform()
        {
            return _info.gameObject.transform;
        }

        public void SetTarget(OCIChar info, OCIChar.IKInfo ikTarget)
        {
            _charInfo = info;
            _info = ikTarget;
        }

        public override object GetSaveInfo()
        {
            // characterDictionaryID, characterDictionaryKey
            return $"{_charInfo.GetCharacterSceneID()},{_charInfo.GetCharacterIKIndex(_info)}";
        }

        public override void AssignSaveInfo(object data)
        {
            if (!(data is string info) || info.IsNullOrEmpty()) return;
            var split = info.Split(',');
            if (split.Length == 2 && int.TryParse(split[0], out var charID) && int.TryParse(split[1], out var ikID))
            {
                if (!Singleton<Studio.Studio>.IsInstance()) return;

                if (Singleton<Studio.Studio>.Instance.dicObjectCtrl.TryGetValue(charID, out var objectInfo) && objectInfo is OCIChar charInfo)
                {
                    _charInfo = charInfo;
                    _info = charInfo.GetCharacterIKByIndex(ikID);
                }
            }
        }
    }
}