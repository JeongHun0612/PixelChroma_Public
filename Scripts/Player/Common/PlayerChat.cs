using Mirror;
using UnityEngine;
using System;

namespace PixelChroma.Network
{
    public class PlayerChat : NetworkBehaviour
    {
        // 변경: netId 추가 → UI에서 본인 여부 판별 가능
        public static event Action<uint, string, string> OnMessageReceived;

        public void SendChat(string message)
        {
            if (!isLocalPlayer) return;
            if (string.IsNullOrWhiteSpace(message)) return;

            string currentNickname = "Player";
            if (TryGetComponent<PlayerBase>(out var playerBase))
                currentNickname = playerBase.playerName;

            CmdSendMessage(currentNickname, message);
        }

        [Command]
        private void CmdSendMessage(string senderName, string message)
        {
            // netId는 서버가 알고 있으므로 서버에서 붙여서 브로드캐스트
            RpcReceiveMessage(netId, senderName, message);
        }

        [ClientRpc]
        private void RpcReceiveMessage(uint senderNetId, string senderName, string message)
        {
            OnMessageReceived?.Invoke(senderNetId, senderName, message);
        }
    }
}
