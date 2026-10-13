using UnityEngine;

namespace CraftMine
{
    // UI and board camera share the same letterboxed coordinate space.
    public static class InterfaceLayout
    {
        public const float Width = 1600, Height = 900;
        public static readonly Rect Board = new Rect(40, 205, 1020, 530);
        public static float Scale(int width, int height) => Mathf.Min(width / Width, height / Height);

        public static Rect BoardPixels(int width, int height)
        {
            float scale = Scale(width, height);
            return new Rect((width - Width * scale) / 2 + Board.x * scale,
                (height - Height * scale) / 2 + (Height - Board.yMax) * scale,
                Board.width * scale, Board.height * scale);
        }
    }
}
