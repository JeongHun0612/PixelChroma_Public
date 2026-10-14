using OneText.UGUI;
using UnityEngine;
using UnityEngine.UI;
using Mirror;

namespace PixelChroma.UI
{
    public class UIMemberSlot : MonoBehaviour
    {
        [SerializeField] private OneTextLabel nameText;
        [SerializeField] private RawImage nicknameImage;  
        [SerializeField] private GameObject readyIcon;
        [SerializeField] private GameObject hostIcon;
        [SerializeField] private Button kickButton;

        private LobbyPlayerState _player;
        private Texture2D _cachedTexture;                  

        private void Awake()
        {
            if (kickButton != null)
                kickButton.AddClickListener(OnClickKick);
        }

        private void OnDestroy()                        
        {
            if (_cachedTexture != null)
                Destroy(_cachedTexture);
        }

        public void Set(LobbyPlayerState player, bool isHost, bool isLocalHost)
        {
            _player = player;

            var playerBase = player.GetComponent<PlayerBase>();
            string playerName = playerBase != null ? playerBase.playerName : "Player";

            byte[] pngData = PixelNicknameHelper.GetPixelData(playerBase);
            bool showPixel = PixelNicknameHelper.ShouldShowPixel();

            if (showPixel && nicknameImage != null)
            {
                _cachedTexture = PixelNicknameHelper.ApplyPng(nicknameImage, pngData, _cachedTexture);
                nicknameImage.gameObject.SetActive(true);
                if (nameText != null) nameText.gameObject.SetActive(false);
            }
            else
            {
                if (nicknameImage != null) nicknameImage.gameObject.SetActive(false);
                if (nameText != null)
                {
                    nameText.gameObject.SetActive(true);
                    nameText.text = playerName;
                }
            }

            if (readyIcon != null)
                readyIcon.SetActive(player.isReady);

            if (hostIcon != null)
                hostIcon.SetActive(isHost);

            if (kickButton != null)
                kickButton.gameObject.SetActive(isLocalHost && !isHost);
        }

        private void OnClickKick()
        {
            if (_player == null) return;

            var localPlayer = LobbyPlayerState.AllPlayers[0];
            if (localPlayer != null && localPlayer.isLocalPlayer)
                localPlayer.CmdKickPlayer(_player.netId);
        }
    }
}