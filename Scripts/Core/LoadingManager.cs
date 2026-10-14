using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using PixelChroma.UI;
using UnityEngine;

namespace PixelChroma
{
    public static class LoadingManager
    {
        // 로딩창 최소 표시 시간(초). 실제 로드가 더 빨리 끝나도 이 시간만큼은 유지한다.
        private const float MinDisplayTime = 3f;

        // 로딩 진입 시 이전 씬 BGM이 사라지는 시간(초).
        private const float BgmFadeOutDuration = 0.4f;

        // 로딩이 어떤 이유로든(씬 로드 행, 콜백 예외 등) 끝나지 않을 때의 최종 안전망(초).
        public const float WatchdogTimeout = 30f;

        private static float _shownTime;
        private static CancellationTokenSource _watchdogCts;

        // 워치독 발동 알림. 실제 타이틀 복귀는 구독자(GameManager)가 처리한다.
        public static event Action OnTimedOut;

        public static bool IsShowing => UIManager.Instance.CurrentPopup is UILoading;

        public static async UniTask Show()
        {
            if (IsShowing) return;

            // 로딩 팝업은 즉시 띄우고, 이전 씬 BGM은 전환 초반에 짧게 Fade-out한다.
            if (SoundManager.Instance != null)
                SoundManager.Instance.StopBgm(BgmFadeOutDuration).Forget();

            // 로딩창을 띄우기 전에 열려 있던 팝업을 모두 닫는다.
            await UIManager.Instance.HideAllPopups();

            _shownTime = Time.unscaledTime;
            await UIManager.Instance.ShowPopup<UILoading>();

            // 로딩이 뜬 순간부터 워치독 가동: 씬 로드/스폰 콜백이 아예 호출되지 않아도
            // 로딩이 영구히 멈추지 않도록, 상한 초과 시 OnTimedOut으로 알린다.
            StartWatchdog();
        }

        public static async UniTask Hide()
        {
            // 로딩이 정상 종료되므로 워치독을 해제한다.
            CancelWatchdog();

            if (IsShowing)
            {
                float remaining = MinDisplayTime - (Time.unscaledTime - _shownTime);
                if (remaining > 0f)
                    await UniTask.Delay((int)(remaining * 1000), ignoreTimeScale: true);
            }

            await UIManager.Instance.HideAllPopups();
        }

        // ── 워치독 ──────────────────────────────────────────────
        private static void StartWatchdog()
        {
            CancelWatchdog();
            _watchdogCts = new CancellationTokenSource();
            RunWatchdog(_watchdogCts.Token).Forget();
        }

        private static async UniTaskVoid RunWatchdog(CancellationToken token)
        {
            try
            {
                await UniTask.Delay(
                    (int)(WatchdogTimeout * 1000),
                    ignoreTimeScale: true,
                    cancellationToken: token);
            }
            catch (OperationCanceledException)
            {
                return; // 로딩이 정상적으로 끝나 워치독이 취소됨
            }

            // 여기 도달 = 상한 초과. 로딩이 여전히 떠 있으면 복구 불가로 보고 알림.
            if (IsShowing)
                OnTimedOut?.Invoke();
        }

        private static void CancelWatchdog()
        {
            if (_watchdogCts == null) return;

            _watchdogCts.Cancel();
            _watchdogCts.Dispose();
            _watchdogCts = null;
        }
    }
}
