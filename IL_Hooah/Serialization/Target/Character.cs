using AIChara;

namespace HooahComponents.Serialization.Target
{
    public class Character
    {
        private ChaControl _target;
        public void SetTarget(ChaControl target)
        {
            this._target = target;
        }
    }
}