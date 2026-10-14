using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelChroma
{
    /// <summary>
    /// UI, 렌더링, 네트워크에 의존하지 않는 공통 픽셀 페인트 규칙입니다.
    /// 실제 픽셀 쓰기와 히스토리 기록은 각 호출자가 담당합니다.
    /// </summary>
    public static class PaintOps
    {
        /// <summary>
        /// 브러시가 덮는 클램프 전 반열린 셀 구간 [min, max)을 반환합니다.
        /// 짝수 크기는 기존 규칙대로 중심 셀의 좌하단 쪽으로 한 칸 더 확장됩니다.
        /// </summary>
        public static void GetBrushRect(int cx, int cy, int size,
            out int minX, out int minY, out int maxX, out int maxY)
        {
            int half = size / 2;
            minX = cx - half;
            minY = cy - half;
            maxX = cx + size - half;
            maxY = cy + size - half;
        }

        /// <summary>
        /// 브러시 구간을 [0,width) x [0,height)로 클램프합니다.
        /// </summary>
        public static bool GetBrushBounds(int cx, int cy, int size, int width, int height,
            out int x0, out int y0, out int x1, out int y1)
        {
            GetBrushRect(cx, cy, size, out int minX, out int minY, out int maxX, out int maxY);
            x0 = Mathf.Max(minX, 0);
            y0 = Mathf.Max(minY, 0);
            x1 = Mathf.Min(maxX, width);
            y1 = Mathf.Min(maxY, height);
            return x0 < x1 && y0 < y1;
        }

        /// <summary>
        /// 시작 셀과 같은 색으로 4방향 연결된 영역의 인덱스를 수집합니다.
        /// 픽셀 배열은 수정하지 않으며, 스크래치 버퍼는 매 호출 내부에서 초기화합니다.
        /// </summary>
        public static int CollectFloodRegion(
            Color32[] cells,
            int width,
            int height,
            int startIdx,
            bool[] canPaint,
            byte channelTolerance,
            bool[] visited,
            Stack<int> stack,
            List<int> result)
        {
            if (stack == null) throw new ArgumentNullException(nameof(stack));
            if (result == null) throw new ArgumentNullException(nameof(result));

            stack.Clear();
            result.Clear();

            if (cells == null || width <= 0 || height <= 0)
                return 0;

            int cellCount = width * height;
            if (visited == null || visited.Length < cellCount)
                throw new ArgumentException("Visited buffer is smaller than the paint surface.", nameof(visited));

            Array.Clear(visited, 0, cellCount);

            if (cells.Length < cellCount)
                return 0;
            if (startIdx < 0 || startIdx >= cellCount)
                return 0;
            if (canPaint != null && (canPaint.Length < cellCount || !canPaint[startIdx]))
                return 0;

            Color32 target = cells[startIdx];
            visited[startIdx] = true;
            stack.Push(startIdx);

            while (stack.Count > 0)
            {
                int idx = stack.Pop();
                result.Add(idx);

                int x = idx % width;
                int y = idx / width;

                if (x > 0)
                    TryPush(idx - 1, cells, target, canPaint, channelTolerance, visited, stack);
                if (x < width - 1)
                    TryPush(idx + 1, cells, target, canPaint, channelTolerance, visited, stack);
                if (y > 0)
                    TryPush(idx - width, cells, target, canPaint, channelTolerance, visited, stack);
                if (y < height - 1)
                    TryPush(idx + width, cells, target, canPaint, channelTolerance, visited, stack);
            }

            return result.Count;
        }

        private static void TryPush(
            int idx,
            Color32[] cells,
            Color32 target,
            bool[] canPaint,
            byte channelTolerance,
            bool[] visited,
            Stack<int> stack)
        {
            if (visited[idx]) return;
            visited[idx] = true;

            if (canPaint != null && !canPaint[idx]) return;
            if (!ColorsMatch(cells[idx], target, channelTolerance)) return;

            stack.Push(idx);
        }

        private static bool ColorsMatch(Color32 a, Color32 b, byte tolerance)
        {
            if (tolerance == 0)
                return PaintColorUtil.Equals(a, b);

            return Math.Abs(a.r - b.r) <= tolerance
                && Math.Abs(a.g - b.g) <= tolerance
                && Math.Abs(a.b - b.b) <= tolerance
                && Math.Abs(a.a - b.a) <= tolerance;
        }
    }
}
