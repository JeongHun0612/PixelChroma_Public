using UnityEngine;

namespace PixelChroma.UI
{
    public static class PaintToolCursor
    {
        public static void Apply(BrushMode mode, int brushSize, Color paintColor)
        {
            if (CursorManager.Instance == null) return;

            // 통 커서를 세팅하기 전에 물감 색부터 맞춘다.
            CursorManager.Instance.SetPaintColor(paintColor);

            float brushScale = 1f + (brushSize * 0.1f);

            switch (mode)
            {
                case BrushMode.Eyedropper:
                    CursorManager.Instance.SetCursorType(CursorType.Eyedropper);
                    break;
                case BrushMode.Fill:
                    CursorManager.Instance.SetCursorType(CursorType.Fill);
                    break;
                case BrushMode.Eraser:
                    CursorManager.Instance.SetCursorType(CursorType.Eraser, brushScale);
                    break;
                case BrushMode.Brush:
                default:
                    CursorManager.Instance.SetCursorType(CursorType.Brush, brushScale);
                    break;
            }
        }
    }
}
