using Steamworks;

namespace PixelChroma
{
    public readonly struct RoomListEntry
    {
        public readonly string roomName;
        public readonly string hostName;
        public readonly int currentPlayers;
        public readonly int maxPlayers;
        public readonly GameMode gameMode;
        public readonly string mapId;
        public readonly string languageCode;
        public readonly string roomStatus;

        public readonly CSteamID steamLobbyId;
        public readonly string connectAddress;

        public bool IsSteamRoom => steamLobbyId.IsValid();
        public bool IsJoinable => string.IsNullOrEmpty(roomStatus) || roomStatus == "Lobby";

        public bool IsWorkshopMap => WorkshopMapId.IsWorkshop(mapId);

        public ulong WorkshopFileId
            => WorkshopMapId.TryGetFileId(mapId, out ulong id) ? id : 0;

        private RoomListEntry(
            string roomName, string hostName, int currentPlayers, int maxPlayers, GameMode gameMode,
            string mapId, string languageCode, string roomStatus, CSteamID steamLobbyId, string connectAddress)
        {
            this.roomName = roomName;
            this.hostName = hostName;
            this.currentPlayers = currentPlayers;
            this.maxPlayers = maxPlayers;
            this.gameMode = gameMode;
            this.mapId = mapId;
            this.languageCode = languageCode;
            this.roomStatus = roomStatus;
            this.steamLobbyId = steamLobbyId;
            this.connectAddress = connectAddress;
        }

        public static RoomListEntry FromSteamLobby(
            CSteamID lobbyId, string roomName, string hostName, int currentPlayers, int maxPlayers,
            GameMode gameMode, string mapId, string languageCode, string roomStatus)
        {
            string finalName = string.IsNullOrEmpty(roomName) ? $"{hostName}의 방" : roomName;
            return new RoomListEntry(finalName, hostName, currentPlayers, maxPlayers, gameMode, mapId, languageCode, roomStatus, lobbyId, null);
        }

        public static RoomListEntry FromLanDiscovery(
            string connectAddress, string roomName, string hostName, int currentPlayers, int maxPlayers,
            GameMode gameMode, string mapId, string languageCode, string roomStatus)
        {
            string finalName = string.IsNullOrEmpty(roomName) ? $"{hostName}의 방" : roomName;
            return new RoomListEntry(finalName, hostName, currentPlayers, maxPlayers, gameMode, mapId, languageCode, roomStatus, CSteamID.Nil, connectAddress);
        }
    }

    public readonly struct RoomListFilter
    {
        public readonly GameMode? gameMode;
        public readonly string mapId;
        public readonly string languageCode;
        public readonly string roomNameQuery;

        public RoomListFilter(GameMode? gameMode, string mapId, string languageCode, string roomNameQuery = null)
        {
            this.gameMode = gameMode;
            this.mapId = mapId;
            this.languageCode = languageCode;
            this.roomNameQuery = roomNameQuery;
        }

        public static readonly RoomListFilter None = new(null, null, null, null);
    }
}