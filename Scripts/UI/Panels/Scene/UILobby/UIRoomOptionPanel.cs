using OneText.UGUI;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    public class UIRoomOptionPanel : MonoBehaviour
    {
        [Header("Registry")]
        [SerializeField] private GameModeRegistrySO gameModeRegistry;
        [SerializeField] private MapRegistrySO mapRegistry;

        [Header("Game Mode")]
        [SerializeField] private OneTextLabel gameModeText;
        [SerializeField] private OneTextLabel gameModeDescriptionText;

        [Header("Map")]
        [SerializeField] private OneTextLabel mapNameText;
        [SerializeField] private Image mapPreviewImage;

        [Header("Player Count")]
        [SerializeField] private OneTextLabel maxPlayersText;
        [SerializeField] private OneTextLabel seekerCountText;

        [Header("Time Settings")]
        [SerializeField] private OneTextLabel hideTimeText;
        [SerializeField] private OneTextLabel seekTimeText;
        [SerializeField] private OneTextLabel checkTimeText;
        [SerializeField] private OneTextLabel forcedExposeIntervalText;
        [SerializeField] private LocalizedString offLabel;

        private RoomOption _currentOption;

        public void Subscribe()
        {
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        }

        public void Unsubscribe()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        }

        public void Refresh(RoomOption option)
        {
            if (option == null) return;

            _currentOption = option;

            RefreshGameMode(option);
            RefreshMapName(option);
            RefreshPlayerCounts(option);
            RefreshTimeSettings(option);
        }

        // 설정값을 그대로 보여준다. 인원에 따른 실제 축소는 GameRoundManager.AssignRoles가 처리한다.
        private void RefreshPlayerCounts(RoomOption option)
        {
            if (maxPlayersText != null) maxPlayersText.text = option.maxPlayers.ToString();
            if (seekerCountText != null) seekerCountText.text = option.seekerCount.ToString();
        }

        private void RefreshGameMode(RoomOption option)
        {
            var data = gameModeRegistry != null ? gameModeRegistry.Find(option.gameMode) : null;

            if (gameModeText != null)
            {
                gameModeText.text = data != null && !data.displayName.IsEmpty
                    ? data.displayName.GetLocalizedString()
                    : option.gameMode.ToString();
            }

            if (gameModeDescriptionText != null)
            {
                gameModeDescriptionText.text = data != null && !data.description.IsEmpty
                    ? data.description.GetLocalizedString()
                    : string.Empty;
            }
        }

        private void RefreshMapName(RoomOption option)
        {
            // 창작마당 맵인 경우
            if (WorkshopMapId.IsWorkshop(option.mapName))
            {
                if (WorkshopMapId.TryGetFileId(option.mapName, out ulong fileId))
                {
                    string installPath = WorkshopSubscriptionService.GetInstallPath(fileId);

                    // ──  미설치 시 맵 이름·프리뷰 모두 비우기 ──
                    if (string.IsNullOrEmpty(installPath))
                    {
                        if (mapNameText != null)
                            mapNameText.text = string.Empty;

                        if (mapPreviewImage != null)
                            mapPreviewImage.enabled = false;

                        return;
                    }

                    // 설치되어 있으면 manifest에서 읽기
                    string displayName = $"Workshop #{fileId}";
                    Sprite preview = null;

                    try
                    {
                        var manifest = WorkshopMapPackageReader.ReadManifest(installPath);
                        if (!string.IsNullOrEmpty(manifest.title))
                            displayName = manifest.title;
                        preview = WorkshopMapPackageReader.LoadPreview(installPath, manifest.previewImage);
                    }
                    catch { }

                    if (mapNameText != null)
                        mapNameText.text = displayName;

                    if (mapPreviewImage != null)
                    {
                        mapPreviewImage.sprite = preview;
                        mapPreviewImage.enabled = preview != null;
                    }
                    return;
                }
            }

            // 내장 맵인 경우 (기존 로직)
            var mapData = mapRegistry != null ? mapRegistry.FindById(option.mapName) : null;

            if (mapNameText != null)
            {
                mapNameText.text = mapData != null
                    ? mapData.GetDisplayName()
                    : option.mapName;
            }

            if (mapPreviewImage != null)
            {
                mapPreviewImage.sprite = mapData != null ? mapData.previewSprite : null;
                mapPreviewImage.enabled = mapPreviewImage.sprite != null;
            }
        }

        private void RefreshTimeSettings(RoomOption option)
        {
            if (hideTimeText != null) hideTimeText.text = option.hideTime.ToString("0");
            if (seekTimeText != null) seekTimeText.text = option.seekTime.ToString("0");
            if (checkTimeText != null) checkTimeText.text = option.checkTime.ToString("0");
            if (forcedExposeIntervalText != null)
                forcedExposeIntervalText.text = option.forcedExposeEnabled
                    ? option.forcedExposeInterval.ToString("0")
                    : offLabel.GetLocalizedString();
        }

        private void OnLocaleChanged(Locale locale)
        {
            Refresh(_currentOption);
        }
    }
}
