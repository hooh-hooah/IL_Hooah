using System.Collections.Generic;
using System.Linq;
using HooahComponents.Serialization.Target;
using Studio;
using BindingFlags = System.Reflection.BindingFlags;

namespace HooahComponents.Serialization
{
    enum ValueType
    {
        Property,
        Field
    }

    public static class Utility
    {
        public static int GetCharacterSceneID(this OCIChar ociChar)
        {
            if (ReferenceEquals(ociChar, null)) return -1;
            return ociChar.oiCharInfo.dicKey;
        }

        public static int GetCharacterIKIndex(this OCIChar ociChar, OCIChar.IKInfo ikInfo)
        {
            if (ReferenceEquals(ociChar, null) || ReferenceEquals(null, ikInfo)) return -1;
            var targetInfos = ociChar.oiCharInfo.ikTarget;
            if (ReferenceEquals(null, targetInfos) || targetInfos.Count <= 0) return -1;
            var info = targetInfos.FirstOrDefault(x => x.Value == ikInfo.targetInfo);
            return info.Equals(default(KeyValuePair<int, OIIKTargetInfo>)) ? info.Key : -1;
        }

        public static OCIChar.IKInfo GetCharacterIKByIndex(this OCIChar ociChar, int index)
        {
            if (ReferenceEquals(ociChar, null) || ociChar.oiCharInfo.ikTarget.TryGetValue(index, out var targetInfo)) return default;
            return ociChar.listIKTarget.FirstOrDefault(info => info.targetInfo.Equals(targetInfo)); // we will see about that.
        }

        public static Dictionary<string, object> GetSaveInfo<T>(T targetObject)
        {
            var result = new Dictionary<string, object>();

            // Get all component properties to save.
            foreach (var propertyInfo in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.GetCustomAttributes(typeof(HooahComponentInfo), false).Length > 0))
            {
                result.Add(propertyInfo.Name, propertyInfo.GetValue(targetObject));
            }

            // Get all component properties to save.
            foreach (var fieldInfo in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.GetCustomAttributes(typeof(HooahComponentInfo), false).Length > 0))
            {
                var value = fieldInfo.GetValue(targetObject);
                result.Add(fieldInfo.Name, fieldInfo.FieldType.IsSubclassOf(typeof(BaseResolverClass)) ? ((BaseResolverClass) value).GetSaveInfo() : value);
            }

            return result;
        }

        public static void AssignInfo<T>(Dictionary<string, object> data, T targetObject)
        {
            foreach (var propertyInfo in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.GetCustomAttributes(typeof(HooahComponentInfo), false).Length > 0 && data.ContainsKey(property.Name)))
            {
                propertyInfo.SetValue(targetObject, data[propertyInfo.Name]);
            }

            foreach (var fieldInfo in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.GetCustomAttributes(typeof(HooahComponentInfo), false).Length > 0 && data.ContainsKey(property.Name)))
            {
                var value = data[fieldInfo.Name];
                if (fieldInfo.FieldType.IsSubclassOf(typeof(BaseResolverClass)))
                {
                    ((BaseResolverClass) fieldInfo.GetValue(targetObject)).AssignSaveInfo(value);
                }
                else
                {
                    fieldInfo.SetValue(targetObject, value);
                }
            }
        }
    }
}