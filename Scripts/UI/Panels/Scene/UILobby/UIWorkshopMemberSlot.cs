using OneText.UGUI;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    public class UIWorkshopMemberSlot : MonoBehaviour
    {
        [Header("Player Name")]
        [SerializeField] private OneTextLabel nameText;
        [SerializeField] private RawImage nicknameImage;

        [Header("Status")]
        [SerializeField] private OneTextLabel statusText;
        [SerializeField] private Color installedColor = Color.green;
        [SerializeField] private Color notInstalledColor = Color.red;

        [Header("Kick")]
        [SerializeField] private Button kickButton;

        private uint _netId;
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

        /// <summary>
        /// 슬롯 데이터를 설정한다.
        /// </summary>
        /// <param name="status">서버에서 받은 개별 플레이어 상태</param>
        /// <param name="isLocalHost">로컬 플레이어가 호스트인지</param>
        /// <param name="installedLabel">로컬라이즈된 "완료" 텍스트</param>
        /// <param name="notInstalledLabel">로컬라이즈된 "미설치" 텍스트</param>
        public void Set(
            WorkshopPlayerStatus status,
            bool isLocalHost,
            string installedLabel,
            string notInstalledLabel)
        {
            _netId = status.netId;

            // ── 1. 이름 표시 (픽셀 닉네임 우선) ──
            PlayerBase playerBase = PixelNicknameHelper.FindPlayer(status.netId);
            byte[] pngData = PixelNicknameHelper.GetPixelData(playerBase);
            bool showPixel = PixelNicknameHelper.ShouldShowPixel();

            if (showPixel && nicknameImage != null)
            {
                _cachedTexture = PixelNicknameHelper.ApplyPng(
                    nicknameImage, pngData, _cachedTexture);
                nicknameImage.gameObject.SetActive(true);
                if (nameText != null) nameText.gameObject.SetActive(false);
            }
            else
            {
                if (nicknameImage != null) nicknameImage.gameObject.SetActive(false);
                if (nameText != null)
                {
                    nameText.gameObject.SetActive(true);
                    nameText.text = status.playerName;
                }
            }

            // ── 2. 설치 상태 텍스트 ──
            if (statusText != null)
            {
                statusText.text = status.isInstalled ? installedLabel : notInstalledLabel;
                statusText.color = status.isInstalled ? installedColor : notInstalledColor;
            }

            // ── 3. 강퇴 버튼 (호스트만, 자기 자신 제외) ──
            if (kickButton != null)
            {
                bool isTargetSelf = (playerBase != null && playerBase.isLocalPlayer);
                kickButton.gameObject.SetActive(isLocalHost && !isTargetSelf);
            }
        }

        private void OnClickKick()
        {
            if (LobbyPlayerState.AllPlayers.Count == 0) return;

            var localPlayer = LobbyPlayerState.AllPlayers[0];
            if (localPlayer != null && localPlayer.isLocalPlayer)
                localPlayer.CmdKickPlayer(_netId);
        }
    }
}