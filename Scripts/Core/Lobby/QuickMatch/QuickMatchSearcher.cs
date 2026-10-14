using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Steamworks;
using UnityEngine;

namespace PixelChroma
{
    public class QuickMatchSearcher : MonoSingleton<QuickMatchSearcher>
    {
        // Phase별 대기 시간(초). Phase 0은 즉시, 이후 10초 간격
        private static readonly float[] PhaseDelays = { 0f, 3f, 3f, 3f };

        private CallResult<LobbyMatchList_t> _lobbyMatchList;
        private int _currentPhase;
        private bool _isSearching;
        private CancellationTokenSource _cts;

        /// 최적의 방을 찾았을 때 (lobbyId)
        public event Action<CSteamID> OnMatchFound;
        /// 모든 Phase에서 방을 못 찾았을 때
        public event Action OnMatchFailed;

        public bool IsSearching => _isSearching;

        protected override void Awake()
        {
            base.Awake();
            if (Instance != this) return;

            if (GameManager.SteamActive)
                _lobbyMatchList = CallResult<LobbyMatchList_t>.Create(OnLobbyMatchList);
        }

        private void OnDestroy()
        {
            Cancel();

            if (GameManager.SteamActive)
                _lobbyMatchList?.Dispose();
        }

        // ── 공개 API ──────────────────────────────────────────

        public void StartSearch()
        {
            if (!GameManager.SteamActive) return;

            Cancel();

            _isSearching = true;
            _currentPhase = 0;
            _cts = new CancellationTokenSource();

            RequestCurrentPhase();
        }

        public void Cancel()
        {
            _isSearching = false;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        // ── Phase 관리 ───────────────────────────────────────
        /// <summary>
        /// 현재 Phase의 조건으로 Steam에 로비 검색을 요청
        /// 공통 필터(게임ID, 공개방, 대기중, 자리있음)는 매 Phase 동일하고,
        /// 거리 필터와 언어 필터만 Phase에 따라 달라짐
        /// </summary>
        private void RequestCurrentPhase()
        {
            if (!_isSearching) return;

            // ── 공통 필터: 모든 Phase에서 항상 적용 ──
            // 우리 게임인지
            SteamMatchmaking.AddRequestLobbyListStringFilter(
                LobbyManager.GameKey, LobbyManager.GameName,
                ELobbyComparison.k_ELobbyComparisonEqual);
            // 공개 방인지
            SteamMatchmaking.AddRequestLobbyListStringFilter(
                LobbyManager.IsPublicKey, "True",
                ELobbyComparison.k_ELobbyComparisonEqual);
            // 대기 중(로비)인 방인지
            SteamMatchmaking.AddRequestLobbyListStringFilter(
                LobbyManager.RoomStatusKey, LobbyManager.StatusLobby,
                ELobbyComparison.k_ELobbyComparisonEqual);
            // 빈 자리가 1개 이상 있는 방
            SteamMatchmaking.AddRequestLobbyListFilterSlotsAvailable(1);

            // Phase별 조건
            switch (_currentPhase)
            {
                case 0: // 가까운 거리 + 같은 언어
                    SteamMatchmaking.AddRequestLobbyListDistanceFilter(
                        ELobbyDistanceFilter.k_ELobbyDistanceFilterClose);
                    SteamMatchmaking.AddRequestLobbyListStringFilter(
                        LobbyManager.LanguageKey, LocalizationManager.CurrentLanguageCode,
                        ELobbyComparison.k_ELobbyComparisonEqual);
                    break;

                case 1: // 보통 거리 + 같은 언어
                    SteamMatchmaking.AddRequestLobbyListDistanceFilter(
                        ELobbyDistanceFilter.k_ELobbyDistanceFilterDefault);
                    SteamMatchmaking.AddRequestLobbyListStringFilter(
                        LobbyManager.LanguageKey, LocalizationManager.CurrentLanguageCode,
                        ELobbyComparison.k_ELobbyComparisonEqual);
                    break;

                case 2: // 먼 거리 + 언어 무관
                    SteamMatchmaking.AddRequestLobbyListDistanceFilter(
                        ELobbyDistanceFilter.k_ELobbyDistanceFilterFar);
                    break;

                case 3: // 전 세계 + 언어 무관
                    SteamMatchmaking.AddRequestLobbyListDistanceFilter(
                        ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
                    break;
            }

            Debug.Log($"[QuickMatch] Phase {_currentPhase} 검색 시작");

            SteamAPICall_t handle = SteamMatchmaking.RequestLobbyList();
            _lobbyMatchList.Set(handle);
        }

        // ── Steam 콜백 ──────────────────────────────────────

        private void OnLobbyMatchList(LobbyMatchList_t result, bool ioFailure)
        {
            if (!_isSearching) return;

            if (!ioFailure && result.m_nLobbiesMatching > 0)
            {
                CSteamID best = PickBestLobby(result.m_nLobbiesMatching);
                if (best.IsValid())
                {
                    Debug.Log($"[QuickMatch] Phase {_currentPhase}에서 방 발견! " +
                              $"인원: {SteamMatchmaking.GetNumLobbyMembers(best)}" +
                              $"/{SteamMatchmaking.GetLobbyMemberLimit(best)}");

                    _isSearching = false;
                    OnMatchFound?.Invoke(best);
                    return;
                }
            }

            // 이 Phase에서 못 찾음 → 다음 Phase로
            _currentPhase++;

            if (_currentPhase >= PhaseDelays.Length)
            {
                Debug.LogWarning("[QuickMatch] 모든 단계에서 방을 찾지 못했습니다.");
                _isSearching = false;
                OnMatchFailed?.Invoke();
                return;
            }

            // 딜레이 후 다음 Phase 검색
            AdvanceToNextPhase().Forget();
        }

        private async UniTaskVoid AdvanceToNextPhase()
        {
            float delay = PhaseDelays[_currentPhase];
            if (delay > 0f)
            {
                Debug.Log($"[QuickMatch] Phase {_currentPhase} 대기 중... ({delay}초)");
                await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: _cts.Token);
            }

            if (!_isSearching) return;

            RequestCurrentPhase();
        }

        // ── 최적 방 선택 ────────────────────────────────────

        private static CSteamID PickBestLobby(uint count)
        {
            CSteamID bestLobby = CSteamID.Nil;
            int bestMemberCount = -1;

            for (int i = 0; i < (int)count; i++)
            {
                CSteamID lobbyId = SteamMatchmaking.GetLobbyByIndex(i);

                int memberCount = SteamMatchmaking.GetNumLobbyMembers(lobbyId);
                int memberLimit = SteamMatchmaking.GetLobbyMemberLimit(lobbyId);

                if (memberCount >= memberLimit) continue;

                string mapId = SteamMatchmaking.GetLobbyData(lobbyId, LobbyManager.MapIdKey);
                if (WorkshopMapId.TryGetFileId(mapId, out ulong fileId)
                    && !WorkshopSubscriptionService.IsInstalled(fileId))
                {
                    Debug.Log($"[QuickMatch] 미설치 창작마당 맵 방 건너뜀: {mapId}");
                    continue;
                }

                if (memberCount > bestMemberCount)
                {
                    bestMemberCount = memberCount;
                    bestLobby = lobbyId;
                }
            }

            return bestLobby;
        }

    }
}