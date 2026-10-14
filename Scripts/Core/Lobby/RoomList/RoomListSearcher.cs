using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;

namespace PixelChroma
{
    public class RoomListSearcher : MonoSingleton<RoomListSearcher>
    {
        private CallResult<LobbyMatchList_t> _lobbyMatchList;
        private RoomListFilter _currentFilter;
        private readonly List<RoomListEntry> _entries = new();

        /// 검색 결과가 갱신될 때마다 호출됩니다.
        /// Steam: 한 번에 전체 목록이 들어옵니다.
        /// LAN: 방이 발견될 때마다 누적된 목록으로 호출됩니다.
        public event Action<List<RoomListEntry>> OnSearchCompleted;

        public IReadOnlyList<RoomListEntry> CurrentEntries => _entries;

        protected override void Awake()
        {
            base.Awake();
            if (Instance != this) return;

            if (GameManager.SteamActive)
                _lobbyMatchList = CallResult<LobbyMatchList_t>.Create(OnSteamLobbyListReceived);
        }

        private void OnDestroy()
        {
            if (GameManager.SteamActive)
                _lobbyMatchList?.Dispose();

            UnsubscribeLan();
        }

        // ── 공개 API ──────────────────────────────────────────

        public void Search(RoomListFilter filter)
        {
            _entries.Clear();
            _currentFilter = filter;

            if (GameManager.SteamActive)
                SearchSteam(filter);
            else
                SearchLan();
        }

        public void StopSearch()
        {
            UnsubscribeLan();

            var discovery = ChromaNetworkDiscovery.Instance;
            if (discovery != null)
                discovery.StopDiscovery();
        }

        // ── Steam 경로 ───────────────────────────────────────

        private void SearchSteam(RoomListFilter filter)
        {
            SteamMatchmaking.AddRequestLobbyListStringFilter(
                LobbyManager.GameKey, LobbyManager.GameName, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter(
                LobbyManager.IsPublicKey, "True", ELobbyComparison.k_ELobbyComparisonEqual);
            // RoomStatusKey 필터 삭제 — 인게임 방도 목록에 표시하되 입장은 비활성화
            SteamMatchmaking.AddRequestLobbyListFilterSlotsAvailable(1);
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(
                ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);

            if (filter.gameMode.HasValue)
                SteamMatchmaking.AddRequestLobbyListStringFilter(
                    LobbyManager.GameModeKey,
                    ((int)filter.gameMode.Value).ToString(),
                    ELobbyComparison.k_ELobbyComparisonEqual);

            if (!string.IsNullOrEmpty(filter.mapId))
                SteamMatchmaking.AddRequestLobbyListStringFilter(
                    LobbyManager.MapIdKey, filter.mapId, ELobbyComparison.k_ELobbyComparisonEqual);

            if (!string.IsNullOrEmpty(filter.languageCode))
                SteamMatchmaking.AddRequestLobbyListStringFilter(
                    LobbyManager.LanguageKey, filter.languageCode, ELobbyComparison.k_ELobbyComparisonEqual);

            SteamAPICall_t handle = SteamMatchmaking.RequestLobbyList();
            _lobbyMatchList.Set(handle);
        }

        private void OnSteamLobbyListReceived(LobbyMatchList_t result, bool ioFailure)
        {
            if (!ioFailure)
            {
                for (int i = 0; i < (int)result.m_nLobbiesMatching; i++)
                {
                    CSteamID lobbyId = SteamMatchmaking.GetLobbyByIndex(i);

                    string roomName = SteamMatchmaking.GetLobbyData(lobbyId, LobbyManager.RoomNameKey);
                    string hostName = SteamMatchmaking.GetLobbyData(lobbyId, LobbyManager.HostNameKey);
                    int currentPlayers = SteamMatchmaking.GetNumLobbyMembers(lobbyId);
                    int maxPlayers = SteamMatchmaking.GetLobbyMemberLimit(lobbyId);

                    string gameModeStr = SteamMatchmaking.GetLobbyData(lobbyId, LobbyManager.GameModeKey);
                    GameMode gameMode = int.TryParse(gameModeStr, out int modeInt)
                        ? (GameMode)modeInt
                        : GameMode.Basic;

                    string mapId = SteamMatchmaking.GetLobbyData(lobbyId, LobbyManager.MapIdKey);
                    string lang = SteamMatchmaking.GetLobbyData(lobbyId, LobbyManager.LanguageKey);
                    string status = SteamMatchmaking.GetLobbyData(lobbyId, LobbyManager.RoomStatusKey);

                    var entry = RoomListEntry.FromSteamLobby(
                        lobbyId, roomName, hostName,
                        currentPlayers, maxPlayers,
                        gameMode, mapId, lang, status);

                    if (!PassesFilter(entry, _currentFilter)) continue;

                    _entries.Add(entry);
                }

                _entries.Sort((a, b) =>
                {
                    // 1순위: 대기중(Lobby)인 방이 위로
                    bool aIsLobby = a.roomStatus == LobbyManager.StatusLobby;
                    bool bIsLobby = b.roomStatus == LobbyManager.StatusLobby;
                    if (aIsLobby != bIsLobby)
                        return aIsLobby ? -1 : 1;

                    // 2순위: 빈자리 있으면서 인원 많은 순
                    bool aHasSlot = a.currentPlayers < a.maxPlayers;
                    bool bHasSlot = b.currentPlayers < b.maxPlayers;
                    if (aHasSlot != bHasSlot)
                        return aHasSlot ? -1 : 1;

                    // 둘 다 빈자리 있으면 인원 많은 순
                    return b.currentPlayers.CompareTo(a.currentPlayers);
                });
            }

            OnSearchCompleted?.Invoke(_entries);
        }

        // ── LAN 경로 ────────────────────────────────────────

        private void SearchLan()
        {
            var discovery = ChromaNetworkDiscovery.Instance;
            if (discovery == null)
            {
                OnSearchCompleted?.Invoke(_entries);
                return;
            }

            discovery.ClearResults();
            UnsubscribeLan();
            discovery.OnRoomFound += HandleLanRoomFound;
            discovery.StartDiscovery();
        }

        private void HandleLanRoomFound(RoomListEntry entry)
        {
            if (!PassesFilter(entry, _currentFilter)) return;
             
            _entries.RemoveAll(e => e.connectAddress == entry.connectAddress);
            _entries.Add(entry);
            OnSearchCompleted?.Invoke(_entries);
        }

        private void UnsubscribeLan()
        {
            var discovery = ChromaNetworkDiscovery.Instance;
            if (discovery != null)
                discovery.OnRoomFound -= HandleLanRoomFound;
        }

        // ── 필터 ─────────────────────────────────────────────

        private static bool PassesFilter(RoomListEntry entry, RoomListFilter filter)
        {
            if (filter.gameMode.HasValue && entry.gameMode != filter.gameMode.Value)
                return false;
            if (!string.IsNullOrEmpty(filter.mapId) && entry.mapId != filter.mapId)
                return false;
            if (!string.IsNullOrEmpty(filter.languageCode) && entry.languageCode != filter.languageCode)
                return false;
            if (!string.IsNullOrEmpty(filter.roomNameQuery)
                && (string.IsNullOrEmpty(entry.roomName)
                    || entry.roomName.IndexOf(filter.roomNameQuery, System.StringComparison.OrdinalIgnoreCase) < 0))
                return false;
            return true;
        }
    }
}