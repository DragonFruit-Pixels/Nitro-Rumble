using UnityEngine;

namespace Extensions
{
    public static class SpriteExtensions
    {
        public static void ChangeSprite(this SpriteRenderer spriteRenderer, Sprite newSprite)
        {
            if (spriteRenderer && newSprite)
                spriteRenderer.sprite = newSprite;
        } 
    }
}