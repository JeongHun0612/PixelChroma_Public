using System;
using System.Collections.Generic;
using System.Net;
using Mirror;
using Mirror.Discovery;

namespace PixelChroma
{
    public struct ChromaDiscoveryRequest : NetworkMessage
    {
    }

    public struct ChromaDiscoveryResponse : NetworkMessage
    {
        public Uri uri;
        public long serverId;
        public string roomName;
        public string hostName;
        public int currentPlayers;
        public int maxPlayers;
        public GameMode gameMode;
        public string mapId;
        public string languageCode;
        public string roomStatus;
    }

    public class ChromaNetworkDiscovery : NetworkDiscoveryBase<ChromaDiscoveryRequest, ChromaDiscoveryResponse>
    {
        public static ChromaNetworkDiscovery Instance { get; private set; }

        private readonly Dictionary<long, RoomListEntry> _found = new();

        public event Action<RoomListEntry> OnRoomFound;

        public override void Start()
        {
            Instance = this;
            base.Start();
        }

        public void ClearResults() => _found.Clear();

        protected override ChromaDiscoveryRequest GetRequest() => new ChromaDiscoveryRequest();

        protected override void ProcessClientRequest(ChromaDiscoveryRequest request, IPEndPoint endpoint)
        {
            var option = LobbyManager.Instance != null ? LobbyManager.Instance.CurrentRoomOption : null;
            if (option == null || !option.isPublic) return;

            base.ProcessClientRequest(request, endpoint);
        }

        protected override ChromaDiscoveryResponse ProcessRequest(ChromaDiscoveryRequest request, IPEndPoint endpoint)
        {
            var option = LobbyManager.Instance.CurrentRoomOption;

            return new ChromaDiscoveryResponse
            {
                uri = transport.ServerUri(),
                serverId = ServerId,
                roomName = option.roomName ?? "",
                hostName = string.IsNullOrEmpty(option.hostName) ? "Player" : option.hostName,
                currentPlayers = NetworkServer.connections.Count,
                maxPlayers = option.maxPlayers,
                gameMode = option.gameMode,
                mapId = option.mapName,
                languageCode = LocalizationManager.CurrentLanguageCode,
                roomStatus = GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.InGame
                    ? LobbyManager.StatusInGame
                    : LobbyManager.StatusLobby,
            };
        }

        protected override void ProcessResponse(ChromaDiscoveryResponse response, IPEndPoint endpoint)
        {
            var realUri = new UriBuilder(response.uri) { Host = endpoint.Address.ToString() };

            var entry = RoomListEntry.FromLanDiscovery(
                realUri.Uri.Host,
                response.roomName,
                response.hostName,
                response.currentPlayers,
                response.maxPlayers,
                response.gameMode,
                response.mapId,
                response.languageCode,
                response.roomStatus);

            _found[response.serverId] = entry;
            OnRoomFound?.Invoke(entry);
        }
    }
}