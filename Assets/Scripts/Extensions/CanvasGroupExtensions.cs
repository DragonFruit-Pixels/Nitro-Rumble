using UnityEngine;

namespace Extensions
{
    public static class CanvasGroupExtensions
    {
        public static void ToggleVisibility(this CanvasGroup canvasGroup, bool on)
        {
            canvasGroup.alpha = on ? 1f : 0f;
            canvasGroup.blocksRaycasts = on;
            canvasGroup.interactable = on;
        } 
    }
}