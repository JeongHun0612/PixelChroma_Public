using UnityEngine;
using OneText.UGUI;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using DG.Tweening;

namespace PixelChroma.UI
{
    public class UINickNameSetting : UIPopup
    {
        [Header("Nickname Limits Settings")]
        [Tooltip("닉네임 최대 글자 수 제한 설정")]
        [SerializeField] private int maxNicknameLength = 10; 

        [Header("Banned Words (비속어 필터링)")]
        [SerializeField] private BannedWordsSO bannedWords;

        [Header("Inputs & Buttons")]
        [SerializeField] private OneTextInputField nicknameInputField;
        [SerializeField] private Button saveButton;     
        [SerializeField] private Button closeButton;      

        [Header("Feedback Status UI")]
        [SerializeField] private UIWarningText warningText;

        [Header("Localization")]
        [SerializeField] private NicknameSettingTextDataSO textData;

        [Header("Animation")]
        [SerializeField] private RectTransform panel;      
        [SerializeField] private float animationDuration = 0.25f;

        public override void Initialize()
        {
            base.Initialize();

            if (saveButton != null) saveButton.AddClickListener(OnClickSave);
            if (closeButton != null) closeButton.AddClickListener(OnClickBack);

            if (nicknameInputField != null)
                nicknameInputField.characterLimit = maxNicknameLength;
        }

        public override void OnBeforeShow()
        {
            base.OnBeforeShow();

            if (nicknameInputField != null)
                nicknameInputField.text = TextNicknameService.LoadOrDefault();

            SetwarningTextMessage(string.Empty);

            if (panel != null) panel.localScale = Vector3.zero;
        }

        protected override async UniTask OnShowAnimation()
        {
            if (panel != null)
            {
                if (nicknameInputField != null) nicknameInputField.ActivateInputField();
                await panel.DOScale(Vector3.one, animationDuration).SetEase(Ease.OutBack).ToUniTask();
            }
        }

        protected override async UniTask OnHideAnimation()
        {
            if (panel != null)
                await panel.DOScale(Vector3.zero, animationDuration).SetEase(Ease.InBack).ToUniTask();
        }

        private void SetwarningTextMessage(string message)
        {
            if (warningText != null) warningText.Show(message);

            if (!string.IsNullOrEmpty(message) && nicknameInputField != null)
                nicknameInputField.ActivateInputField();
        }

        private void OnClickSave()
        {
            if (nicknameInputField == null) return;

            string enteredName = nicknameInputField.text.Trim();

            // OneText는 NotoSans 동적 아틀라스와 폴백 스택으로 다국어를 폭넓게 렌더하므로,
            // 렌더 폰트를 넘겨 □ 문자를 걸러내던 TMP 시절 안전망은 더 이상 넘기지 않는다.
            switch (NameValidationService.Validate(enteredName, bannedWords, maxNicknameLength))
            {
                case NameValidation.TooLong:
                    SetwarningTextMessage(textData.warningMaxLength.GetLocalizedString(maxNicknameLength));
                    return;
                case NameValidation.Banned:
                    SetwarningTextMessage(textData.warningBanned.GetLocalizedString());
                    return;
                case NameValidation.Unsupported:
                    if (!textData.warningUnsupported.IsEmpty)
                        SetwarningTextMessage(textData.warningUnsupported.GetLocalizedString());
                    return;
            }

            // 비워두면 스팀 이름(스팀 미사용 시 임의 이름)을 대신 저장한다.
            if (string.IsNullOrEmpty(enteredName))
                enteredName = TextNicknameService.GetDefaultSteamName();

            TextNicknameService.Save(enteredName);
            OnClickBack();
        }
    }
}
