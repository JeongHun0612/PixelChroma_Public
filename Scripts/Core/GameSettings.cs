using System;
using UnityEngine;

namespace PixelChroma
{
    // 사운드를 제외한 게임 옵션(화면/그래픽)의 저장·적용을 담당하는 정적 홀더.
    // 볼륨은 SoundManager가 소유하므로 여기서 다루지 않는다.
    //
    // 흐름:
    //   부팅 시퀀스에서 Load()로 PlayerPrefs 값을 읽어 화면에 즉시 적용한다.
    //   옵션 UI(UISettingDisplayPanel)가 Setter로 값을 바꾸면 즉시 적용·저장하고 Changed를 발생시킨다.
    //   게임 로직(예: SeekerWaterGun의 카메라 쉐이크)은 캐시된 프로퍼티를 매 프레임 읽어도 부담이 없다.
    public static class GameSettings
    {
        private const string PrefPrefix = "Settings.";
        private const string KeyFullscreen = PrefPrefix + "Fullscreen";
        private const string KeyResWidth = PrefPrefix + "ResWidth";
        private const string KeyResHeight = PrefPrefix + "ResHeight";
        private const string KeyVSync = PrefPrefix + "VSync";
        private const string KeyScreenShake = PrefPrefix + "ScreenShake";

        private const string KeyShowPixelNickname = PrefPrefix + "ShowPixelNickname";

        // 게임 로직이 매 발사마다 조회하므로 PlayerPrefs 접근 없이 캐시된 값으로 노출한다.
        public static bool ScreenShakeEnabled { get; private set; } = true;
        public static bool VSyncEnabled { get; private set; } = true;
        // true = 테두리 없는 전체화면(FullScreenWindow), false = 창모드(Windowed).
        public static bool Fullscreen { get; private set; } = true;
        public static int ResolutionWidth { get; private set; }
        public static int ResolutionHeight { get; private set; }
        public static bool ShowPixelNickname { get; private set; } = true;// true = 그림 닉네임 표시, false = 텍스트 닉네임으로 표시


        // 옵션 값이 바뀔 때마다 발생. 열려 있는 UI가 표시를 갱신하는 데 사용한다.
        public static event Action Changed;

        private static bool _loaded;

        // 부팅 시퀀스에서 1회 호출. 저장값을 읽어 화면에 즉시 적용한다.
        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;

            ScreenShakeEnabled = PlayerPrefs.GetInt(KeyScreenShake, 1) == 1;
            ShowPixelNickname = PlayerPrefs.GetInt(KeyShowPixelNickname, 1) == 1;
            VSyncEnabled = PlayerPrefs.GetInt(KeyVSync, 1) == 1;

            Fullscreen = PlayerPrefs.GetInt(KeyFullscreen, 1) == 1;

            // 저장된 해상도가 없으면(최초 실행) 데스크톱 해상도를 기본값으로 사용한다.
            var desktop = Screen.currentResolution;
            ResolutionWidth = PlayerPrefs.GetInt(KeyResWidth, desktop.width);
            ResolutionHeight = PlayerPrefs.GetInt(KeyResHeight, desktop.height);

            ApplyDisplay();
            ApplyVSync();
        }

        // ── Setters (옵션 UI가 호출) ──────────────────────────────
        public static void SetFullscreen(bool enabled)
        {
            Fullscreen = enabled;
            PlayerPrefs.SetInt(KeyFullscreen, enabled ? 1 : 0);
            ApplyDisplay();
            Changed?.Invoke();
        }

        public static void SetResolution(int width, int height)
        {
            ResolutionWidth = width;
            ResolutionHeight = height;
            PlayerPrefs.SetInt(KeyResWidth, width);
            PlayerPrefs.SetInt(KeyResHeight, height);
            ApplyDisplay();
            Changed?.Invoke();
        }

        public static void SetVSync(bool enabled)
        {
            VSyncEnabled = enabled;
            PlayerPrefs.SetInt(KeyVSync, enabled ? 1 : 0);
            ApplyVSync();
            Changed?.Invoke();
        }

        public static void SetScreenShake(bool enabled)
        {
            ScreenShakeEnabled = enabled;
            PlayerPrefs.SetInt(KeyScreenShake, enabled ? 1 : 0);
            Changed?.Invoke();
        }

        public static void SetShowPixelNickname(bool enabled)
        {
            ShowPixelNickname = enabled;
            PlayerPrefs.SetInt(KeyShowPixelNickname, enabled ? 1 : 0);
            Changed?.Invoke();
        }

        // ── Apply ─────────────────────────────────────────────────
        private static void ApplyDisplay()
        {
            if (Fullscreen)
            {
                int nativeW = Display.main.systemWidth;
                int nativeH = Display.main.systemHeight;
                Screen.SetResolution(nativeW, nativeH, FullScreenMode.FullScreenWindow);
                return;
            }

            if (ResolutionWidth <= 0 || ResolutionHeight <= 0) return;
            Screen.SetResolution(ResolutionWidth, ResolutionHeight, FullScreenMode.Windowed);
        }

        private static void ApplyVSync()
        {
            QualitySettings.vSyncCount = VSyncEnabled ? 1 : 0;
        }
    }
}
