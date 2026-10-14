using UnityEngine;

namespace PixelChroma.UI
{
    // 엠보스 프리뷰의 배경/캐릭터 색을 대표 테스트 색상으로 빠르게 전환한다.
    public class UIEmbossColorSelector : MonoBehaviour
    {
        [SerializeField] private UIEmbossPreview preview;

        private UIEmbossColorPreset[] _presets;
        private bool _initialized;

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            _presets = GetComponentsInChildren<UIEmbossColorPreset>(true);
            foreach (var preset in _presets)
                preset.Initialize(OnPresetSelected);

            ApplyDefaultColor();
        }

        public void Bind(UIEmbossPreview targetPreview)
        {
            preview = targetPreview;
            ApplyDefaultColor();
        }

        private void ApplyDefaultColor()
        {
            var preset = GetFirstPreset();
            ApplyColor(preset != null ? preset.Color : GetDefaultColor());
        }

        private UIEmbossColorPreset GetFirstPreset()
        {
            if (_presets == null || _presets.Length == 0)
                return null;

            return _presets[0];
        }

        private Color GetDefaultColor()
        {
            return preview != null ? preview.PaintColor : Color.white;
        }

        private void OnPresetSelected(UIEmbossColorPreset preset)
        {
            if (preset == null) return;

            ApplyColor(preset.Color);
        }

        private void ApplyColor(Color color)
        {
            if (preview != null)
                preview.SetPaintColor(color);
        }
    }
}
