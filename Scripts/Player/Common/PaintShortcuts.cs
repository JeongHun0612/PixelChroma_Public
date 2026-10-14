namespace PixelChroma
{
    /// <summary>
    /// 페인트 도구가 공유하는 입력 매핑만 해석합니다.
    /// 드래그 차단, 실행 가능 여부, 값 클램프는 호출자가 결정합니다.
    /// </summary>
    public static class PaintShortcuts
    {
        public static bool TryReadTool(PlayerInputActions input, out BrushMode mode)
        {
            mode = BrushMode.Brush;
            if (input == null) return false;

            if (input.SelectBrush != null && input.SelectBrush.WasPressedThisFrame())
                mode = BrushMode.Brush;
            else if (input.SelectEraser != null && input.SelectEraser.WasPressedThisFrame())
                mode = BrushMode.Eraser;
            else if (input.SelectFill != null && input.SelectFill.WasPressedThisFrame())
                mode = BrushMode.Fill;
            else
                return false;

            return true;
        }

        public static int ReadHistory(PlayerInputActions input)
        {
            if (input == null) return 0;

            // Ctrl+Shift+Z가 Undo 조건도 만족하므로 Redo를 먼저 판정한다.
            if (input.Redo != null && input.Redo.WasPressedThisFrame()) return 1;
            if (input.Undo != null && input.Undo.WasPressedThisFrame()) return -1;
            return 0;
        }

        public static int ReadSizeDelta(float scroll)
        {
            if (scroll > 0f) return 1;
            if (scroll < 0f) return -1;
            return 0;
        }
    }
}
