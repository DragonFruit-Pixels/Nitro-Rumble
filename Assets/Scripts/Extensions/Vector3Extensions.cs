using UnityEngine;

namespace Extensions
{
    public static class Vector3Extensions
    {
        public static Vector3 NoY(this Vector3 v) => new Vector3(v.x, 0, v.z);
    }
}