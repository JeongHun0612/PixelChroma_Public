using Cysharp.Threading.Tasks;
using DG.Tweening;
using OneText.UGUI;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    // 로비 진입 실패/강제 복귀 시 사유 문구를 보여주는 공통 경고 팝업.
    // 문구는 LobbyFailureTextDataSO(중앙 매핑)에서 사유로 조회한다.
    public class UIwarningTextPopup : UIPopup
    {
        [Header("Content")]
        [SerializeField] private OneTextLabel messageText;
        [SerializeField] private LobbyFailureTextDataSO failureTextData;
        [SerializeField] private Button confirmButton;

        [Header("Animation")]
        [SerializeField] private RectTransform panel;
        [SerializeField] private float animationDuration = 0.25f;

        private LobbyFailureReason _reason = LobbyFailureReason.None;

        // 표시 직전에 사유를 지정한다.
        public void SetReason(LobbyFailureReason reason) => _reason = reason;

        public override void Initialize()
        {
            base.Initialize();

            if (confirmButton != null) confirmButton.AddClickListener(OnClickBack);
        }

        public override void OnBeforeShow()
        {
            base.OnBeforeShow();

            if (messageText != null && failureTextData != null)
                messageText.text = failureTextData.GetLocalized(_reason);

            if (panel != null) panel.localScale = Vector3.zero;
        }

        protected override async UniTask OnShowAnimation()
        {
            if (panel != null)
                await panel.DOScale(Vector3.one, animationDuration).SetEase(Ease.OutBack).ToUniTask();
        }

        protected override async UniTask OnHideAnimation()
        {
            if (panel != null)
                await panel.DOScale(Vector3.zero, animationDuration).SetEase(Ease.InBack).ToUniTask();
        }
    }
}
