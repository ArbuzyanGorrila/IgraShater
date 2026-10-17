using UnityEngine;

namespace CraftMine
{
    // UI and board camera share the same letterboxed coordinate space.
    public static class InterfaceLayout
    {
        public const float Width = 1600, Height = 900;
        public static readonly Rect Board = new Rect(40, 205, 1020, 530);
        public static readonly Rect GameplayBoard = new Rect(40, 156, 1520, 590);
        public static float Scale(int width, int height) => Mathf.Min(width / Width, height / Height);

        public static Rect BoardPixels(int width, int height, bool gameplay = false)
        {
            float scale = Scale(width, height);
            Rect board = gameplay ? GameplayBoard : Board;
            return new Rect((width - Width * scale) / 2 + board.x * scale,
                (height - Height * scale) / 2 + (Height - board.yMax) * scale,
                board.width * scale, board.height * scale);
        }
    }
}
