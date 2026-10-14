using System.Linq;
using Cysharp.Threading.Tasks;
using OneText.UGUI;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    public class UILobby : UIScene, IPaintHUD
    {
        [Header("Room Info")]
        [SerializeField] private OneTextLabel roomNameText;
        [SerializeField] private OneTextLabel roomCodeText;
        [SerializeField] private OneTextLabel playerCountText;

        [Header("Member List")]
        [SerializeField] private UIMemberSlot[] memberSlots;
        [SerializeField] private Button copyCodeButton;
        [SerializeField] private Button leaveButton;

        [SerializeField] private UIDisableCoverButton roomSettingsButton;

        [Header("Show Code")]
        [SerializeField] private Button showCodeButton;
        [SerializeField] private GameObject shownIcon;
        [SerializeField] private GameObject hiddenIcon;

        [Header("Start (Host)")]
        [SerializeField] private UIDisableCoverButton startGameButton;
        [SerializeField] private OneTextLabel startReadyCountText;

        [Header("Ready (Client)")]
        [SerializeField] private UIDisableCoverButton readyButton;
        [SerializeField] private Image readyCheckImage;
        [SerializeField] private OneTextLabel readyCountText;

        [Header("Countdown")]
        [SerializeField] private GameObject timerPanel;
        [SerializeField] private UIPunchText timerText;

        [Header("Setting Buttons")]
        [SerializeField] private Button settingButton;
        [SerializeField] private Button languageSettingButton;

        [Header("Room Option")]
        [SerializeField] private UIRoomOptionPanel roomOptionPanel;

        [Header("Customize")]
        [SerializeField] private GameObject paintModeRoot;
        [SerializeField] private UIColorPicker colorPicker;

        [Header("Workshop Map Check")]
        [SerializeField] private UIWorkshopMapCheck workshopMapCheckUI;

        [HideInInspector] public LobbyPlayerState localLobbyPlayer;

        private bool _isCodeVisible;


        // ── Override ─────────────────────────────────────────

        public UIColorPicker ColorPicker => colorPicker;

        public override void Initialize()
        {
            base.Initialize();
            if (copyCodeButton != null) copyCodeButton.AddClickListener(OnClickCopyCode);
            if (leaveButton != null) leaveButton.AddClickListener(OnClickLeave);
            if (startGameButton != null) startGameButton.Button.AddClickListener(OnClickStartGame);
            if (readyButton != null) readyButton.Button.AddClickListener(OnClickReady);
            if (roomSettingsButton != null) roomSettingsButton.Button.AddClickListener(OnClickRoomSettings);
            if (showCodeButton != null) showCodeButton.AddClickListener(OnClickToggleCode);
            if (settingButton != null) settingButton.AddClickListener(OnClickSetting);
            if (languageSettingButton != null) languageSettingButton.AddClickListener(OnClickLanguageButton);

            if (colorPicker != null) colorPicker.Initialize();
            if (paintModeRoot != null) paintModeRoot.SetActive(false);

            if (workshopMapCheckUI != null) workshopMapCheckUI.Initialize(this);
        }

        /// <summary>
        /// 패널이 켜지기 직전에 호출. GameObject 활성화 전에 이벤트를 구독한다.
        /// </summary>
        public override void OnBeforeShow()
        {
            base.OnBeforeShow();

            LobbyPlayerState.OnLobbyPlayersChanged += OnLobbyPlayersChanged;
            LobbyPlayerState.OnReadyChanged += OnReadyChanged;
            LobbyPlayerState.OnRoomOptionSynced += OnRoomOptionSynced;

            PlayerBase.OnAnyPlayerNameChanged += RefreshMemberList;
            GameSettings.Changed += RefreshMemberList;
            GameRoundManager.OnTimeStateChanged += OnTimeStateChanged;
            GameRoundManager.OnTimerTick += OnTimerTick;


            if (workshopMapCheckUI != null) workshopMapCheckUI.Subscribe();
            if (roomOptionPanel != null) roomOptionPanel.Subscribe();

        }

        /// <summary>
        /// 패널이 완전히 내려간 뒤 호출. OnBeforeShow와 대칭으로 구독을 해제한다.
        /// </summary>
        public override void OnAfterHide()
        {
            base.OnAfterHide();

            LobbyPlayerState.OnLobbyPlayersChanged -= OnLobbyPlayersChanged;
            LobbyPlayerState.OnReadyChanged -= OnReadyChanged;
            LobbyPlayerState.OnRoomOptionSynced -= OnRoomOptionSynced;

            PlayerBase.OnAnyPlayerNameChanged -= RefreshMemberList;
            GameSettings.Changed -= RefreshMemberList;
            GameRoundManager.OnTimeStateChanged -= OnTimeStateChanged;
            GameRoundManager.OnTimerTick -= OnTimerTick;


            if (workshopMapCheckUI != null) workshopMapCheckUI.Unsubscribe();
            if (roomOptionPanel != null) roomOptionPanel.Unsubscribe();
        }

        protected override UniTask OnShow()
        {
            if (timerPanel != null)
                timerPanel.SetActive(false);

            bool isHost = LobbyManager.Instance.IsHost;

            // 역할 게이팅: 호스트는 Start, 클라는 Ready 버튼만 노출
            if (startGameButton != null) startGameButton.gameObject.SetActive(isHost);
            if (readyButton != null) readyButton.gameObject.SetActive(!isHost);

            // 이전 라운드의 카운트다운 비활성화 상태가 남아있을 수 있으니 초기화
            if (readyButton != null) readyButton.SetInteractable(true);

            // 시작 버튼은 모두 레디 전까지 비활성 (RefreshReadyUI에서 갱신)
            if (startGameButton != null) startGameButton.SetInteractable(false);

            if (roomSettingsButton != null)
            {
                roomSettingsButton.gameObject.SetActive(isHost);
                roomSettingsButton.SetInteractable(true);
            }

            if (workshopMapCheckUI != null) workshopMapCheckUI.Hide();

            _isCodeVisible = false;
            ApplyCodeVisibility();

            RefreshRoomTitle();
            RefreshRoomCode();
            RefreshMemberList();
            RefreshPlayerCount();
            RefreshReadyUI();
            RefreshRoomOptionPanel();

            return UniTask.CompletedTask;
        }

        public override void OnCancel()
        {
            UIManager.Instance.ShowPopup<UISetting>().Forget();
        }

        // ── Public ───────────────────────────────────────────

        public void SetPaintMode(bool active)
        {
            if (paintModeRoot != null)
                paintModeRoot.SetActive(active);

            if (roomOptionPanel != null)
                roomOptionPanel.gameObject.SetActive(!active);
        }

        // ── Private ──────────────────────────────────────────

        private void RefreshRoomTitle()
        {
            if (roomNameText == null)
                return;

            var option = LobbyManager.Instance?.CurrentRoomOption;
            roomNameText.text = option?.roomName ?? "";
        }

        private void RefreshRoomCode()
        {
            if (roomCodeText == null)
                return;

            string code = LobbyManager.Instance.CurrentRoomCode;
            string display = _isCodeVisible ? code : MaskCode(code);
            roomCodeText.text = display;
        }

        private void RefreshMemberList()
        {
            if (memberSlots == null || memberSlots.Length == 0) return;

            var sortedPlayers = LobbyPlayerState.AllPlayers.OrderBy(p => p.netId).ToList();
            bool isLocalHost = LobbyManager.Instance.IsHost;

            for (int i = 0; i < memberSlots.Length; i++)
            {
                bool active = i < sortedPlayers.Count;
                memberSlots[i].gameObject.SetActive(active);
                if (active)
                    memberSlots[i].Set(sortedPlayers[i], isHost: i == 0, isLocalHost);
            }
        }

        private void RefreshPlayerCount()
        {
            if (playerCountText == null || LobbyManager.Instance == null)
                return;
            
            int current = LobbyPlayerState.AllPlayers.Count;

            int max = LobbyManager.Instance.CurrentRoomOption != null
                ? LobbyManager.Instance.CurrentRoomOption.maxPlayers
                : RoomOption.DefaultMaxPlayers;

            playerCountText.text = $"{current}/{max}";
        }

        private void RefreshReadyUI()
        {
            var allPlayers = LobbyPlayerState.AllPlayers;
            int totalPlayers = allPlayers.Count;
            int readyPlayers = allPlayers.Count(p => p.isReady);

            localLobbyPlayer = allPlayers.FirstOrDefault(p => p.isLocalPlayer);

            string countDisplay = $"{readyPlayers}/{totalPlayers}";

            if (readyCountText != null)
                readyCountText.text = countDisplay;

            if (startReadyCountText != null)
                startReadyCountText.text = countDisplay;

            if (totalPlayers == 0) return;

            bool isEveryoneReady = (readyPlayers == totalPlayers);

            // 카운트다운 중에는 인원 변동으로 갱신되더라도 시작 버튼을 다시 켜지 않는다.
            bool isCountdown = GameRoundManager.Instance != null
                && GameRoundManager.Instance.CurrentTimeState == TimeState.Countdown;

            if (LobbyManager.Instance.IsHost && startGameButton != null)
                startGameButton.SetInteractable(isEveryoneReady && !isCountdown);

            if (!LobbyManager.Instance.IsHost && readyCheckImage != null)
            {
                bool isLocalReady = localLobbyPlayer != null && localLobbyPlayer.isReady;
                readyCheckImage.gameObject.SetActive(isLocalReady);
            }
        }

        private void RefreshRoomOptionPanel()
        {
            if (roomOptionPanel != null)
                roomOptionPanel.Refresh(LobbyManager.Instance?.CurrentRoomOption);
        }

        private void ApplyCodeVisibility()
        {
            if (shownIcon != null) shownIcon.SetActive(_isCodeVisible);
            if (hiddenIcon != null) hiddenIcon.SetActive(!_isCodeVisible);
        }

        private static string MaskCode(string code)
        {
            return string.IsNullOrEmpty(code) ? string.Empty : new string('*', code.Length);
        }

        // ── 이벤트 바인딩 ────────────────────────────────────

        /// <summary>
        /// 플레이어 입장/퇴장 시 호출.
        /// </summary>
        private void OnLobbyPlayersChanged()
        {
            RefreshMemberList();
            RefreshPlayerCount();
            RefreshReadyUI();
        }

        /// <summary>
        /// 레디 상태 변경 시 호출.
        /// </summary>
        private void OnReadyChanged()
        {
            RefreshMemberList();
            RefreshReadyUI();
        }

        /// <summary>
        /// 서버로부터 RoomOption이 동기화되었을 때 호출.
        /// </summary>
        private void OnRoomOptionSynced(RoomOption option)
        {
            RefreshRoomTitle();
            RefreshRoomCode();
            RefreshPlayerCount();
            RefreshRoomOptionPanel();
        }

        private void OnTimeStateChanged(TimeState state, int remainingSeconds)
        {
            bool isCountdown = state == TimeState.Countdown;

            if (timerPanel != null)
                timerPanel.SetActive(isCountdown);

            if (readyButton != null)
                readyButton.SetInteractable(!isCountdown);

            if (roomSettingsButton != null)
                roomSettingsButton.SetInteractable(!isCountdown);

            if (isCountdown)
            {
                if (startGameButton != null)
                    startGameButton.SetInteractable(false);
            }
            else
            {
                RefreshReadyUI();
            }
        }

        private void OnTimerTick(int remainingSeconds)
        {
            if (GameRoundManager.Instance.CurrentTimeState != TimeState.Countdown) return;

            if (timerText != null)
                timerText.SetText(Mathf.Max(0, remainingSeconds).ToString());
        }

        // ── 클릭 이벤트 ──────────────────────────────────────

        private void OnClickRoomSettings()
        {
            UIManager.Instance.ShowPopup<UIRoomSetting>().Forget();
        }

        private void OnClickReady()
        {
            if (localLobbyPlayer != null)
                localLobbyPlayer.CmdToggleReady();
        }

        private void OnClickStartGame()
        {
            if (!LobbyManager.Instance.IsHost) return;

            var option = LobbyManager.Instance.CurrentRoomOption;
            if (option == null) return;

            string mapName = option.mapName;

            Debug.Log($"[UILobby] OnClickStartGame | SteamActive={GameManager.SteamActive}, mapName=\"{mapName}\"");

            if (GameManager.SteamActive && WorkshopMapId.IsWorkshop(mapName))
            {
                if (WorkshopMapId.TryGetFileId(mapName, out ulong fileId))
                {
                    Debug.Log($"[UILobby] Workshop 맵 감지 → RequestCheck 호출 | fileId={fileId}");
                    if (startGameButton != null)
                        startGameButton.SetInteractable(false);

                    WorkshopMapChecker.RequestCheck(fileId);
                    return;
                }
                else
                {
                    Debug.LogError($"[UILobby] fileId 파싱 실패! mapName=\"{mapName}\"");
                }
            }

            Debug.Log("[UILobby] 일반 맵 → ProceedStartGame 직행");
            ProceedStartGame();
        }

        public void ProceedStartGame()
        {
            LobbyManager.Instance.ChangeLobbyStatusToInGame();
            GameRoundManager.Instance?.StartGameWithOption(LobbyManager.Instance.CurrentRoomOption);
        }

        public void NotifyWorkshopCheckFailed()
        {
            RefreshReadyUI();
        }

        private void OnClickCopyCode()
        {
            GUIUtility.systemCopyBuffer = LobbyManager.Instance.CurrentRoomCode;
        }

        private void OnClickLeave()
        {
            GameManager.Instance.LeaveSession();
        }

        private void OnClickToggleCode()
        {
            _isCodeVisible = !_isCodeVisible;
            ApplyCodeVisibility();
            RefreshRoomCode();
        }

        private void OnClickSetting()
        {
            UIManager.Instance.ShowPopup<UISetting>().Forget();
        }

        private void OnClickLanguageButton()
        {
            UIManager.Instance.ShowPopup<UILanguageSelect>().Forget();
        }
    }
}
