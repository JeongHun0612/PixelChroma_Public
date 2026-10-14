using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    public class UIInitialLoading : UIPanel
    {
        [Header("Fade Animation")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private float fadeDuration = 0.5f;

        [Header("Warning Panel")]
        [SerializeField] private Button quitButton;
        [SerializeField] private RectTransform warningPanel;
        [SerializeField] private float warningAnimationDuration = 0.25f;

        public override void Initialize()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            if (quitButton != null)
                quitButton.AddClickListener(() => Application.Quit());
        }

        protected override UniTask OnShowAnimation()
        {
            canvasGroup.alpha = 1f;

            // 경고 패널은 기본적으로 숨겨둡니다(Steam 초기화 실패 시에만 노출).
            if (warningPanel != null)
                warningPanel.gameObject.SetActive(false);

            return UniTask.CompletedTask;
        }

        protected override async UniTask OnHideAnimation()
        {
            await canvasGroup.DOFade(0f, fadeDuration).ToUniTask();
        }

        public async UniTask ShowSteamWarning()
        {
            if (warningPanel != null)
            {
                warningPanel.gameObject.SetActive(true);
                warningPanel.localScale = Vector3.zero;
                await warningPanel.DOScale(Vector3.one, warningAnimationDuration)
                    .SetEase(Ease.OutBack).ToUniTask();
            }
        }
    }
}
