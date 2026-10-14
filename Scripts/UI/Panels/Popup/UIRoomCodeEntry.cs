using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


namespace PixelChroma.UI
{
    public class UIRoomCodeEntry : UIPopup
    {
        [Header("Code Input")]
        [SerializeField] private TMP_InputField roomCodeInput;

        [Header("Show Code")]
        [SerializeField] private Button showCodeButton;
        [SerializeField] private Image showCodeIcon;
        [SerializeField] private Sprite shownSprite;
        [SerializeField] private Sprite hiddenSprite;

        [Header("Actions")]
        [SerializeField] private Button joinButton;
        [SerializeField] private Button closeButton;

        [Header("Status")]
        [SerializeField] private UIWarningText warningText;

        [Header("Localization")]
        [SerializeField] private RoomCodeEntryTextDataSO textData;
        [SerializeField] private LobbyFailureTextDataSO failureTextData;

        [Header("Animation")]
        [SerializeField] private RectTransform panel;
        [SerializeField] private float animationDuration = 0.25f;

        private bool _isCodeVisible;

        private void Update()
        {
            if (roomCodeInput != null && roomCodeInput.isFocused)
                Input.imeCompositionMode = IMECompositionMode.Off;
        }

        public override void Initialize()
        {
            base.Initialize();

            if (joinButton != null) joinButton.AddClickListener(OnClickJoin);
            if (closeButton != null) closeButton.AddClickListener(OnClickBack);
            if (showCodeButton != null) showCodeButton.AddClickListener(ToggleCodeVisibility);

            if (roomCodeInput != null)
            {
                roomCodeInput.characterLimit = LobbyManager.CodeLength;
                roomCodeInput.onValidateInput = OnValidateChar;
                roomCodeInput.onValueChanged.AddListener(OnCodeChanged);
            }
        }

        public override void OnBeforeShow()
        {
            base.OnBeforeShow();

            // 한글 등 IME 조합 입력 자체를 차단해 Password 마스킹에 조합 문자열이 그려지는 것을 방지
            Input.imeCompositionMode = IMECompositionMode.Off;

            if (roomCodeInput != null) roomCodeInput.SetTextWithoutNotify(string.Empty);

            _isCodeVisible = false;
            ApplyCodeVisibility();
            SetStatusVisible(false);
            RefreshJoinButton(0);

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
            return UniTask.CompletedTask;
        }

        protected override UniTask OnHide()
        {
            if (LobbyManager.Instance != null)
            {
                LobbyManager.Instance.OnLobbyFailed -= HandleLobbyFailed;
            }

            Input.imeCompositionMode = IMECompositionMode.Auto;

            return UniTask.CompletedTask;
        }

        private void ShowStatus(string message)
        {
            if (warningText != null) warningText.Show(message);
        }

        private void SetStatusVisible(bool visible)
        {
            if (!visible && warningText != null) warningText.Clear();
        }

        private void RefreshJoinButton(int length)
        {
            if (joinButton != null) joinButton.interactable = length == LobbyManager.CodeLength;
        }

        private void ToggleCodeVisibility()
        {
            _isCodeVisible = !_isCodeVisible;
            ApplyCodeVisibility();
        }

        private void ApplyCodeVisibility()
        {
            if (roomCodeInput != null)
            {
                roomCodeInput.contentType = _isCodeVisible
                    ? TMP_InputField.ContentType.Standard
                    : TMP_InputField.ContentType.Password;
                roomCodeInput.ForceLabelUpdate();
            }

            var sprite = _isCodeVisible ? shownSprite : hiddenSprite;

            if (showCodeIcon != null && sprite != null)
            {
                showCodeIcon.sprite = sprite;
            }
        }

        private void OnCodeChanged(string value)
        {
            RefreshJoinButton(value.Length);
        }

        private void HandleLobbyFailed(LobbyFailureReason reason)
        {
            if (failureTextData != null)
                ShowStatus(failureTextData.GetLocalized(reason));

            if (roomCodeInput != null)
                RefreshJoinButton(roomCodeInput.text.Trim().Length);
        }

        // 입력되는 문자를 문자 단위로 검증한다.
        // 영문/숫자만 허용하고 소문자는 대문자로 변환, 그 외(한글 등)는 '\0'을 반환해 커밋 자체를 막는다.
        private char OnValidateChar(string text, int charIndex, char addedChar)
        {
            if (addedChar >= 'a' && addedChar <= 'z')
                return char.ToUpper(addedChar);

            if ((addedChar >= 'A' && addedChar <= 'Z') || (addedChar >= '0' && addedChar <= '9'))
                return addedChar;

            if (textData != null)
                ShowStatus(textData.invalidCharwarningText.GetLocalizedString());

            return '\0';
        }

        private void OnClickJoin()
        {
            string code = roomCodeInput != null ? roomCodeInput.text.Trim() : string.Empty;
            if (code.Length != LobbyManager.CodeLength)
                return;

            if (joinButton != null)
                joinButton.interactable = false;

            SetStatusVisible(false);
            LobbyManager.Instance.JoinLobbyByCode(code);
        }
    }
}