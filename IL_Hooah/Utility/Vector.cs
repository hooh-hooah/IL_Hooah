using UnityEngine;

namespace HooahComponents.Utility
{
    public static class Vector
    {
        public static float SqrDistance(this Vector3 from, Vector3 to)
        {
            return (from - to).sqrMagnitude;
        }

        public static bool IsInRange(this Vector3 from, Vector3 to, float distance)
        {
            return from.SqrDistance(to) <= distance;
        }
    }
}