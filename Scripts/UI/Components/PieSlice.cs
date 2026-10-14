using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public class PieSlice : MaskableGraphic
    {
        private float _startAngle;
        private float _endAngle;
        private float _innerRadius;
        private float _outerRadius;
        private const int Segments = 40;

        public void Setup(float startAngle, float endAngle, float innerRadius, float outerRadius)
        {
            _startAngle = startAngle;
            _endAngle = endAngle;
            _innerRadius = innerRadius;
            _outerRadius = outerRadius;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            float range = _endAngle - _startAngle;
            if (Mathf.Approximately(range, 0f)) return;

            float step = range / Segments;

            // 내/외곽 정점 쌍을 호를 따라 배치
            for (int i = 0; i <= Segments; i++)
            {
                float angle = (_startAngle + step * i) * Mathf.Deg2Rad;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                float u = (float)i / Segments;

                vh.AddVert(
                    new Vector3(cos * _innerRadius, sin * _innerRadius),
                    color,
                    new Vector2(u, 0f));

                vh.AddVert(
                    new Vector3(cos * _outerRadius, sin * _outerRadius),
                    color,
                    new Vector2(u, 1f));
            }

            // 인접한 정점 쌍 4개로 사각형(삼각형 2개) 구성
            for (int i = 0; i < Segments; i++)
            {
                int idx = i * 2;
                vh.AddTriangle(idx, idx + 1, idx + 3);
                vh.AddTriangle(idx, idx + 3, idx + 2);
            }
        }
    }
}