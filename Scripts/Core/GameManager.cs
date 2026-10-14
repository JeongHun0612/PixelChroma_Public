using Cysharp.Threading.Tasks;
using Mirror;
using PixelChroma.UI;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;

namespace PixelChroma
{
    public class GameManager : MonoSingleton<GameManager>
    {
        private const float NetworkLoadTimeout = 15f;
        private const float InitialTitleBgmFadeInDuration = 0.3f;

        [Header("Localization")]
        [SerializeField] private FontTableSO fontTable;

        [Header("Scene Essentials")]
        [SerializeField] private GameObject mainCameraPrefab;
        [SerializeField] private GameObject cinemachineCameraPrefab;

        private bool _handlingDisconnect;
        private bool _voluntaryLeave;
        private LobbyFailureReason _pendingFailure = LobbyFailureReason.None;

        public GameState CurrentState { get; private set; } = GameState.Boot;
        public static bool SteamInitialized { get; private set; }

        private async void Start()
        {
            await Boot();
        }

        private void OnEnable()
        {
            LoadingManager.OnTimedOut -= HandleLoadingTimedOut;
            LoadingManager.OnTimedOut += HandleLoadingTimedOut;
        }

        private void OnDisable()
        {
            LoadingManager.OnTimedOut -= HandleLoadingTimedOut;
        }

        private async UniTask Boot()
        {
            // 안쪽 타임아웃(스폰/씬 대기)이 바깥 워치독보다 먼저 동작해야 graceful 복구가 보장된다.
            Debug.Assert(NetworkLoadTimeout < LoadingManager.WatchdogTimeout,
                "[GameManager] NetworkLoadTimeout은 LoadingManager.WatchdogTimeout보다 짧아야 합니다.");

            // 저장된 화면/그래픽 옵션(화면 모드·해상도·VSync·화면 흔들림)을 읽어 즉시 적용합니다.
            GameSettings.Load();

            // Localization 시스템을 먼저 초기화합니다.
            var initOp = LocalizationSettings.InitializationOperation;
            await UniTask.WaitUntil(() => initOp.IsDone);
            LocalizationManager.Initialize(fontTable);

            // 로케일·폰트 적용 (Steam 정상 & 최초 실행이면 Steam 언어를 기본값으로 사용)
            await LocalizationManager.ApplySavedLocaleAsync();

            var initialLoading = UIManager.Instance.InitialLoading;

            // 초기 로딩 화면을 띄웁니다.
            if (initialLoading != null)
            {
                initialLoading.Initialize();
                initialLoading.RegisterLocalizedTexts();
                initialLoading.Show().Forget();
            }

            // Steam 초기화 실패 시 경고 패널을 띄우고 부팅을 중단합니다(종료는 종료 버튼이 처리).
            if (!InitializeSteam())
            {
                Debug.LogWarning("[GameManager] Steam 초기화 실패 — 경고 패널을 표시합니다.");
                if (initialLoading != null)
                    await initialLoading.ShowSteamWarning();
                return;
            }

            await UIManager.Instance.InitializeAsync();

            // 초기 로딩 화면을 숨깁니다.
            if (initialLoading != null)
                await initialLoading.Hide();

            await EnterMainMenu();
        }

        // 스팀을 사용하는(초기화된) 빌드인지 여부. 에디터는 localhost로 테스트하므로 스팀을 쓰지 않습니다.
        // 스팀 콜백 등록·로비 API 등 스팀 의존 로직을 한 곳에서 켜고 끄기 위한 단일 게이트입니다.
        public static bool SteamActive =>
#if UNITY_EDITOR
            false;
#else
            true;
#endif

        private bool InitializeSteam()
        {
            // 스팀 미사용(에디터/로컬)이면 초기화를 건너뛰고 성공으로 처리합니다.
            if (!SteamActive)
                return true;

            SteamInitialized = Steamworks.SteamAPI.Init();
            return SteamInitialized;
        }

        public void LeaveSession()
        {
            LeaveSessionAsync().Forget();
        }

