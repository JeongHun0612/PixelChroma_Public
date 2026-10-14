using System;
using Steamworks;
using UnityEngine;
using Mirror;

namespace PixelChroma
{
    public class LobbyManager : MonoSingleton<LobbyManager>
    {
        public const int CodeLength = 6;

        // 스팀 로비 식별
        public const string GameKey = "GameID";
        public const string GameName = "PixelChroma_Dev";

        // 방 상태
        public const string IsPublicKey = "IsPublic";
        public const string RoomStatusKey = "RoomStatus";
        public const string StatusLobby = "Lobby";
        public const string StatusInGame = "InGame";

        // 방 메타데이터
        public const string GameModeKey = "GameMode";
        public const string MapIdKey = "MapId";
        public const string LanguageKey = "Language";
        public const string RoomNameKey = "RoomName";
        public const string HostNameKey = "HostName";

        private const string CodeChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        private const string HostAddressKey = "HostAddress";
        private const string CodeKey = "RoomCode";


        [Header("Map List")]
        [SerializeField] private MapRegistrySO mapRegistry;

        public event Action<LobbyFailureReason> OnLobbyFailed;
        public event Action OnMembersChanged;

        private Callback<LobbyCreated_t> _lobbyCreated;             // CreateLobby() 호출 후 Steam 서버가 로비 생성 결과를 돌려줄 때
        private CallResult<LobbyMatchList_t> _lobbyMatchList;       // RequestLobbyList() 호출 후 검색 결과가 돌아왔을 때 (코드 참가 전용)
        private Callback<GameLobbyJoinRequested_t> _joinRequested;  // Steam 오버레이에서 친구가 "게임 참가"를 클릭했을 때
        private Callback<LobbyEnter_t> _lobbyEntered;               // JoinLobby() 호출 후 로비 입장이 완료되었을 때 (성공/실패 모두)
        private Callback<LobbyChatUpdate_t> _lobbyChatUpdate;       // 로비 멤버 변동 시 (입장, 퇴장, 추방 등)

        private bool _waitingForConnection;

        public RoomOption CurrentRoomOption { get; set; } = new();
        public string CurrentRoomCode
        {
            get => CurrentRoomOption?.roomCode;
            set { if (CurrentRoomOption != null) CurrentRoomOption.roomCode = value; }
        }
        public CSteamID CurrentLobbyId { get; private set; }

        public bool IsHost => NetworkServer.active;
        public MapRegistrySO MapRegistry => mapRegistry;



        protected override void Awake()
        {
            base.Awake();

            if (Instance != this) return;

            // 스팀이 활성인 빌드에서만 스팀 콜백을 등록합니다. (에디터는 localhost 테스트라 스팀 미사용)
            if (GameManager.SteamActive)
            {
                _lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
                _lobbyMatchList = CallResult<LobbyMatchList_t>.Create(OnLobbyMatchList);
                _joinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
                _lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEnteredCallback);
                _lobbyChatUpdate = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
            }
        }

        private void OnDestroy()
        {
            if (GameManager.SteamActive)
            {
                _lobbyCreated?.Dispose();
                _lobbyMatchList?.Dispose();
                _joinRequested?.Dispose();
                _lobbyEntered?.Dispose();
                _lobbyChatUpdate?.Dispose();
            }
        }

        private void OnApplicationQuit()
        {
            ForceLeaveIfActive();
        }

        public void NotifyClientConnected()
        {
            _waitingForConnection = false;
            Debug.Log($"[LobbyManager] 클라이언트 연결 성공 | 코드: {CurrentRoomCode}");
        }

        public bool NotifyClientDisconnected()
        {
            if (_waitingForConnection)
            {
                _waitingForConnection = false;
                Debug.LogWarning("[LobbyManager] 클라이언트 연결 실패");
                OnLobbyFailed?.Invoke(LobbyFailureReason.ConnectFailed);
                return true;
            }

            return false;
        }

