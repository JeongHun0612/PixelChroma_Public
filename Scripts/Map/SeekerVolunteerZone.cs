using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace PixelChroma
{
    public class SeekerVolunteerZone : MonoBehaviour
    {
        private readonly HashSet<int> _volunteerConnectionIds = new();

        public IReadOnlyCollection<int> VolunteerConnectionIds => _volunteerConnectionIds;

        private void OnTriggerStay2D(Collider2D other)
        {
            if (!NetworkServer.active) return;

            var lobbyPlayer = other.GetComponentInParent<LobbyPlayer>();
            if (lobbyPlayer == null || lobbyPlayer.connectionToClient == null) return;

            _volunteerConnectionIds.Add(lobbyPlayer.connectionToClient.connectionId);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!NetworkServer.active) return;

            var lobbyPlayer = other.GetComponentInParent<LobbyPlayer>();
            if (lobbyPlayer == null || lobbyPlayer.connectionToClient == null) return;

            _volunteerConnectionIds.Remove(lobbyPlayer.connectionToClient.connectionId);
        }
    }
}