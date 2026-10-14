using UnityEngine;

namespace PixelChroma
{
    /// <summary>
    /// 인게임과 닉네임 편집기가 공유하는 브러시 프리뷰 스타일과 텍스처 레시피입니다.
    /// </summary>
    public static class PaintBrushStyle
    {
        public const int SubRes = 6;

        public static readonly Color Outline = new Color(0f, 0f, 0f, 0.8f);
        public static readonly Color Fill = new Color(0.5f, 0.5f, 0.5f, 0.3f);
        public static readonly Color EraserFill = new Color(1f, 0.2f, 0.2f, 0.3f);

        public static Texture2D CreateBrushTexture(int size)
        {
            int brushCells = Mathf.Max(size, 1);
            int textureSize = brushCells * SubRes;
            var texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[textureSize * textureSize];
            Color32 outline = Outline;
            Color32 fill = Fill;

            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    bool isBorder = x == 0 || y == 0 || x == textureSize - 1 || y == textureSize - 1;
                    pixels[y * textureSize + x] = isBorder ? outline : fill;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        public static void StampBrush(
            Texture2D target,
            Color32[] clearBuffer,
            int cellX,
            int cellY,
            int size,
            int cellsWide,
            int cellsHigh)
        {
            if (target == null || clearBuffer == null) return;

            int textureWidth = cellsWide * SubRes;
            int textureHeight = cellsHigh * SubRes;
            if (clearBuffer.Length != textureWidth * textureHeight
                || target.width != textureWidth
                || target.height != textureHeight)
                return;

            System.Array.Clear(clearBuffer, 0, clearBuffer.Length);
            PaintOps.GetBrushRect(cellX, cellY, size,
                out int minCellX, out int minCellY, out int maxCellX, out int maxCellY);

            int minX = minCellX * SubRes;
            int minY = minCellY * SubRes;
            int maxX = maxCellX * SubRes - 1;
            int maxY = maxCellY * SubRes - 1;
            Color32 outline = Outline;
            Color32 fill = Fill;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (x < 0 || x >= textureWidth || y < 0 || y >= textureHeight)
                        continue;

                    bool isBorder = x == minX || x == maxX || y == minY || y == maxY;
                    clearBuffer[y * textureWidth + x] = isBorder ? outline : fill;
                }
            }

            target.SetPixels32(clearBuffer);
            target.Apply();
        }
    }
}
