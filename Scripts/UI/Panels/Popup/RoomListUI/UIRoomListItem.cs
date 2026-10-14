using System;
using OneText.UGUI;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace PixelChroma.UI
{
    public class UIRoomListItem : MonoBehaviour
    {
        [SerializeField] private OneTextLabel roomNameText;
        [SerializeField] private OneTextLabel statusText;
        [SerializeField] private OneTextLabel mapText;
        [SerializeField] private OneTextLabel gameModeText;
        [SerializeField] private OneTextLabel playerCountText;
        [SerializeField] private Button joinButton;

        [Header("Localization")]
        [SerializeField] private RoomListTextDataSO textData;

        [Header("Registry")]
        [SerializeField] private GameModeRegistrySO gameModeRegistry;

        private RoomListEntry _entry;

        public event Action<RoomListEntry> OnJoinClicked;

        private void Awake()
        {
            if (joinButton != null)
                joinButton.AddClickListener(() => OnJoinClicked?.Invoke(_entry));
        }

        public void Setup(RoomListEntry entry)
        {
            _entry = entry;

            if (roomNameText != null)
            {
                roomNameText.text = string.IsNullOrEmpty(entry.roomName)
                    ? string.Format(textData.defaultRoomNameFormat.GetLocalizedString(), entry.hostName)
                    : entry.roomName;
            }
            if (playerCountText != null) playerCountText.text = $"{entry.currentPlayers}/{entry.maxPlayers}";
            if (gameModeText != null)
            {
                var data = gameModeRegistry != null ? gameModeRegistry.Find(entry.gameMode) : null;
                gameModeText.text = data != null
                    ? data.displayName.GetLocalizedString()
                    : entry.gameMode.ToString();
            }

            if (mapText != null)
            {
                string mapDisplay = "-";
                if (!string.IsNullOrEmpty(entry.mapId))
                {
                    var registry = LobbyManager.Instance != null ? LobbyManager.Instance.MapRegistry : null;
                    var mapData = registry != null ? registry.FindById(entry.mapId) : null;
                    mapDisplay = mapData != null
                        ? mapData.GetDisplayName()
                        : entry.mapId;
                }
                mapText.text = mapDisplay;
            }

            if (statusText != null)
            {
                statusText.text = entry.IsJoinable
                    ? textData.lobbyStatusLabel.GetLocalizedString()
                    : textData.inGameStatusLabel.GetLocalizedString();
            }
            if (joinButton != null) joinButton.interactable = entry.IsJoinable;
        }

        public void SetInteractable(bool interactable)
        {
            if (joinButton != null) joinButton.interactable = interactable && _entry.IsJoinable;
        }
    }
}