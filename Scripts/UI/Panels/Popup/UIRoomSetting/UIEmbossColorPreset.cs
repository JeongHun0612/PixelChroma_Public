using System;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    // 엠보스 프리뷰 배경 테스트에 사용할 색상 버튼 하나를 담당한다.
    public class UIEmbossColorPreset : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image swatchImage;

        public Color Color => swatchImage != null ? swatchImage.color : Color.white;

        public void Initialize(Action<UIEmbossColorPreset> onSelected)
        {
            if (button == null) button = GetComponent<Button>();
            if (swatchImage == null) swatchImage = GetComponent<Image>();

            if (button == null) return;

            button.AddClickListener(() => onSelected?.Invoke(this));
        }
    }
}
