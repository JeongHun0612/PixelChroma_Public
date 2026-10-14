using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    public class UITitle : UIScene
    {
        [Header("Logo")]
        [SerializeField] private Transform logo;
        [SerializeField] private float logoAnimationDuration = 0.5f;

        [Header("Main Menu Buttons")]
        [SerializeField] private Button quickJoinButton;
        [SerializeField] private Button createRoomButton;
        [SerializeField] private Button findRoomButton;
        [SerializeField] private Button joinRoomButton;

        [Header("Setting Buttons")]
        [SerializeField] private Button nicknameSettingButton;
        [SerializeField] private Button languageSettingButton;
        [SerializeField] private Button settingButton;

        [Header("System Buttons")]
        [SerializeField] private Button exitGameButton;

        [Header("External Links Button")]
        [SerializeField] private Button discordButton;
        [SerializeField] private Button snsButton;
        [SerializeField] private Button workshopButton;

        [Header("SNS Panel")]
        [SerializeField] private RectTransform snsPanel;
        [SerializeField] private CanvasGroup snsPanelCanvasGroup;
        [SerializeField] private float snsSlideDuration = 0.3f;
        [SerializeField] private float snsSlideDistance = 200f;


        [Header("SNS Link Buttons")]
        [SerializeField] private Button youtubeButton;
        [SerializeField] private Button twitterButton;
        [SerializeField] private Button tiktokButton;
        [SerializeField] private Button instagramButton;


        [Header("External Link URLs")]
        [SerializeField] private string discordUrl = "https://discord.gg/여기에초대코드";
        [SerializeField] private string workshopUrl = "https://steamcommunity.com/app/5023800/workshop/";
        [SerializeField] private string youtubeUrl = "https://www.youtube.com/@여기에채널";
        [SerializeField] private string twitterUrl = "https://x.com/여기에계정";
        [SerializeField] private string tiktokUrl = "https://www.tiktok.com/@여기에계정";
        [SerializeField] private string instagramUrl = "https://www.instagram.com/여기에계정";

        private Vector2 _snsPanelRestPos;
        private bool _snsPanelCaptured;
        private bool _snsPanelOpen;
        private Sequence _snsPanelSeq;

        public override void Initialize()
        {
            base.Initialize();

            if (quickJoinButton != null) quickJoinButton.AddClickListener(OnClickQuickJoin);
            if (createRoomButton != null) createRoomButton.AddClickListener(OnClickCreateRoom);
            if (findRoomButton != null) findRoomButton.AddClickListener(OnClickFindRoom);
            if (joinRoomButton != null) joinRoomButton.AddClickListener(OnClickJoinRoom);
            if (nicknameSettingButton != null) nicknameSettingButton.AddClickListener(OnClickNicknameSetting);
            if (languageSettingButton != null) languageSettingButton.AddClickListener(OnClickLanguageButton);
            if (settingButton != null) settingButton.AddClickListener(OnClickSetting);
            if (exitGameButton != null) exitGameButton.AddClickListener(OnClickExitGame);

            if (discordButton != null) discordButton.AddClickListener(() => OpenUrl(discordUrl));
            if (workshopButton != null) workshopButton.AddClickListener(() => OpenUrl(workshopUrl));
            if (snsButton != null) snsButton.AddClickListener(ToggleSnsPanel);

            if (snsPanel != null)
                snsPanel.gameObject.SetActive(false);

            if (youtubeButton != null) youtubeButton.AddClickListener(() => OpenUrl(youtubeUrl));
            if (twitterButton != null) twitterButton.AddClickListener(() => OpenUrl(twitterUrl));
            if (tiktokButton != null) tiktokButton.AddClickListener(() => OpenUrl(tiktokUrl));
            if (instagramButton != null) instagramButton.AddClickListener(() => OpenUrl(instagramUrl));
        }

        public override void OnCancel() => OnClickSetting();

        protected override async UniTask OnShowAnimation()
        {
            if (logo == null) return;

            logo.localScale = Vector3.zero;
            await logo.DOScale(Vector3.one, logoAnimationDuration)
                .SetEase(Ease.OutBack)
                .ToUniTask();
        }
        protected override UniTask OnShow()
        {
            // 현재 선택된 프리셋이 빈 그림일 경우에만 닉네임 설정 팝업을 띄운다
            bool isDefault = PixelNicknameService.LoadIsDefaultPreset();
            if (!isDefault)
            {
                int activeIndex = PixelNicknameService.LoadActivePresetIndex();
                byte[] data = PixelNicknameService.LoadPreset(activeIndex);
                if (data == null || data.Length == 0)
                {
                    UIManager.Instance.ShowPopup<UIPixelNicknameSetting>().Forget();
                }
            }

            return UniTask.CompletedTask;
        }

        private void OnClickQuickJoin()
        {
            UIManager.Instance.ShowPopup<UIQuickMatch>().Forget();
        }

        private void OnClickCreateRoom()
        {
            UIManager.Instance.ShowPopup<UIRoomSetting>().Forget();
        }

        private void OnClickFindRoom()
        {
            UIManager.Instance.ShowPopup<UIRoomList>().Forget();
        }

        private void OnClickJoinRoom()
        {
            UIManager.Instance.ShowPopup<UIRoomCodeEntry>().Forget();
        }
        private void OnClickNicknameSetting()
        {
            //UIManager.Instance.ShowPopup<UINickNameSetting>().Forget();
            UIManager.Instance.ShowPopup<UIPixelNicknameSetting>().Forget();
        }

        public void OnClickLanguageButton()
        {
            UIManager.Instance.ShowPopup<UILanguageSelect>().Forget();
        }

        private void OnClickSetting()
        {
            UIManager.Instance.ShowPopup<UISetting>().Forget();
        }

        private void OnClickExitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static void OpenUrl(string url)
        {
            // Steam 초기화 상태면 Overlay 브라우저 사용 (게임을 나가지 않음)
            if (GameManager.SteamInitialized)
            {
                Steamworks.SteamFriends.ActivateGameOverlayToWebPage(url);
                return;
            }

            // Steam 없으면 시스템 브라우저로 fallback
            Application.OpenURL(url);
        }


        //----------------------SNS 패널---------------------------------------

        private void ToggleSnsPanel()
        {
            if (snsPanel == null) return;

            if (_snsPanelOpen)
                CloseSnsPanel();
            else
                OpenSnsPanel();
        }

        private void OpenSnsPanel()
        {
            _snsPanelSeq?.Kill();

            if (!_snsPanelCaptured)
            {
                _snsPanelRestPos = snsPanel.anchoredPosition;
                _snsPanelCaptured = true;
            }

            snsPanel.gameObject.SetActive(true);

            // 시작 상태: 오른쪽으로 밀려난 위치 + 투명
            snsPanel.anchoredPosition = _snsPanelRestPos + new Vector2(snsSlideDistance, 0f);
            snsPanelCanvasGroup.alpha = 0f;

            // UIPanelSlideIntro와 동일한 패턴
            _snsPanelSeq = DOTween.Sequence().SetLink(snsPanel.gameObject);
            _snsPanelSeq.Insert(0f, snsPanel.DOAnchorPos(_snsPanelRestPos, snsSlideDuration).SetEase(Ease.OutCubic));
            _snsPanelSeq.Insert(0f, snsPanelCanvasGroup.DOFade(1f, snsSlideDuration).SetEase(Ease.OutQuad));

            _snsPanelOpen = true;
        }

        private void CloseSnsPanel()
        {
            _snsPanelSeq?.Kill();

            var hidePos = _snsPanelRestPos + new Vector2(snsSlideDistance, 0f);

            _snsPanelSeq = DOTween.Sequence().SetLink(snsPanel.gameObject);
            _snsPanelSeq.Insert(0f, snsPanel.DOAnchorPos(hidePos, snsSlideDuration).SetEase(Ease.InCubic));
            _snsPanelSeq.Insert(0f, snsPanelCanvasGroup.DOFade(0f, snsSlideDuration).SetEase(Ease.InQuad));
            _snsPanelSeq.OnComplete(() =>
            {
                snsPanel.gameObject.SetActive(false);
                _snsPanelOpen = false;
            });

            _snsPanelOpen = false;
        }

    }
}