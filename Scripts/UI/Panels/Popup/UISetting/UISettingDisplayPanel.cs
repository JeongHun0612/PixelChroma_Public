using System.Collections.Generic;
using OneText.UGUI;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    public class UISettingDisplayPanel : MonoBehaviour
    {
        [Header("Resolution")]
        [SerializeField] private GameObject resolutionRow;
        [SerializeField] private OneTextDropdown resolutionDropdown;

        [Header("Toggles")]
        [SerializeField] private Toggle fullscreenToggle;   // On = 테두리 없는 전체화면, Off = 창모드
        [SerializeField] private Toggle vSyncToggle;
        [SerializeField] private Toggle screenShakeToggle;
        [SerializeField] private Toggle pixelNicknameToggle;

        private readonly List<Vector2Int> _resolutions = new();

        public void Initialize()
        {
            BuildResolutionOptions();

            if (fullscreenToggle != null)
                fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
            if (resolutionDropdown != null)
                resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
            if (vSyncToggle != null)
                vSyncToggle.onValueChanged.AddListener(GameSettings.SetVSync);
            if (screenShakeToggle != null)
                screenShakeToggle.onValueChanged.AddListener(GameSettings.SetScreenShake);
            if (pixelNicknameToggle != null)
                pixelNicknameToggle.onValueChanged.AddListener(GameSettings.SetShowPixelNickname);
        }

        public void Refresh()
        {
            if (fullscreenToggle != null)
                fullscreenToggle.SetIsOnWithoutNotify(GameSettings.Fullscreen);

            if (resolutionDropdown != null)
                resolutionDropdown.SetValueWithoutNotify(CurrentResolutionIndex());

            SetResolutionRowActive(!GameSettings.Fullscreen);

            if (vSyncToggle != null)
                vSyncToggle.SetIsOnWithoutNotify(GameSettings.VSyncEnabled);
            if (screenShakeToggle != null)
                screenShakeToggle.SetIsOnWithoutNotify(GameSettings.ScreenShakeEnabled);
            if (pixelNicknameToggle != null)
                pixelNicknameToggle.SetIsOnWithoutNotify(GameSettings.ShowPixelNickname);
        }

        private void OnFullscreenChanged(bool isFullscreen)
        {
            GameSettings.SetFullscreen(isFullscreen);
            SetResolutionRowActive(!isFullscreen);
        }

        private void SetResolutionRowActive(bool active)
        {
            if (resolutionRow != null)
                resolutionRow.SetActive(active);
            else if (resolutionDropdown != null)
                resolutionDropdown.interactable = active;
        }

        // 모니터가 지원하는 해상도를 (너비×높이) 기준으로 중복 제거해 나열한다(주사율 차이는 합침).
        private void BuildResolutionOptions()
        {
            if (resolutionDropdown == null) return;

            _resolutions.Clear();
            var seen = new HashSet<long>();

            var resArr = Screen.resolutions;
            for (int i = resArr.Length - 1; i >= 0; i--)
            {
                long key = ((long)resArr[i].width << 32) | (uint)resArr[i].height;
                if (seen.Add(key))
                    _resolutions.Add(new Vector2Int(resArr[i].width, resArr[i].height));
            }

            // 저장된 해상도가 목록에 없으면(모니터 변경 등) 추가해 항상 선택 가능하게 한다.
            var current = new Vector2Int(GameSettings.ResolutionWidth, GameSettings.ResolutionHeight);
            if (current.x > 0 && current.y > 0 && !_resolutions.Contains(current))
                _resolutions.Add(current);

            var labels = new List<string>(_resolutions.Count);
            foreach (var res in _resolutions)
                labels.Add($"{res.x} x {res.y}");

            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(labels);
        }

        private int CurrentResolutionIndex()
        {
            var current = new Vector2Int(GameSettings.ResolutionWidth, GameSettings.ResolutionHeight);
            int index = _resolutions.IndexOf(current);
            return index >= 0 ? index : 0;
        }

        private void OnResolutionChanged(int index)
        {
            if (index < 0 || index >= _resolutions.Count) return;

            var res = _resolutions[index];
            GameSettings.SetResolution(res.x, res.y);
        }
    }
}
