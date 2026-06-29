using UnityEngine;

namespace Extensions
{
    public static class StringExtensions
    {
        public static bool IsEmpty(this string s)
        {
            return s.Length == 0;
        }
    }
}