        public void ForceLeaveIfActive() // 로비 퇴장, 방 정리
        {
            if (!GameManager.SteamActive) return;
            if (!CurrentLobbyId.IsValid()) return;

            SteamMatchmaking.LeaveLobby(CurrentLobbyId);
            CurrentLobbyId = CSteamID.Nil;
        }

        // ── 1. 방 만들기 ─────────────────────────
        public void CreateLobby(RoomOption option)
        {
            if (IsHost || NetworkClient.active)
            {
                Debug.LogWarning("[LobbyManager] 이미 연결 중입니다! 방 생성을 거부합니다.");
                OnLobbyFailed?.Invoke(LobbyFailureReason.AlreadyConnected);
                return;
            }

            CurrentRoomOption = ClampRoomOption(option, RoomOption.MinPlayers);
            CurrentRoomOption.hostName = TextNicknameService.LoadOrDefault();

            NetworkManager.singleton.maxConnections = CurrentRoomOption.maxPlayers;

            // 스팀 미사용(에디터/로컬): 곧바로 localhost 호스트로 시작합니다.
            if (!GameManager.SteamActive)
            {
                CurrentRoomCode = GenerateCode();
                Debug.Log($"[LobbyManager] 로컬 방 생성 시도 (localhost) | 코드: {CurrentRoomCode} | 모드: {option.gameMode} | 공개: {option.isPublic}");
                TryStartHost();
                return;
            }

            var lobbyType = CurrentRoomOption.isPublic ? ELobbyType.k_ELobbyTypePublic : ELobbyType.k_ELobbyTypeFriendsOnly;
            // 스팀 로비 생성 (인원 제한 포함). 결과는 OnLobbyCreated 콜백에서 처리됩니다.
            SteamMatchmaking.CreateLobby(lobbyType, CurrentRoomOption.maxPlayers);
        }

        private bool TryStartHost()
        {
            try
            {
                NetworkManager.singleton.StartHost();
            }
            catch (Exception e)
            {
                Debug.LogError($"[LobbyManager] 호스트 시작 중 예외: {e.Message}");
                OnLobbyFailed?.Invoke(LobbyFailureReason.CreateFailed);
                return false;
            }

            if (!NetworkServer.active)
            {
                Debug.LogError("[LobbyManager] 호스트 시작 실패 (서버가 활성화되지 않음)");
                OnLobbyFailed?.Invoke(LobbyFailureReason.CreateFailed);
                return false;
            }

            return true;
        }

