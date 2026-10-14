using Cysharp.Threading.Tasks;
using OneText.UGUI;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    public class UIWorkshopMapCheck : MonoBehaviour
    {
        [Header("Member List")]
        [SerializeField] private Transform slotContainer;             
        [SerializeField] private UIWorkshopMemberSlot slotPrefab;

        [Header("Localization")]
        [SerializeField] private WorkshopMapCheckTextDataSO textData;

        [Header("Buttons")]
        [SerializeField] private Button downloadButton;
        [SerializeField] private Button leaveLobbyButton;

        [Header("Status")]
        [SerializeField] private OneTextLabel statusText;

        private UILobby _lobby;
        private ulong _fileId;
        private bool _isDownloading;
        private CancellationTokenSource _downloadCts;
        private readonly List<UIWorkshopMemberSlot> _activeSlots = new();
        // ── 초기화 ──────────────────────────────────────

        public void Initialize(UILobby lobby)
        {
            _lobby = lobby;

            if (downloadButton != null)
                downloadButton.AddClickListener(OnClickDownload);
            if (leaveLobbyButton != null)
                leaveLobbyButton.AddClickListener(OnClickLeaveLobby);

            gameObject.SetActive(false);
        }

        public void Subscribe()
        {
            WorkshopMapChecker.OnCheckCompleted += OnCheckCompleted;
        }

        public void Unsubscribe()
        {
            WorkshopMapChecker.OnCheckCompleted -= OnCheckCompleted;
            CancelDownload();
        }

        public void Hide()
        {
            CancelDownload();

            // 슬롯 비활성화
            foreach (var slot in _activeSlots)
            {
                if (slot != null)
                    slot.gameObject.SetActive(false);
            }

            gameObject.SetActive(false);
        }

        // ── 결과 수신 (모든 클라이언트) ─────────────────

        private void OnCheckCompleted(WorkshopMapCheckResult result)
        {
            _fileId = result.fileId;

            // 전원 보유 → 게임 시작 (호스트만 실행)
            if (result.allInstalled)
            {
                if (LobbyManager.Instance.IsHost)
                    _lobby.ProceedStartGame();

                Hide();
                return;
            }

            // 패널 표시
            gameObject.SetActive(true);

            // 멤버 슬롯 갱신
            RefreshSlots(result.playerStatuses);

            // 본인의 설치 상태에 따라 UI 분기
            bool iHaveIt = !GameManager.SteamActive
                           || WorkshopSubscriptionService.IsInstalled(result.fileId);

            if (iHaveIt)
            {
                SetStatus(textData != null
                    ? textData.statusWaiting.GetLocalizedString()
                    : "Waiting for other players...");

                if (downloadButton != null)
                    downloadButton.gameObject.SetActive(false);
            }
            else
            {
                if (!_isDownloading)
                {
                    SetStatus(textData != null
                        ? textData.statusNeedDownload.GetLocalizedString()
                        : "Map not installed. Please download it.");
                    if (downloadButton != null)
                    {
                        downloadButton.gameObject.SetActive(true);
                        downloadButton.interactable = true;
                    }
                }
            }

            // 호스트는 시작 버튼 비활성 유지
            if (LobbyManager.Instance.IsHost)
                _lobby.NotifyWorkshopCheckFailed();
        }

        // ── 다운로드 ────────────────────────────────────

        private void OnClickDownload()
        {
            if (_isDownloading) return;
            _isDownloading = true;

            // 창작마당 아이템 페이지 열기
            WorkshopUrls.OpenItem(_fileId);

            // 버튼은 유지하되 비활성화 (다시 누르기 방지)
            if (downloadButton != null)
                downloadButton.interactable = false;

            WaitForSubscriptionAsync().Forget();
        }

        private async UniTaskVoid DownloadAndWaitAsync()
        {
            CancelDownload();
            _downloadCts = new CancellationTokenSource();
            var token = _downloadCts.Token;

            try
            {
                // 1. 구독
                SetStatus(textData != null
                    ? textData.statusSubscribing.GetLocalizedString()
                    : "Subscribing...");
                WorkshopSubscriptionService.SubscribeItem(_fileId);
                await UniTask.Delay(1000, cancellationToken: token);

                // 2. 다운로드
                SetStatus(textData != null
                    ? textData.statusDownloading.GetLocalizedString()
                    : "Downloading...");
                WorkshopSubscriptionService.RequestDownload(_fileId);

                // 3. 설치 완료 대기 (최대 60초)
                float elapsed = 0f;
                const float timeout = 60f;
                const float pollInterval = 0.5f;

                while (!token.IsCancellationRequested)
                {
                    if (WorkshopSubscriptionService.IsInstalled(_fileId))
                        break;

                    elapsed += pollInterval;
                    if (elapsed >= timeout)
                    {
                        SetStatus(textData != null
                            ? textData.statusTimeout.GetLocalizedString()
                            : "Download timed out.");
                        _isDownloading = false;
                        if (downloadButton != null) downloadButton.interactable = true;
                        return;
                    }

                    await UniTask.Delay((int)(pollInterval * 1000), cancellationToken: token);
                }

                if (token.IsCancellationRequested) return;

                // 4. 완료 → 서버에 통보
                SetStatus(textData != null
                    ? textData.statusComplete.GetLocalizedString()
                    : "Download complete!");
                if (downloadButton != null)
                    downloadButton.gameObject.SetActive(false);

                Mirror.NetworkClient.Send(new WorkshopMapDownloadComplete
                {
                    fileId = _fileId
                });
            }
            catch (System.OperationCanceledException) { }
            catch (System.Exception e)
            {
                Debug.LogError($"[UIWorkshopMapCheck] 다운로드 실패: {e.Message}");
                SetStatus(textData != null
                    ? textData.statusFailed.GetLocalizedString()
                    : "Download failed.");
                _isDownloading = false;
                if (downloadButton != null) downloadButton.interactable = true;
            }
        }

        // ── 로비 나가기 ─────────────────────────────────

        private void OnClickLeaveLobby()
        {
            CancelDownload();
            GameManager.Instance.LeaveSession();
        }

        // ── 유틸 ────────────────────────────────────────
        private void RefreshSlots(WorkshopPlayerStatus[] statuses)
        {
            if (slotContainer == null || slotPrefab == null) return;

            bool isLocalHost = LobbyManager.Instance.IsHost;
            int needed = statuses?.Length ?? 0;

            // 로컬라이즈 문자열 미리 캐싱 (매 슬롯마다 호출하지 않도록)
            string installedLabel = textData != null
                ? textData.slotInstalled.GetLocalizedString()
                : "OK";
            string notInstalledLabel = textData != null
                ? textData.slotNotInstalled.GetLocalizedString()
                : "Missing";

            // 부족하면 생성
            while (_activeSlots.Count < needed)
            {
                var slot = Instantiate(slotPrefab, slotContainer);
                _activeSlots.Add(slot);
            }

            // 데이터 바인딩
            for (int i = 0; i < _activeSlots.Count; i++)
            {
                if (i < needed)
                {
                    _activeSlots[i].gameObject.SetActive(true);
                    _activeSlots[i].Set(statuses[i], isLocalHost, installedLabel, notInstalledLabel);
                }
                else
                {
                    _activeSlots[i].gameObject.SetActive(false);
                }
            }
        }

        private async UniTaskVoid WaitForSubscriptionAsync()
        {
            CancelDownload();
            _downloadCts = new CancellationTokenSource();
            var token = _downloadCts.Token;

            try
            {
                // 1. 구독 대기 (유저가 Workshop 페이지에서 구독할 때까지)
                SetStatus(textData != null
                    ? textData.statusSubscribing.GetLocalizedString()
                    : "Subscribing...");

                float elapsed = 0f;
                const float subscribeTimeout = 120f;
                const float pollInterval = 1f;

                while (!token.IsCancellationRequested)
                {
                    if (WorkshopSubscriptionService.IsSubscribed(_fileId))
                        break;

                    elapsed += pollInterval;
                    if (elapsed >= subscribeTimeout)
                    {
                        SetStatus(textData != null
                            ? textData.statusTimeout.GetLocalizedString()
                            : "Download timed out.");
                        _isDownloading = false;
                        if (downloadButton != null) downloadButton.interactable = true;
                        return;
                    }

                    await UniTask.Delay((int)(pollInterval * 1000), cancellationToken: token);
                }

                if (token.IsCancellationRequested) return;

                // 2. 구독 확인 → 다운로드 자동 시작
                SetStatus(textData != null
                    ? textData.statusDownloading.GetLocalizedString()
                    : "Downloading...");
                WorkshopSubscriptionService.RequestDownload(_fileId);

                // 3. 설치 완료 대기 (최대 60초)
                elapsed = 0f;
                const float downloadTimeout = 60f;
                const float downloadPoll = 0.5f;

                while (!token.IsCancellationRequested)
                {
                    if (WorkshopSubscriptionService.IsInstalled(_fileId))
                        break;

                    elapsed += downloadPoll;
                    if (elapsed >= downloadTimeout)
                    {
                        SetStatus(textData != null
                            ? textData.statusTimeout.GetLocalizedString()
                            : "Download timed out.");
                        _isDownloading = false;
                        if (downloadButton != null) downloadButton.interactable = true;
                        return;
                    }

                    await UniTask.Delay((int)(downloadPoll * 1000), cancellationToken: token);
                }

                if (token.IsCancellationRequested) return;

                // 4. 완료 → 서버에 통보
                SetStatus(textData != null
                    ? textData.statusComplete.GetLocalizedString()
                    : "Download complete!");
                if (downloadButton != null)
                    downloadButton.gameObject.SetActive(false);

                Mirror.NetworkClient.Send(new WorkshopMapDownloadComplete
                {
                    fileId = _fileId
                });
            }
            catch (System.OperationCanceledException) { }
            catch (System.Exception e)
            {
                Debug.LogError($"[UIWorkshopMapCheck] 처리 실패: {e.Message}");
                SetStatus(textData != null
                    ? textData.statusFailed.GetLocalizedString()
                    : "Download failed.");
                _isDownloading = false;
                if (downloadButton != null) downloadButton.interactable = true;
            }
        }
        private void SetStatus(string text)
        {
            if (statusText == null) return;
            statusText.text = text;
        }

        private void CancelDownload()
        {
            _downloadCts?.Cancel();
            _downloadCts?.Dispose();
            _downloadCts = null;
            _isDownloading = false;
        }
    }
}