        private async UniTask LeaveSessionAsync()
        {
            _voluntaryLeave = true;

            // StopHost/StopClient가 offlineScene 로드를 시작하기 전에 화면을 먼저 덮는다.
            await LoadingManager.Show();

            if (LobbyManager.Instance != null)
                LobbyManager.Instance.LeaveLobby();
        }

        public void HandleDisconnect()
        {
            HandleDisconnectAsync().Forget();
        }

        private async UniTask HandleDisconnectAsync()
        {
            if (_handlingDisconnect) return;
            _handlingDisconnect = true;

            try
            {
                bool voluntary = _voluntaryLeave;
                _voluntaryLeave = false;

                if (!voluntary && _pendingFailure == LobbyFailureReason.None)
                    _pendingFailure = LobbyFailureReason.Disconnected;

                Debug.LogWarning("[GameManager] 서버와의 연결이 끊어졌습니다. 메인 메뉴로 복귀합니다.");

                // 로딩 팝업이 실제로 활성화된 뒤에 타이틀 UI 전환을 시작한다.
                // Forget으로 병렬 실행하면 UITitle의 SetActive가 먼저 호출되어 한 프레임 노출될 수 있다.
                await LoadingManager.Show();

                if (InGameManager.Instance != null)
                    InGameManager.Instance.ResetState();

                if (LobbyManager.Instance != null && LobbyManager.Instance.CurrentLobbyId.IsValid())
                    LobbyManager.Instance.LeaveLobby();

                await EnterMainMenu();
            }
            finally
            {
                // 로딩 표시 자체에서 예외가 발생해도 이후 복구 시도를 막지 않는다.
                _handlingDisconnect = false;
            }
        }

        public async UniTask EnterMainMenu()
        {
            try
            {
                bool isInitialEntry = CurrentState == GameState.Boot;
                CurrentState = GameState.MainMenu;
                EnsureSceneEssentials();

                if (CursorManager.Instance != null)
                    CursorManager.Instance.SetCursorType(CursorType.Default);

                await UIManager.Instance.ShowScene<UITitle>();

                if (LoadingManager.IsShowing)
                {
                    var netManager = NetworkManager.singleton as ChromaNetworkManager;
                    string titleScene = netManager != null ? netManager.GetSceneName(netManager.offlineScene) : null;

                    await WaitForActiveScene(titleScene);
                    await LoadingManager.Hide();
                }

                // 로딩 화면이 완전히 닫힌 뒤 현재 씬 BGM을 시작한다.
                if (SoundManager.Instance != null)
                {
                    if (isInitialEntry)
                        SoundManager.Instance.PlayBgm(BgmType.Title, InitialTitleBgmFadeInDuration).Forget();
                    else
                        SoundManager.Instance.PlayBgm(BgmType.Title).Forget();
                }

                // 로딩 이후 실패로 복귀한 경우: 로딩을 완전히 내린 뒤 공통 경고 팝업으로 사유를 명확히 알린다.
                if (_pendingFailure != LobbyFailureReason.None)
                {
                    var reason = _pendingFailure;
                    _pendingFailure = LobbyFailureReason.None;

                    UIManager.Instance.ShowPopup<UIwarningTextPopup>(p => p.SetReason(reason)).Forget();
                }
            }
            finally
            {
                _handlingDisconnect = false;
            }
        }

        public async UniTask EnterLobby()
        {
            CurrentState = GameState.Lobby;
            EnsureSceneEssentials();

            if (CursorManager.Instance != null)
                CursorManager.Instance.SetCursorType(CursorType.Default);

            if (!await WaitForLocalPlayer())
            {
                if (NetworkClient.active)
                    FailToTitle(LobbyFailureReason.Timeout, "로비 플레이어 스폰 대기 시간 초과");
                return;
            }

            await LoadingManager.Hide();

            if (!NetworkClient.active)
                return;

            if (SoundManager.Instance != null)
                SoundManager.Instance.PlayBgm(BgmType.Lobby).Forget();

            await UIManager.Instance.ShowScene<UILobby>();
        }

