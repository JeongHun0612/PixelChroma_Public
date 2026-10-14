using Cysharp.Threading.Tasks;
using DG.Tweening;
using OneText.UGUI;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    public class UIQuickMatch : UIPopup
    {
        [Header("Status")]
        [SerializeField] private OneTextLabel statusText;
        [SerializeField] private UIWarningText warningText;
        [SerializeField] private Animator loadingIcon;
        [SerializeField] private GameObject warningIcon;

        [Header("Localization")]
        [SerializeField] private QuickMatchTextDataSO textData;
        [SerializeField] private LobbyFailureTextDataSO failureTextData;

        [Header("Buttons")]
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button closeButton;

        [Header("Animation")]
        [SerializeField] private RectTransform panel;
        [SerializeField] private float animationDuration = 0.25f;

        public override void Initialize()
        {
            base.Initialize();

            if (cancelButton != null) cancelButton.AddClickListener(OnClickCancel);
            if (retryButton != null) retryButton.AddClickListener(OnClickRetry);
            if (closeButton != null) closeButton.AddClickListener(OnClickBack);
        }

        public override void OnBeforeShow()
        {
            base.OnBeforeShow();

            SetLoading();

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

        protected override UniTask OnShow()
        {
            LobbyManager.Instance.OnLobbyFailed += HandleLobbyFailed;
            LobbyManager.Instance.JoinQuickMatch();
            return UniTask.CompletedTask;
        }

        protected override UniTask OnHide()
        {
            if (LobbyManager.Instance != null)
                LobbyManager.Instance.OnLobbyFailed -= HandleLobbyFailed;

            return UniTask.CompletedTask;
        }

        private void HandleLobbyFailed(LobbyFailureReason reason)
        {
            SetFailed(reason);
        }

        private void SetLoading()
        {
            if (loadingIcon != null)
            {
                loadingIcon.gameObject.SetActive(true);
                loadingIcon.speed = 1f;
                loadingIcon.Play(0, 0, 0f);
            }
            if (warningIcon != null) warningIcon.SetActive(false);

            if (statusText != null) statusText.gameObject.SetActive(true);
            if (warningText != null) warningText.Clear();

            if (cancelButton != null) cancelButton.gameObject.SetActive(true);
            if (retryButton != null) retryButton.gameObject.SetActive(false);
        }

        private void SetFailed(LobbyFailureReason reason)
        {
            if (loadingIcon != null) loadingIcon.gameObject.SetActive(false);
            if (warningIcon != null) warningIcon.SetActive(true);

            if (statusText != null) statusText.gameObject.SetActive(false);
            if (warningText != null && failureTextData != null)
                warningText.Show(failureTextData.GetLocalized(reason));

            if (cancelButton != null) cancelButton.gameObject.SetActive(false);
            if (retryButton != null) retryButton.gameObject.SetActive(true);
        }

        private void SetCancelled()
        {
            if (loadingIcon != null)
            {
                loadingIcon.Play(0, 0, 0f);
                loadingIcon.speed = 0f;
            }
            if (warningIcon != null) warningIcon.SetActive(false);

            if (statusText != null) statusText.gameObject.SetActive(false);
            if (warningText != null)
                warningText.Show(textData.searchCancelledStatus.GetLocalizedString());

            if (cancelButton != null) cancelButton.gameObject.SetActive(false);
            if (retryButton != null) retryButton.gameObject.SetActive(true);
        }

        private void OnClickCancel()
        {
            if (LobbyManager.Instance != null)
                LobbyManager.Instance.CancelQuickMatch();

            SetCancelled();
        }

        private void OnClickRetry()
        {
            SetLoading();
            LobbyManager.Instance.JoinQuickMatch();
        }

        public override void OnClickBack()
        {
            if (LobbyManager.Instance != null)
                LobbyManager.Instance.CancelQuickMatch();

            base.OnClickBack();
        }
    }
}
