using UnityEngine;

namespace PixelChroma
{
    // 외곽선 엠보스(하이라이트/쉐도우) 계산의 단일 원본.
    // 실제 인게임 합성(PlayerPaintCanvas.Recomposite)과 설정창 프리뷰(UIEmbossPreview)가
    // 반드시 같은 결과를 내도록 수식을 여기 한곳에 모은다. 여기만 고치면 양쪽이 동시에 바뀐다.
    public static class PaintEmboss
    {
        // 빛 각도(도) → 방향 벡터.
        public static Vector2 LightDir(float angleDeg)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }

        // 외곽선 픽셀 하나의 색(r,g,b, 0~1)에 하이라이트/쉐도우를 적용한다.
        // 내부(법선이 0인) 픽셀은 페인트색 그대로 유지된다.
        public static void Shade(
            ref float r, ref float g, ref float b,
            Vector2 normal, Vector2 lightDir,
            float highlight, float shadow, float strength)
        {
            bool isEdge = normal.x != 0f || normal.y != 0f;
            if (!isEdge) return;

            float edge = Vector2.Dot(normal, lightDir);
            float hi = Mathf.Max(0f, edge) * highlight * strength;
            float sh = Mathf.Max(0f, -edge) * shadow * strength;

            r = Mathf.Lerp(Mathf.Lerp(r, 0f, sh), 1f, hi);
            g = Mathf.Lerp(Mathf.Lerp(g, 0f, sh), 1f, hi);
            b = Mathf.Lerp(Mathf.Lerp(b, 0f, sh), 1f, hi);
        }

        // 실루엣 테두리 픽셀에서 투명한 쪽을 향하는 단위 법선을 계산한다(단일 프레임).
        // 내부/투명 픽셀은 (0,0)으로 남는다 = 엠보스 대상 아님.
        public static Vector2[] BuildEdgeNormals(Color[] pixels, int texWidth, int texHeight)
        {
            var normals = new Vector2[texWidth * texHeight];

            for (int y = 0; y < texHeight; y++)
            {
                for (int x = 0; x < texWidth; x++)
                {
                    int idx = y * texWidth + x;
                    if (pixels[idx].a < 0.01f) continue;

                    float nx = 0f, ny = 0f;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            if (IsTransparentAt(pixels, x + dx, y + dy, texWidth, texHeight))
                            {
                                nx += dx;
                                ny += dy;
                            }
                        }
                    }

                    float len = Mathf.Sqrt(nx * nx + ny * ny);
                    if (len < 0.001f) continue;

                    normals[idx] = new Vector2(nx / len, ny / len);
                }
            }

            return normals;
        }

        private static bool IsTransparentAt(Color[] pixels, int x, int y, int texWidth, int texHeight)
        {
            if (x < 0 || x >= texWidth || y < 0 || y >= texHeight) return true;
            return pixels[y * texWidth + x].a < 0.01f;
        }
    }
}
