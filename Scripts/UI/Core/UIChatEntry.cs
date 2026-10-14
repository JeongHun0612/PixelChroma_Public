using OneText.UGUI;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    public class UIChatEntry : MonoBehaviour
    {
        [SerializeField] private RawImage nicknameImage;       // 그림 닉네임
        [SerializeField] private OneTextLabel nicknameText;     // ★ 텍스트 닉네임 추가
        [SerializeField] private OneTextLabel messageText;      // ": 메시지" 본문

        private CanvasGroup _canvasGroup;
        private Texture2D _cachedTexture;

        public CanvasGroup CanvasGroup
        {
            get
            {
                if (_canvasGroup == null)
                {
                    _canvasGroup = GetComponent<CanvasGroup>();
                    if (_canvasGroup == null)
                        _canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
                return _canvasGroup;
            }
        }

        public void Setup(string senderName, string message, string colorHex, byte[] pixelPng)
        {
            bool showPixel = PixelNicknameHelper.ShouldShowPixel();

            // ── 닉네임 영역: 이미지 vs 텍스트 전환 ──
            if (showPixel && nicknameImage != null)
            {
                _cachedTexture = PixelNicknameHelper.ApplyPng(nicknameImage, pixelPng, _cachedTexture);
                nicknameImage.gameObject.SetActive(true);
                if (nicknameText != null) nicknameText.gameObject.SetActive(false);
            }
            else
            {
                if (nicknameImage != null) nicknameImage.gameObject.SetActive(false);
                if (nicknameText != null)
                {
                    nicknameText.gameObject.SetActive(true);
                    nicknameText.text = $"<color=#{colorHex}>[{senderName}]</color>";
                }
            }

            // ── 메시지 본문 (항상 표시) ──
            messageText.text = $"<color=#{colorHex}>: {message}</color>";
        }

        public void ResetAlpha()
        {
            CanvasGroup.alpha = 1f;
        }

        private void OnDestroy()
        {
            if (_cachedTexture != null)
                Destroy(_cachedTexture);
        }
    }
}