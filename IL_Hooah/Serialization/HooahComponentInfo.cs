using System;

// To get class's saving information.
namespace HooahComponents.Serialization
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class HooahComponentInfo : Attribute
    {
        public HooahComponentInfo()
        {
        }
    }
}