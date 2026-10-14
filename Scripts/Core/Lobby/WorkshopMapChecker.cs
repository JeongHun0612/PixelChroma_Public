using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Mirror;
using UnityEngine;

namespace PixelChroma
{
    public static class WorkshopMapChecker
    {
        /// <summary>검사 결과가 나왔을 때 발행. UILobby에서 구독.</summary>
        public static event Action<WorkshopMapCheckResult> OnCheckCompleted;

        private static readonly Dictionary<int, WorkshopMapCheckResponse> _responses = new();
        private static int _expectedCount;
        private static ulong _pendingFileId;
        private static CancellationTokenSource _cts;

        // ── 핸들러 등록/해제 ─────────────────────────────

        // RegisterHandlers (클라이언트 전용)
        public static void RegisterHandlers()
        {
            NetworkClient.RegisterHandler<WorkshopMapCheckRequest>(OnClientReceiveRequest);
        }

        public static void UnregisterHandlers()
        {
            NetworkClient.UnregisterHandler<WorkshopMapCheckRequest>();
            Cancel();
        }


        // RegisterServerHandlers (서버 전용)
        public static void RegisterServerHandlers()
        {
            NetworkServer.RegisterHandler<WorkshopMapCheckResponse>(OnServerReceiveResponse);
            NetworkServer.RegisterHandler<WorkshopMapDownloadComplete>(OnServerReceiveDownloadComplete);
        }

        // UnregisterServerHandlers 
        public static void UnregisterServerHandlers()
        {
            NetworkServer.UnregisterHandler<WorkshopMapCheckResponse>();
            NetworkServer.UnregisterHandler<WorkshopMapDownloadComplete>();
        }

        // ── 서버 측: 검사 요청 ───────────────────────────

        /// <summary>[Server] 모든 클라이언트에 맵 보유 확인 요청</summary>
        public static void RequestCheck(ulong fileId)
        {
            Cancel();
            _responses.Clear();
            _pendingFileId = fileId;
            _expectedCount = NetworkServer.connections.Count;

            NetworkServer.SendToAll(new WorkshopMapCheckRequest { fileId = fileId });
            Debug.Log($"[WorkshopMapChecker] 검사 요청 전송 | fileId={fileId}, 대상={_expectedCount}명");

            _cts = new CancellationTokenSource();
            WaitForTimeout(_cts.Token).Forget();
        }

        // ── 서버 측: 응답 수집 ───────────────────────────

        private static void OnServerReceiveResponse(
            NetworkConnectionToClient conn, WorkshopMapCheckResponse msg)
        {
            _responses[conn.connectionId] = msg;
            Debug.Log($"[WorkshopMapChecker] 응답: {msg.playerName} → installed={msg.isInstalled}");

            if (_responses.Count >= _expectedCount)
            {
                Cancel();
                PublishResult();
            }
        }

        private static async UniTaskVoid WaitForTimeout(CancellationToken token)
        {
            try
            {
                await UniTask.Delay(5000, cancellationToken: token);
                Debug.LogWarning("[WorkshopMapChecker] 응답 타임아웃, 결과 발행");
                PublishResult();
            }
            catch (OperationCanceledException) { }
        }

        private static void PublishResult()
        {
            var statuses = new List<WorkshopPlayerStatus>();
            bool allInstalled = true;

            foreach (var conn in NetworkServer.connections.Values)
            {
                uint netId = conn.identity != null ? conn.identity.netId : 0;
                var player = conn.identity?.GetComponent<PlayerBase>();
                string name = player != null ? player.playerName : $"Player#{conn.connectionId}";

                bool installed;

                if (_responses.TryGetValue(conn.connectionId, out var resp))
                {
                    installed = resp.isInstalled;
                    if (!string.IsNullOrEmpty(resp.playerName))
                        name = resp.playerName;
                }
                else
                {
                    // 응답 없음 → 미설치 취급
                    installed = false;
                }

                if (!installed) allInstalled = false;

                statuses.Add(new WorkshopPlayerStatus
                {
                    netId = netId,
                    isInstalled = installed,
                    playerName = name
                });
            }

            string mapName = ResolveMapName(_pendingFileId);

            var result = new WorkshopMapCheckResult
            {
                allInstalled = allInstalled,
                playerStatuses = statuses.ToArray(),
                fileId = _pendingFileId,
                mapName = mapName
            };

            // 전원에게 브로드캐스트
            NetworkServer.SendToAll(result);

            // 전원 보유면 응답 정리
            if (result.allInstalled)
            {
                _responses.Clear();
                _pendingFileId = 0;
            }
        }

        private static string ResolveMapName(ulong fileId)
        {
            string path = WorkshopSubscriptionService.GetInstallPath(fileId);
            if (string.IsNullOrEmpty(path)) return $"Workshop #{fileId}";

            try
            {
                var manifest = WorkshopMapPackageReader.ReadManifest(path);
                return manifest.title ?? $"Workshop #{fileId}";
            }
            catch
            {
                return $"Workshop #{fileId}";
            }
        }

        // ── 클라이언트 측: 요청 수신 → 응답 ─────────────

        private static void OnClientReceiveRequest(WorkshopMapCheckRequest msg)
        {
            bool installed = GameManager.SteamActive
                ? WorkshopSubscriptionService.IsInstalled(msg.fileId)
                : true; // 에디터/로컬 → 항상 보유

            string myName = TextNicknameService.LoadOrDefault();

            NetworkClient.Send(new WorkshopMapCheckResponse
            {
                isInstalled = installed,
                playerName = myName
            });
        }

        // ── 호스트 클라 측: 결과 수신 ────────────────────

        /// <summary>
        /// NetworkClient.RegisterHandler에서 호출되어 이벤트를 발행한다.
        /// </summary>
        public static void HandleCheckResult(WorkshopMapCheckResult msg)
        {
            OnCheckCompleted?.Invoke(msg);
        }

        private static void Cancel()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        public static void HandlePlayerDisconnected(int connectionId)
        {
            // 진행 중인 검사가 없으면 무시
            if (_pendingFileId == 0) return;

            _responses.Remove(connectionId);
            _expectedCount = NetworkServer.connections.Count;

            // 남은 인원으로 결과 재발행
            // (PublishResult는 NetworkServer.connections.Values를 순회하므로
            //  이미 끊어진 플레이어는 자동으로 빠진다)
            PublishResult();
        }


        private static void OnServerReceiveDownloadComplete(
    NetworkConnectionToClient conn, WorkshopMapDownloadComplete msg)
        {
            if (msg.fileId != _pendingFileId) return;

            // 해당 클라의 응답을 "설치됨"으로 갱신
            if (_responses.ContainsKey(conn.connectionId))
            {
                var updated = _responses[conn.connectionId];
                updated.isInstalled = true;
                _responses[conn.connectionId] = updated;
            }

            Debug.Log($"[WorkshopMapChecker] 다운로드 완료 통보: connId={conn.connectionId}");

            // 갱신된 결과를 전원에게 재브로드캐스트
            PublishResult();
        }
    }
}