        // ── 2. 코드로 참가 ─────────────────────────
        public void JoinLobbyByCode(string code)
        {
            code = code.Trim().ToUpper();

            if (!GameManager.SteamActive)
            {
                StartLocalClient(code);
                return;
            }

            SteamMatchmaking.AddRequestLobbyListStringFilter(GameKey, GameName, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter(CodeKey, code, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
            SteamMatchmaking.AddRequestLobbyListFilterSlotsAvailable(1);

            SteamAPICall_t handle = SteamMatchmaking.RequestLobbyList();
            _lobbyMatchList.Set(handle);
        }

        // ── 3. 빠른 참가 ─────────────────────────
        public void JoinQuickMatch()
        {
            if (!GameManager.SteamActive)
            {
                StartLocalClient();
                return;
            }

            var searcher = QuickMatchSearcher.Instance;
            searcher.OnMatchFound -= HandleQuickMatchFound;
            searcher.OnMatchFailed -= HandleQuickMatchFailed;
            searcher.OnMatchFound += HandleQuickMatchFound;
            searcher.OnMatchFailed += HandleQuickMatchFailed;
            searcher.StartSearch();
        }

        private void HandleQuickMatchFound(CSteamID lobbyId)
        {
            UnsubscribeQuickMatch();
            SteamMatchmaking.JoinLobby(lobbyId);
        }

        private void HandleQuickMatchFailed()
        {
            UnsubscribeQuickMatch();
            OnLobbyFailed?.Invoke(LobbyFailureReason.QuickMatchNotFound);
        }

        public void CancelQuickMatch()
        {
            UnsubscribeQuickMatch();
            QuickMatchSearcher.Instance?.Cancel();
        }

        private void UnsubscribeQuickMatch()
        {
            var searcher = QuickMatchSearcher.Instance;
            if (searcher == null) return;
            searcher.OnMatchFound -= HandleQuickMatchFound;
            searcher.OnMatchFailed -= HandleQuickMatchFailed;
        }

        // ── 4. 방 목록에서 선택한 방에 입장 ─────────────────────────
        public void JoinRoom(RoomListEntry entry)
        {
            if (IsHost || NetworkClient.active)
            {
                Debug.LogWarning("[LobbyManager] 이미 연결 중입니다! 중복 입장을 거부합니다.");
                OnLobbyFailed?.Invoke(LobbyFailureReason.AlreadyConnected);
                return;
            }

            if (entry.IsSteamRoom)
            {
                SteamMatchmaking.JoinLobby(entry.steamLobbyId);
            }
            else
            {
                _waitingForConnection = true;
                NetworkManager.singleton.networkAddress = entry.connectAddress;
                NetworkManager.singleton.StartClient();
            }
        }

        // ── 로비 안에서 방 설정이 변경되었을 때 호출할 함수 ──
        public void UpdateLobbySettings(RoomOption newOption)
        {
            // 방장이 아니면 튕겨냅니다. (스팀 로비 ID 체크는 여기서 하지 않습니다)
            if (!IsHost) return;

            string preservedCode = CurrentRoomCode;
            string preservedHostName = CurrentRoomOption?.hostName ?? "";
            CurrentRoomOption = ClampLobbyEditableOption(newOption);
            CurrentRoomOption.roomCode = preservedCode;
            CurrentRoomOption.hostName = preservedHostName;

            // 2. Mirror 엔진 접속 제한 즉시 업데이트
            if (NetworkManager.singleton != null)
            {
                NetworkManager.singleton.maxConnections = CurrentRoomOption.maxPlayers;
            }

            // 3. 스팀이 활성일 때만 스팀 서버 쪽에 인원/공개 여부를 갱신 요청합니다.
            if (GameManager.SteamActive && CurrentLobbyId.IsValid())
            {
                SteamMatchmaking.SetLobbyMemberLimit(CurrentLobbyId, CurrentRoomOption.maxPlayers);
                ApplyLobbyMetadata();
            }

            LobbyPlayerState.BroadcastRoomOption();

            Debug.Log($"[LobbyManager] 방 설정 갱신 완료! 최대 인원: {CurrentRoomOption.maxPlayers}명, 숨는 시간: {CurrentRoomOption.hideTime}초");
        }
 
        // 스팀 미사용(에디터/로컬) 전용 로컬 클라이언트 접속 (코드 참가 / 빠른 참가 공용)
        private void StartLocalClient(string roomCode = null)
        {
            if (IsHost)
            {
                Debug.LogWarning("[LobbyManager] 이미 서버가 실행 중입니다! 중복 입장을 거부합니다.");
                OnLobbyFailed?.Invoke(LobbyFailureReason.AlreadyConnected);
                return;
            }

            if (!string.IsNullOrEmpty(roomCode))
                CurrentRoomCode = roomCode;

            _waitingForConnection = true;
            Debug.Log(roomCode != null
                ? $"[LobbyManager] 로컬 접속 시도 (localhost) | 입력한 코드: {roomCode}"
                : "[LobbyManager] 로컬 빠른 참가 시도 (localhost)");

            NetworkManager.singleton.networkAddress = "localhost";
            NetworkManager.singleton.StartClient();
        }

        private void OnLobbyCreated(LobbyCreated_t result)
        {
            if (result.m_eResult != EResult.k_EResultOK)
            {
                OnLobbyFailed?.Invoke(LobbyFailureReason.CreateFailed);
                return;
            }

            CurrentLobbyId = new CSteamID(result.m_ulSteamIDLobby);
            CurrentRoomCode = GenerateCode();

            ApplyLobbyMetadata();

            Debug.Log($"[LobbyManager] 스팀 방 생성 완료 | 코드: {CurrentRoomCode}");

            // Mirror 호스트 시작에 실패하면 방금 만든 스팀 로비도 함께 정리한다. (유령 로비 방지)
            if (!TryStartHost())
                ForceLeaveIfActive();
        }

        private void OnLobbyMatchList(LobbyMatchList_t result, bool ioFailure)
        {
            if (ioFailure || result.m_nLobbiesMatching == 0)
            {
                Debug.LogWarning("[LobbyManager] 조건에 맞는 방을 찾지 못했습니다.");
                OnLobbyFailed?.Invoke(LobbyFailureReason.CodeRoomNotFound);
                return;
            }

            CSteamID matchedLobbyID = SteamMatchmaking.GetLobbyByIndex(0);


            string status = SteamMatchmaking.GetLobbyData(matchedLobbyID, RoomStatusKey);
            if (status == StatusInGame)
            {
                Debug.LogWarning("[LobbyManager] 코드에 해당하는 방이 이미 게임 진행 중입니다.");
                OnLobbyFailed?.Invoke(LobbyFailureReason.RoomInGame);
                return;
            }

            SteamMatchmaking.JoinLobby(matchedLobbyID);
        }

        private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t result) { SteamMatchmaking.JoinLobby(result.m_steamIDLobby); }

        private void OnLobbyEnteredCallback(LobbyEnter_t result)
        {
            bool success = result.m_EChatRoomEnterResponse == (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess;
            if (!success)
            {
                Debug.LogError($"[LobbyManager] 방 입장 실패 코드: {result.m_EChatRoomEnterResponse}");
                OnLobbyFailed?.Invoke(LobbyFailureReason.JoinFailed);
                return;
            }

            CurrentLobbyId = new CSteamID(result.m_ulSteamIDLobby);

            if (IsHost) 
                return;

            CurrentRoomCode = SteamMatchmaking.GetLobbyData(CurrentLobbyId, CodeKey);
            string hostAddress = SteamMatchmaking.GetLobbyData(CurrentLobbyId, HostAddressKey);

            _waitingForConnection = true;
            Debug.Log($"[LobbyManager] 스팀 방 입장 완료, Mirror 연결 대기 중 | 코드: {CurrentRoomCode}");

            NetworkManager.singleton.networkAddress = hostAddress;
            NetworkManager.singleton.StartClient();
        }

        private void OnLobbyChatUpdate(LobbyChatUpdate_t result) { OnMembersChanged?.Invoke(); }

        public void ChangeLobbyStatusToInGame()
        {
            if (GameManager.SteamActive && NetworkServer.active)
                SteamMatchmaking.SetLobbyData(CurrentLobbyId, RoomStatusKey, StatusInGame);
        }

        public void ChangeLobbyStatusToLobby()
        {
            if (GameManager.SteamActive && NetworkServer.active)
                SteamMatchmaking.SetLobbyData(CurrentLobbyId, RoomStatusKey, StatusLobby);
        }

        public void LeaveLobby()
        {
            if (CurrentLobbyId.IsValid()) 
                SteamMatchmaking.LeaveLobby(CurrentLobbyId);

            CurrentLobbyId = CSteamID.Nil;
            CurrentRoomCode = null;

            if (IsHost) 
                NetworkManager.singleton.StopHost();
            else 
                NetworkManager.singleton.StopClient();
        }

        public void JoinLobbyDirect(CSteamID lobbyId)
        {
            SteamMatchmaking.JoinLobby(lobbyId);
        }

        // ── 유틸 ───────────────────────────────────────────────

        public int GetMemberCount() =>
            CurrentLobbyId.IsValid() ? SteamMatchmaking.GetNumLobbyMembers(CurrentLobbyId) : 0;

        public CSteamID GetMember(int index) =>
            SteamMatchmaking.GetLobbyMemberByIndex(CurrentLobbyId, index);

        private RoomOption ClampLobbyEditableOption(RoomOption option)
        {
            int memberCount = Mathf.Max(GetMemberCount(), LobbyPlayerState.AllPlayers.Count);
            int minPlayers = Mathf.Clamp(memberCount, RoomOption.MinPlayers, RoomOption.MaxPlayersLimit);

            return ClampRoomOption(option, minPlayers);
        }

        private static RoomOption ClampRoomOption(RoomOption option, int minPlayers)
        {
            RoomOption clamped = option?.Clone() ?? new RoomOption();
            minPlayers = Mathf.Clamp(minPlayers, RoomOption.MinPlayers, RoomOption.MaxPlayersLimit);

            clamped.maxPlayers = Mathf.Clamp(clamped.maxPlayers, minPlayers, RoomOption.MaxPlayersLimit);

            int maxSeeker = RoomOption.GetMaxSeekerCount(clamped.maxPlayers);
            clamped.seekerCount = Mathf.Clamp(clamped.seekerCount, RoomOption.MinSeekerCount, maxSeeker);
            clamped.hideTime = Mathf.Clamp(Mathf.Round(clamped.hideTime), RoomOption.MinHideTime, RoomOption.MaxHideTime);
            clamped.seekTime = Mathf.Clamp(Mathf.Round(clamped.seekTime), RoomOption.MinSeekTime, RoomOption.MaxSeekTime);
            clamped.checkTime = Mathf.Clamp(Mathf.Round(clamped.checkTime), RoomOption.MinCheckTime, RoomOption.MaxCheckTime);
            clamped.forcedExposeInterval = Mathf.Clamp(
                Mathf.Round(clamped.forcedExposeInterval),
                RoomOption.MinForcedExposeInterval,
                RoomOption.GetMaxForcedExposeInterval(clamped.seekTime));

            return clamped;
        }

        private static string GenerateCode()
        {
            var chars = new char[CodeLength];
            for (int i = 0; i < CodeLength; i++) chars[i] = CodeChars[UnityEngine.Random.Range(0, CodeChars.Length)];
            return new string(chars);
        }

        private void ApplyLobbyMetadata() // 방찾기 설정
        {
            if (!CurrentLobbyId.IsValid()) return;

            var option = CurrentRoomOption;

            SteamMatchmaking.SetLobbyData(CurrentLobbyId, GameKey, GameName);
            SteamMatchmaking.SetLobbyData(CurrentLobbyId, CodeKey, CurrentRoomCode);
            SteamMatchmaking.SetLobbyData(CurrentLobbyId, HostAddressKey, SteamUser.GetSteamID().ToString());
            SteamMatchmaking.SetLobbyData(CurrentLobbyId, IsPublicKey, option.isPublic ? "True" : "False");
            SteamMatchmaking.SetLobbyData(CurrentLobbyId, RoomStatusKey, StatusLobby);

            // ── 3단계 추가 ──
            SteamMatchmaking.SetLobbyData(CurrentLobbyId, GameModeKey, ((int)option.gameMode).ToString());
            SteamMatchmaking.SetLobbyData(CurrentLobbyId, MapIdKey, option.mapName ?? "");
            SteamMatchmaking.SetLobbyData(CurrentLobbyId, RoomNameKey, option.roomName ?? "");
            SteamMatchmaking.SetLobbyData(CurrentLobbyId, HostNameKey, option.hostName ?? "");
            SteamMatchmaking.SetLobbyData(CurrentLobbyId, LanguageKey, LocalizationManager.CurrentLanguageCode);
        }
    }
}
