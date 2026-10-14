using UnityEngine;

namespace PixelChroma
{
    public static class PaintColorUtil
    {
        public static Color32 ToOpaqueColor32(Color color)
        {
            return new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.r * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.g * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.b * 255f), 0, 255),
                255);
        }

        public static bool Equals(Color32 a, Color32 b)
        {
            return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
        }

        public static bool Approximately(Color a, Color b, float tolerance = 0.01f)
        {
            return Mathf.Abs(a.r - b.r) < tolerance
                   && Mathf.Abs(a.g - b.g) < tolerance
                   && Mathf.Abs(a.b - b.b) < tolerance
                   && Mathf.Abs(a.a - b.a) < tolerance;
        }
    }
}
