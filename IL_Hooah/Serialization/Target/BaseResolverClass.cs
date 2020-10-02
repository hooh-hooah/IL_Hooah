namespace HooahComponents.Serialization.Target
{
    public abstract class BaseResolverClass
    {
        public abstract object GetSaveInfo();
        public abstract void AssignSaveInfo(object info);
    }
}