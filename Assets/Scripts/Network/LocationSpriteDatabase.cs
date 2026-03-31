using System;
using UnityEngine;

namespace Kotatsu.Network
{
    public enum LocationVisualKind
    {
        Start,
        Home,
        Straight,
        Sasuke,
        Animal,
        Mountain
    }

    [CreateAssetMenu(fileName = "LocationSpriteDatabase", menuName = "Kotatsu/Location Sprite Database")]
    public class LocationSpriteDatabase : ScriptableObject
    {
        [Serializable]
        private struct PlayerLocationSprites
        {
            public Sprite start;
            public Sprite home;
            public Sprite straight;
            public Sprite sasuke;
            public Sprite animal;
            public Sprite mountain;
            public Sprite gameBackground;
        }

        [SerializeField] private PlayerLocationSprites[] playerSprites = new PlayerLocationSprites[4];

        public Sprite GetSprite(int colorIndex, LocationVisualKind kind)
        {
            if (playerSprites == null || playerSprites.Length == 0)
            {
                return null;
            }

            int safeIndex = Mathf.Clamp(colorIndex, 0, playerSprites.Length - 1);
            PlayerLocationSprites sprites = playerSprites[safeIndex];

            return kind switch
            {
                LocationVisualKind.Start => sprites.start,
                LocationVisualKind.Home => sprites.home,
                LocationVisualKind.Straight => sprites.straight,
                LocationVisualKind.Sasuke => sprites.sasuke,
                LocationVisualKind.Animal => sprites.animal,
                LocationVisualKind.Mountain => sprites.mountain,
                _ => null
            };
        }

        public Sprite GetGameBackground(int colorIndex)
        {
            if (playerSprites == null || playerSprites.Length == 0)
            {
                return null;
            }

            int safeIndex = Mathf.Clamp(colorIndex, 0, playerSprites.Length - 1);
            return playerSprites[safeIndex].gameBackground;
        }
    }
}