        public async UniTask EnterInGame()
        {
            CurrentState = GameState.InGame;
            EnsureSceneEssentials();

            if (!await WaitForLocalPlayer())
            {
                if (NetworkClient.active)
                    FailToTitle(LobbyFailureReason.Timeout, "로컬 플레이어 스폰 대기 시간 초과");
                return;
            }

            await LoadingManager.Hide();

            if (SoundManager.Instance != null)
                SoundManager.Instance.PlayBgm(BgmType.InGame).Forget();

            var localPlayer = NetworkClient.localPlayer?.GetComponent<PlayerBase>();
            if (localPlayer != null)
                localPlayer.ApplyDefaultCursor();
        }

        // 씬+맵 로드와 로컬 플레이어 스폰이 끝났는지 대기한다. 시간 초과 또는 연결 끊김 시 false.
        private async UniTask<bool> WaitForLocalPlayer()
        {
            float elapsed = 0f;
            // 대기 중 방이 사라져 연결이 끊기면(NetworkClient.active == false) 타임아웃까지 기다리지 않고 즉시 중단한다.
            while (NetworkClient.localPlayer == null && NetworkClient.active && elapsed < NetworkLoadTimeout)
            {
                await UniTask.Yield();
                elapsed += Time.unscaledDeltaTime;
            }

            return NetworkClient.localPlayer != null && NetworkClient.active;
        }

        // 지정한 씬이 활성화될 때까지 대기한다(teardown 시 offlineScene 로드 완료 확인). 시간 초과 시 그냥 반환.
        private async UniTask WaitForActiveScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return;

            float elapsed = 0f;
            while (SceneManager.GetActiveScene().name != sceneName && elapsed < NetworkLoadTimeout)
            {
                await UniTask.Yield();
                elapsed += Time.unscaledDeltaTime;
            }
        }

        // 로딩 이후(씬 전환/스폰) 단계의 복구 불가 상황: 사유를 실어 타이틀로 강제 복귀시킨다.
        // 사유는 EnterMainMenu에서 로딩을 내린 뒤 토스트로 표시된다.
        private void FailToTitle(LobbyFailureReason reason, string debugDetail)
        {
            Debug.LogWarning($"[GameManager] {debugDetail} — 메인 메뉴로 복귀합니다. (사유: {reason})");

            if (_pendingFailure == LobbyFailureReason.None)
                _pendingFailure = reason;

            HandleDisconnect();
        }

        private void HandleLoadingTimedOut()
        {
            FailToTitle(LobbyFailureReason.Timeout, "로딩 화면 워치독 타임아웃");
        }

        // 씬 전환 시 필수 오브젝트를 보장한다. 없으면 프리팹으로 생성한다.
        private void EnsureSceneEssentials()
        {
            EnsureEventSystem();
            EnsureMainCamera();

            // vcam은 Title(메인 메뉴)을 제외한 모든 씬(로비·인게임)에서 필요하다.
            // 스폰된 로컬 플레이어가 PlayerBase.SetupCinemachineFollow에서 이 vcam을 찾아 Follow를 건다.
            if (CurrentState != GameState.MainMenu)
                EnsureCinemachineCamera();
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null) 
                return;

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        // Main Camera(Brain·Camera·AudioListener)는 맵마다 동일한 제네릭 오브젝트이므로
        // 씬에 없으면 프리팹으로 생성한다. Camera.main 가드로 중복 AudioListener를 막는다.
        private void EnsureMainCamera()
        {
            if (Camera.main != null) return;

            if (mainCameraPrefab == null)
            {
                Debug.LogError("[GameManager] mainCameraPrefab이 할당되지 않았습니다!");
                return;
            }

            Instantiate(mainCameraPrefab);
        }

        // vcam은 Confiner 같은 맵 고유 씬 참조가 없어 제네릭하게 생성할 수 있다.
        // Follow 타겟은 프리팹에서 비어 있고, 로컬 플레이어 스폰 시 런타임에 바인딩된다.
        private void EnsureCinemachineCamera()
        {
            if (FindAnyObjectByType<CinemachineCamera>() != null) return;

            if (cinemachineCameraPrefab == null)
            {
                Debug.LogError("[GameManager] cinemachineCameraPrefab이 할당되지 않았습니다!");
                return;
            }

            Instantiate(cinemachineCameraPrefab);
        }
    }
}
