using Cysharp.Threading.Tasks;
using Mirror;
using Mirror.FizzySteam;
using Steamworks;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PixelChroma
{
    public class ChromaNetworkManager : NetworkManager
    {
        [Header("Chroma Scene Settings")]
        [Scene] public string inGameScene;

        [Header("Chroma Prefabs")]
        public GameObject lobbyPlayerPrefab;
        public GameObject runnerPrefab;
        public GameObject seekerPrefab;

        private MapRegistrySO MapRegistry => LobbyManager.Instance != null ? LobbyManager.Instance.MapRegistry : null;

        public override void Awake()
        {
            if (singleton != null && singleton != this)
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }

            base.Awake();

            ConfigureTransport();
        }

        // Steam 사용 여부에 따라 활성 Transport를 런타임에 확정한다.
        private void ConfigureTransport()
        {
            var fizzy = GetComponent<FizzySteamworks>();
            var kcp = GetComponent<kcp2k.KcpTransport>();

            if (GameManager.SteamActive)
            {
                // 빌드: Steam 릴레이(Fizzy) 사용
                if (kcp != null) kcp.enabled = false;
                if (fizzy != null) { fizzy.enabled = true; Transport.active = fizzy; }
            }
            else
            {
                // 에디터/로컬: Steam 미사용 → KCP(localhost) 사용
                if (fizzy != null) fizzy.enabled = false;
                if (kcp != null) { kcp.enabled = true; Transport.active = kcp; }
            }
        }

        // ───────────────────────── 서버 Lifecycle ─────────────────────────

        public override void OnStartServer()
        {
            base.OnStartServer();

            ScoreManager.Instance?.ResetAll();
            ScoreManager.Instance?.ClearRanks();

            if (!GameManager.SteamActive && ChromaNetworkDiscovery.Instance != null)
                ChromaNetworkDiscovery.Instance.AdvertiseServer();
        }

        public override void OnStopServer()
        {
            base.OnStopServer();

            LikeManager.Clear();
            NicknameRegistry.Clear();

            if (GameManager.SteamActive)
            {
                LobbyManager.Instance?.ForceLeaveIfActive();
            }
            else
            {
                if (ChromaNetworkDiscovery.Instance != null)
                    ChromaNetworkDiscovery.Instance.StopDiscovery();
            }
        }

        // ──────────────────────── 클라이언트 Lifecycle ────────────────────────

        public override void OnStartClient()
        {
            base.OnStartClient();

            RankClient.Reset();
            ScoreBoardClient.Reset();

            if (GameRoundManager.Instance != null)
                GameRoundManager.Instance.Initialize();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();

            if (GameRoundManager.Instance != null)
                GameRoundManager.Instance.Cleanup();
        }

        // ─────────────────────────── 연결 (Connect) ───────────────────────────

        public override void OnServerConnect(NetworkConnectionToClient conn)
        {
            base.OnServerConnect(conn);
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            base.OnServerDisconnect(conn);
            NicknameRegistry.Unregister(conn.connectionId);

            WorkshopMapChecker.HandlePlayerDisconnected(conn.connectionId);

            if (GameRoundManager.Instance != null)
                GameRoundManager.Instance.RemovePlayer(conn.connectionId);
        }

        public override void OnClientConnect()
        {
            base.OnClientConnect();

            if (LobbyManager.Instance != null)
                LobbyManager.Instance.NotifyClientConnected();

            // 호스트는 StartHost 시 OnClientSceneChanged가 호출되지 않으므로 여기서 로비로 진입한다.
            // 원격 클라이언트는 씬 로드 완료 후 OnClientSceneChanged에서 진입한다. (EnterLobby 중복 호출 방지)
            if (NetworkServer.active && GameManager.Instance != null)
                GameManager.Instance.EnterLobby().Forget();
        }

        public override void OnClientDisconnect()
        {
            base.OnClientDisconnect();

            // 접속 시도 자체가 실패한 경우는 엔트리 팝업(빠른참가/코드참가 등)이 재시도 UI로 처리한다.
            bool handledAsAttemptFailure =
                LobbyManager.Instance != null && LobbyManager.Instance.NotifyClientDisconnected();

            if (!handledAsAttemptFailure && GameManager.Instance != null)
                GameManager.Instance.HandleDisconnect();
        }

        // ─────────────────────────── 씬 전환 (Scene) ───────────────────────────

        // 서버 씬 로드가 시작되기 직전에 호출된다. (호스트 최초 시작 시엔 클라 씬 콜백이 생략되므로 여기서 로딩을 띄운다)
        public override void OnServerChangeScene(string newSceneName)
        {
            base.OnServerChangeScene(newSceneName);
            ShowLoadingIfManagedScene(newSceneName);
        }

        public override void OnServerSceneChanged(string sceneName)
        {
            string scene = GetSceneName(sceneName);

            if (IsInGameScene(scene))
                LoadSelectedMap();

            base.OnServerSceneChanged(sceneName);

            if (IsInGameScene(scene) && GameRoundManager.Instance != null)
                GameRoundManager.Instance.BeginRound();

            else if (IsLobbyScene(scene))
            {
                InGameManager.Instance?.ResetState();
                LobbyManager.Instance?.ChangeLobbyStatusToLobby();
                LikeManager.ClearRecords();

                ScoreManager.Instance?.SaveRanks();  // ResetAll 전에 호출!
                int firstConn = ScoreManager.Instance?.FirstConnectionId ?? -1;
                int secondConn = ScoreManager.Instance?.SecondConnectionId ?? -1;
                int thirdConn = ScoreManager.Instance?.ThirdConnectionId ?? -1;
                int lastConn = ScoreManager.Instance?.LastConnectionId ?? -1;
                ScoreManager.Instance?.ResetAll();

                bool hasAnyRank = firstConn >= 0 || secondConn >= 0 || thirdConn >= 0 || lastConn >= 0;
                if (hasAnyRank)
                    SendRanksAfterReady(firstConn, secondConn, thirdConn, lastConn).Forget();
            }

        }



        // 씬 로드가 시작되기 직전에 호출된다.
        public override void OnClientChangeScene(string newSceneName, SceneOperation sceneOperation, bool customHandling)
        {
            base.OnClientChangeScene(newSceneName, sceneOperation, customHandling);

            ScoreBoardClient.Reset();
            RankClient.Reset();

            ShowLoadingIfManagedScene(newSceneName);
        }

        public override void OnClientSceneChanged()
        {
            string currentScene = SceneManager.GetActiveScene().name;

            if (IsInGameScene(currentScene) && !NetworkServer.active)
                LoadSelectedMap();

            base.OnClientSceneChanged();

            if (IsInGameScene(currentScene) && GameManager.Instance != null)
                GameManager.Instance.EnterInGame().Forget();
            else if (IsLobbyScene(currentScene) && GameManager.Instance != null)
            {
                GameManager.Instance.EnterLobby().Forget();
            }
        }

        // ─────────────────────────── 플레이어 스폰 ───────────────────────────

        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            string currentScene = SceneManager.GetActiveScene().name;

            GameObject playerInstance = null;
            if (IsLobbyScene(currentScene))
                playerInstance = Instantiate(
                    lobbyPlayerPrefab,
                    LobbySpawnPoint.GetSpawnPosition(lobbyPlayerPrefab.transform.position),
                    Quaternion.identity);
            else if (IsInGameScene(currentScene))
                playerInstance = CreateInGamePlayer(conn);

            if (playerInstance == null) return;

            // 명부에 저장된 닉네임이 있으면 스폰된 오브젝트에 입힌다.
            if (NicknameRegistry.TryGet(conn.connectionId, out var savedName))
            {
                var playerBase = playerInstance.GetComponent<PlayerBase>();
                if (playerBase != null)
                    playerBase.playerName = savedName;
            }

            if (NicknameRegistry.TryGetPixel(conn.connectionId, out var savedPixelPng))
            {
                var playerBase = playerInstance.GetComponent<PlayerBase>();
                if (playerBase != null)
                    playerBase.pixelNicknamePng = savedPixelPng;
            }


            NetworkServer.AddPlayerForConnection(conn, playerInstance);
        }

        // 인게임 씬: 라운드에서 역할을 결정하고, 역할에 맞는 프리팹·위치로 스폰한다.
        private GameObject CreateInGamePlayer(NetworkConnectionToClient conn)
        {
            var round = GameRoundManager.Instance;
            PlayerRole assignedRole = (round != null && round.TryGetRole(conn.connectionId, out var r))
                ? r
                : PlayerRole.Runner;

            GameObject prefab = assignedRole == PlayerRole.Seeker ? seekerPrefab : runnerPrefab;

            Vector3 spawnPos = Vector3.zero;
            Quaternion spawnRot = Quaternion.identity;

            if (InGameManager.Instance != null)
            {
                spawnPos = (assignedRole == PlayerRole.Seeker)
                    ? InGameManager.Instance.GetWaitingPosition()
                    : InGameManager.Instance.GetSpawnPosition();
            }
            else
            {
                Transform startPos = GetStartPosition();
                if (startPos != null)
                {
                    spawnPos = startPos.position;
                    spawnRot = startPos.rotation;
                }
            }

            GameObject instance = Instantiate(prefab, spawnPos, spawnRot);

            var playerBase = instance.GetComponent<PlayerBase>();
            if (playerBase != null)
                playerBase.role = assignedRole;

            round?.RemovePlayer(conn.connectionId);
            return instance;
        }

        // ─────────────────────────── 커스텀 로직 / 유틸 ───────────────────────────

        [Server]
        public void ServerConvertRunnerToSeeker(RunnerPlayer runner)
        {
            if (runner == null) return;

            NetworkConnectionToClient conn = runner.connectionToClient;
            if (conn == null) return;

            Vector3 spawnPos = InGameManager.Instance != null
                ? InGameManager.Instance.GetSpawnPosition()
                : Vector3.zero;

            GameObject seekerInstance = Instantiate(seekerPrefab, spawnPos, Quaternion.identity);

            var seekerBase = seekerInstance.GetComponent<PlayerBase>();
            if (seekerBase != null)
            {
                seekerBase.role = PlayerRole.Seeker;
                seekerBase.playerName = runner.playerName;
                seekerBase.pixelNicknamePng = runner.pixelNicknamePng;
            }

            NetworkServer.ReplacePlayerForConnection(conn, seekerInstance, ReplacePlayerOptions.KeepActive);
        }

        public bool IsInGameScene(string sceneName)
        {
            return sceneName == GetSceneName(inGameScene);
        }

        public bool IsLobbyScene(string sceneName)
        {
            return sceneName == GetSceneName(onlineScene);
        }

        public string GetSceneName(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath)) return string.Empty;
            return System.IO.Path.GetFileNameWithoutExtension(scenePath);
        }

        // 로비/인게임 등 네트워크가 관리하는 씬으로의 전환이면 로딩 화면을 띄운다. (서버/클라 씬 전환 공통)
        private void ShowLoadingIfManagedScene(string newSceneName)
        {
            string scene = GetSceneName(newSceneName);
            if (IsInGameScene(scene) || IsLobbyScene(scene))
                LoadingManager.Show().Forget();
        }

        private void LoadSelectedMap()
        {
            var roomOption = GameRoundManager.Instance?.CurrentRoomOption;
            if (roomOption == null) return;

            string mapName = roomOption.mapName;

            // Workshop 맵 분기
            if (WorkshopMapId.TryGetFileId(mapName, out ulong fileId))
            {
                string path = WorkshopSubscriptionService.GetInstallPath(fileId);

                if (!string.IsNullOrEmpty(path))
                    MapLoader.Instance.LoadWorkshopMap(path);
                else
                    Debug.LogError("Workshop 맵 미설치: " + fileId);
                return;
            }

            // 기존 내장 맵 로딩 (변경 없음)
            var mapData = MapRegistry?.FindById(mapName);
            if (mapData?.mapPrefab != null)
                MapLoader.Instance.LoadBuiltInMap(mapData);
        }

        private async UniTaskVoid SendRanksAfterReady(int firstConn, int secondConn, int thirdConn, int lastConn)
        {
            // 클라이언트들이 로비 씬 로드 완료할 때까지 대기
            float elapsed = 0f;
            while (elapsed < 5f)
            {
                if (NetworkServer.connections.Values.All(conn => conn.isReady))
                    break;
                await UniTask.Delay(System.TimeSpan.FromSeconds(0.5f));
                elapsed += 0.5f;
            }

            // connectionId → 로비 플레이어의 netId 변환 (헬퍼 메서드 사용)
            uint firstNetId = ConnIdToNetId(firstConn);
            uint secondNetId = ConnIdToNetId(secondConn);
            uint thirdNetId = ConnIdToNetId(thirdConn);
            uint lastNetId = ConnIdToNetId(lastConn);

            NetworkServer.SendToAll(new RankMessage
            {
                firstNetId = firstNetId,
                secondNetId = secondNetId,
                thirdNetId = thirdNetId,
                lastNetId = lastNetId
            });

            ScoreManager.Instance?.ClearRanks();
        }

        private uint ConnIdToNetId(int connId)
        {
            if (connId < 0) return 0;
            if (NetworkServer.connections.TryGetValue(connId, out var conn) && conn.identity != null)
                return conn.identity.netId;
            return 0;
        }
    }
}
