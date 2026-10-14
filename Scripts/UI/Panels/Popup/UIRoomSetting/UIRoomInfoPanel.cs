using OneText.UGUI;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    // 방 이름 / 인원 수(최대 인원·술래) / 공개 여부 / 게임 모드 를 담당한다.
    // RoomOption의 소유권은 UIRoomSetting에 있고, 여기서는 Bind로 받은 참조에 값만 반영한다.
    public class UIRoomInfoPanel : MonoBehaviour
    {
        [Header("Scroll")]
        [SerializeField] private ScrollRect scrollRect;

        [Header("Room Name")]
        [SerializeField] private OneTextInputField roomNameInput;
        [SerializeField] private UIWarningText roomNameWarningText;
        [SerializeField] private OneTextLabel roomNameCountText;

        [Header("Room Name Error")]
        [SerializeField] private Image roomNameInputBackground;
        [SerializeField] private Color roomNameInputErrorColor = new Color(1f, 0.78f, 0.78f, 1f);

        [Header("Max Players")]
        [SerializeField] private UINumericStepper maxPlayersStepper;

        [Header("Seeker Count")]
        [SerializeField] private UINumericStepper seekerCountStepper;

        [Header("Public / Private")]
        [SerializeField] private Button publicToggleButton;
        [SerializeField] private GameObject publicObject;
        [SerializeField] private GameObject privateObject;

        [Header("Game Mode")]
        [SerializeField] private UIGameModeSelector gameModeSelector;

        [Header("Banned Words (비속어 필터링)")]
        [SerializeField] private BannedWordsSO bannedWords;

        [Header("Localization")]
        [SerializeField] private RoomSettingTextDataSO textData;

        private RoomOption _option;
        private bool _isEditMode;
        private Color _roomNameInputDefaultColor;
        private bool _hasRoomNameInputDefaultColor;
        private bool _initialized;

        // ── Public ──

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            // 방 이름 InputField 이벤트 등록
            if (roomNameInput != null)
            {
                roomNameInput.characterLimit = RoomOption.MaxRoomNameLength;
                roomNameInput.onValueChanged.AddListener(OnRoomNameChanged);
                roomNameInput.onEndEdit.AddListener(OnRoomNameInput);
            }

            CacheRoomNameInputDefaultColor();
            if (roomNameWarningText != null) roomNameWarningText.SetAutoHide(false);
            ClearRoomNameError();

            // 인원 수(최대 인원·술래) 이벤트 등록
            if (maxPlayersStepper != null)
            {
                maxPlayersStepper.Initialize();
                maxPlayersStepper.Configure(RoomOption.MinPlayers, RoomOption.MaxPlayersLimit);
                maxPlayersStepper.OnValueChanged += OnMaxPlayersChanged;
            }

            if (seekerCountStepper != null)
            {
                seekerCountStepper.Initialize();
                seekerCountStepper.Configure(RoomOption.MinSeekerCount, RoomOption.GetMaxSeekerCount(RoomOption.DefaultMaxPlayers));
                seekerCountStepper.OnValueChanged += OnSeekerCountChanged;
            }

            // 공개 여부 이벤트 등록
            if (publicToggleButton != null) publicToggleButton.AddClickListener(TogglePublic);

            // 게임 모드 선택기 초기화 (탭 생성 포함)
            if (gameModeSelector != null) gameModeSelector.Initialize();

            LobbyPlayerState.OnLobbyPlayersChanged -= OnLobbyPlayersChanged;
            LobbyPlayerState.OnLobbyPlayersChanged += OnLobbyPlayersChanged;
        }

        public void Bind(RoomOption option, bool isEditMode)
        {
            _option = option;
            _isEditMode = isEditMode;
            if (gameModeSelector != null) gameModeSelector.Bind(option);
            NormalizePlayerCounts();
            RefreshAll();
        }

        public void ResetScroll()
        {
            if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
        }

        public void FocusRoomName()
        {
            if (roomNameInput != null) roomNameInput.ActivateInputField();
        }

        // EventSystem은 선택된 오브젝트가 비활성이 되어도 선택을 비우지 않는다.
        // 놓아주지 않으면 닫힌 입력창이 계속 "선택됨"으로 남아 다른 곳의 단축키 판정을 망친다.
        public void ReleaseRoomNameFocus()
        {
            if (roomNameInput != null) roomNameInput.DeactivateInputField();
        }

        // 방 이름을 검증해 option에 반영한다. 실패 시 경고를 띄우고 false.
        public bool TryApply()
        {
            if (_option == null) return false;

            if (roomNameInput != null)
            {
                string roomName = roomNameInput.text.Trim();
                var result = NameValidationService.Validate(
                    roomName,
                    bannedWords,
                    RoomOption.MaxRoomNameLength,
                    allowEmpty: false);

                if (result != NameValidation.Valid)
                {
                    string warningMessage = GetWarningMessage(result);
                    if (!string.IsNullOrEmpty(warningMessage))
                        ShowRoomNameWarningText(warningMessage);
                    return false;
                }

                _option.roomName = roomName;
            }

            return true;
        }

        // ── Private ──

        private void RefreshAll()
        {
            if (_option == null) return;

            RefreshRoomNameUI();
            RefreshPlayerCountUI();
            RefreshPublicUI();
        }

        // 방 이름
        private void RefreshRoomNameUI()
        {
            if (roomNameInput != null)
            {
                string roomName = _option.roomName ?? "";
                roomNameInput.SetTextWithoutNotify(roomName);
                RefreshRoomNameCount(roomName.Length);
            }

            ClearRoomNameError();
        }

        private string GetWarningMessage(NameValidation result)
        {
            if (textData == null)
                return string.Empty;

            return result switch
            {
                NameValidation.Empty => textData.warningEmptyRoomName.GetLocalizedString(),
                NameValidation.Banned => textData.warningBanned.GetLocalizedString(),
                _ => string.Empty,
            };
        }

        // 인원 수 (최대 인원 / 술래)
        // 버튼(+/-)과 InputField 직접 입력이 동일한 clamp 규칙을 공유하도록 값 설정을 한곳에 모은다.
        private void SetMaxPlayers(int value)
        {
            _option.maxPlayers = Mathf.Clamp(
                value,
                GetMaxPlayersMinValue(),
                RoomOption.MaxPlayersLimit);

            int maxSeeker = RoomOption.GetMaxSeekerCount(_option.maxPlayers);
            if (_option.seekerCount > maxSeeker)
                _option.seekerCount = Mathf.Max(RoomOption.MinSeekerCount, maxSeeker);

            RefreshPlayerCountUI();
        }

        private void SetSeekerCount(int value)
        {
            int maxSeeker = RoomOption.GetMaxSeekerCount(_option.maxPlayers);
            _option.seekerCount = Mathf.Clamp(
                value,
                RoomOption.MinSeekerCount,
                maxSeeker);

            RefreshPlayerCountUI();
        }

        private void RefreshPlayerCountUI()
        {
            if (_option == null) return;

            NormalizePlayerCounts();

            int minPlayers = GetMaxPlayersMinValue();
            if (maxPlayersStepper != null) maxPlayersStepper.SetRange(minPlayers, RoomOption.MaxPlayersLimit);
            if (maxPlayersStepper != null) maxPlayersStepper.SetValue(_option.maxPlayers);

            int maxSeeker = RoomOption.GetMaxSeekerCount(_option.maxPlayers);
            if (seekerCountStepper != null)
            {
                seekerCountStepper.SetRange(RoomOption.MinSeekerCount, maxSeeker);
                seekerCountStepper.SetValue(_option.seekerCount);
            }
        }

        private void NormalizePlayerCounts()
        {
            if (_option == null) return;

            int minPlayers = GetMaxPlayersMinValue();
            _option.maxPlayers = Mathf.Clamp(_option.maxPlayers, minPlayers, RoomOption.MaxPlayersLimit);

            int maxSeeker = RoomOption.GetMaxSeekerCount(_option.maxPlayers);
            _option.seekerCount = Mathf.Clamp(_option.seekerCount, RoomOption.MinSeekerCount, maxSeeker);
        }

        private int GetMaxPlayersMinValue()
        {
            if (!_isEditMode)
                return RoomOption.MinPlayers;

            return Mathf.Clamp(GetCurrentLobbyMemberCount(), RoomOption.MinPlayers, RoomOption.MaxPlayersLimit);
        }

        private static int GetCurrentLobbyMemberCount()
        {
            int steamMemberCount = LobbyManager.Instance != null ? LobbyManager.Instance.GetMemberCount() : 0;
            int localMemberCount = LobbyPlayerState.AllPlayers.Count;
            return Mathf.Max(steamMemberCount, localMemberCount);
        }

        // ── 이벤트 바인딩 ──

        // 방 이름: 타이핑 중에는 경고를 지우고 글자 수만 갱신한다. (값 반영은 onEndEdit에서)
        private void OnRoomNameChanged(string text)
        {
            ClearRoomNameError();
            RefreshRoomNameCount(text.Length);
        }

        private void OnRoomNameInput(string text)
        {
            if (_option == null) return;
            _option.roomName = text.Trim();
        }

        private void OnMaxPlayersChanged(float value)
        {
            if (_option == null) return;
            SetMaxPlayers(Mathf.RoundToInt(value));
        }

        private void OnSeekerCountChanged(float value)
        {
            if (_option == null) return;
            SetSeekerCount(Mathf.RoundToInt(value));
        }

        private void OnLobbyPlayersChanged()
        {
            if (_option == null || !_isEditMode) return;
            RefreshPlayerCountUI();
        }

        // 공개 여부
        private void RefreshPublicUI()
        {
            if (publicObject != null) publicObject.SetActive(_option.isPublic);
            if (privateObject != null) privateObject.SetActive(!_option.isPublic);
        }

        private void ShowRoomNameWarningText(string message)
        {
            if (roomNameWarningText != null)
                roomNameWarningText.Show(message);

            SetRoomNameInputErrorState(true);

            if (roomNameInput != null)
                roomNameInput.ActivateInputField();
        }

        private void ClearRoomNameError()
        {
            if (roomNameWarningText != null)
                roomNameWarningText.Clear();

            SetRoomNameInputErrorState(false);
        }

        private void CacheRoomNameInputDefaultColor()
        {
            if (roomNameInputBackground == null && roomNameInput != null)
                roomNameInputBackground = roomNameInput.GetComponent<Image>();

            if (roomNameInputBackground == null) return;

            _roomNameInputDefaultColor = roomNameInputBackground.color;
            _hasRoomNameInputDefaultColor = true;
        }

        private void SetRoomNameInputErrorState(bool isError)
        {
            if (roomNameInputBackground == null) return;

            if (!_hasRoomNameInputDefaultColor)
                CacheRoomNameInputDefaultColor();

            roomNameInputBackground.color = isError
                ? roomNameInputErrorColor
                : _roomNameInputDefaultColor;
        }

        private void RefreshRoomNameCount(int length)
        {
            if (roomNameCountText != null)
                roomNameCountText.text = $"{length}/{RoomOption.MaxRoomNameLength}";
        }

        // 공개 여부: 클릭할 때마다 공개/비공개를 뒤집는다.
        private void TogglePublic()
        {
            if (_option == null) return;

            _option.isPublic = !_option.isPublic;
            RefreshPublicUI();
        }

        private void OnDestroy()
        {
            LobbyPlayerState.OnLobbyPlayersChanged -= OnLobbyPlayersChanged;
        }
    }
}
