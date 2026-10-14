using System;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

namespace PixelChroma
{
    public class LobbyPlayerState : NetworkBehaviour
    {
        private static readonly List<LobbyPlayerState> _allPlayers = new();
        public static event Action OnLobbyPlayersChanged;
        public static event Action OnReadyChanged;
        public static event Action<RoomOption> OnRoomOptionSynced;

        public static IReadOnlyList<LobbyPlayerState> AllPlayers
        {
            get
            {
                _allPlayers.RemoveAll(p => p == null || !NetworkClient.spawned.ContainsKey(p.netId));
                return _allPlayers;
            }
        }


        [SyncVar(hook = nameof(OnReadyStateChanged))]
        public bool isReady = false;

        public override void OnStartServer()
        {
            if (connectionToClient == NetworkServer.localConnection)
                isReady = true;
        }

        public override void OnStartClient()
        {
            if (!_allPlayers.Contains(this))
                _allPlayers.Add(this);

            OnLobbyPlayersChanged?.Invoke();
        }

        public override void OnStopClient()
        {
            _allPlayers.Remove(this);
            OnLobbyPlayersChanged?.Invoke();
        }

        public override void OnStartLocalPlayer()
        {
            // 클라이언트에 이 오브젝트가 완전히 스폰된 뒤에 서버로 방 옵션을 요청합니다.
            // (서버가 OnStartServer에서 바로 TargetRpc로 보내면, 그 시점엔 클라에 아직 이 오브젝트가 없어 RPC가 유실됩니다.)
            CmdRequestRoomOption();
        }

        [Command]
        private void CmdRequestRoomOption()
        {
            SyncRoomOptionToClient(connectionToClient);
        }


        [Command]
        public void CmdKickPlayer(uint targetNetId)
        {
            if (connectionToClient != NetworkServer.localConnection)
                return;

            if (!NetworkServer.spawned.TryGetValue(targetNetId, out var targetIdentity))
                return;

            targetIdentity.connectionToClient?.Disconnect();
        }

        [Command]
        public void CmdToggleReady()
        {
            if (connectionToClient == NetworkServer.localConnection)
                return;

            isReady = !isReady;
        }

        private void OnReadyStateChanged(bool oldReady, bool newReady)
        {
            OnReadyChanged?.Invoke();
        }

        // ── RoomOption 동기화 ────────────────────────────────

        [Server]
        public void SyncRoomOptionToClient(NetworkConnectionToClient conn)
        {
            if (LobbyManager.Instance?.CurrentRoomOption == null)
                return;

            TargetReceiveRoomOption(conn, LobbyManager.Instance.CurrentRoomOption.ToData());
        }

        [Server]
        public static void BroadcastRoomOption()
        {
            if (LobbyManager.Instance?.CurrentRoomOption == null)
                return;

            LobbyPlayerState sender = null;
            foreach (var p in AllPlayers)
            {
                if (p.connectionToClient != null)
                {
                    sender = p;
                    break;
                }
            }
            if (sender == null)
                return;

            sender.RpcReceiveRoomOption(LobbyManager.Instance.CurrentRoomOption.ToData());
        }

        [TargetRpc]
        private void TargetReceiveRoomOption(NetworkConnectionToClient conn, RoomOptionData data)
        {
            ApplyRoomOption(data);
        }

        [ClientRpc]
        private void RpcReceiveRoomOption(RoomOptionData data)
        {
            ApplyRoomOption(data);
        }

        private void ApplyRoomOption(RoomOptionData data)
        {
            if (LobbyManager.Instance == null)
                return;

            var opt = RoomOption.FromData(data);
            LobbyManager.Instance.CurrentRoomOption = opt;
            OnRoomOptionSynced?.Invoke(opt);
        }
    }
}
