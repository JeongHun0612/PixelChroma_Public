using UnityEngine;
using UnityEngine.UI;    // ← 추가
using OneText.UGUI;

namespace PixelChroma.UI
{
    public class IndicatorIconView : MonoBehaviour
    {
        [SerializeField] private RectTransform ringArrow;
        [SerializeField] private OneTextLabel nicknameText;
        [SerializeField] private RawImage pixelNicknameImage; 

        private Texture2D _cachedTex;  

        public RectTransform Slot => (RectTransform)transform;
        public RectTransform RingArrow => ringArrow;

        public void SetNickname(string name)
        {
            if (nicknameText != null)
                nicknameText.text = name;
        }

        // PixelNickname 우선, 없으면 텍스트 폴백
        public void SetNicknameData(string textName, byte[] pixelPng)
        {
            bool hasPixel = pixelPng != null && pixelPng.Length > 0;

            // 픽셀 닉네임 처리
            if (pixelNicknameImage != null)
            {
                if (hasPixel)
                {
                    _cachedTex = PixelNicknameHelper.ApplyPng(
                        pixelNicknameImage, pixelPng, _cachedTex);
                    pixelNicknameImage.gameObject.SetActive(true);
                }
                else
                {
                    pixelNicknameImage.gameObject.SetActive(false);
                }
            }

            // 텍스트 닉네임 처리 (픽셀이 있으면 숨김)
            if (nicknameText != null)
            {
                nicknameText.gameObject.SetActive(!hasPixel);
                if (!hasPixel)
                    nicknameText.text = textName;
            }
        }

        // ★ 캐시 텍스처 정리
        private void OnDestroy()
        {
            if (_cachedTex != null)
                Destroy(_cachedTex);
        }
    }
}