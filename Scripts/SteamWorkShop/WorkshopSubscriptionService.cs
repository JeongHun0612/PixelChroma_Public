using Steamworks;
using System;
using System.Collections.Generic;
using UnityEngine;
using PixelChroma;  // GameManager 접근용

public static class WorkshopSubscriptionService
{
    /// <summary>아이템 다운로드가 끝났을 때 발행. 인자는 publishedFileId.</summary>
    public static event Action<ulong> OnItemDownloaded;

    /// <summary>스팀 오버레이가 닫혔을 때 발행. 오버레이에서 구독하고 돌아온 경우를 잡기 위한 신호다.</summary>
    public static event Action OnOverlayClosed;

    private static Callback<DownloadItemResult_t> _downloadResult;
    private static Callback<GameOverlayActivated_t> _overlayActivated;
    private static bool _callbacksReady;

    /// <summary>
    /// 스팀 콜백을 최초 1회 등록한다. 스팀이 초기화된 뒤에 불려야 하므로 실제 사용 시점에 호출한다.
    /// </summary>
    public static void EnsureCallbacks()
    {
        if (_callbacksReady) return;
        if (!GameManager.SteamInitialized) return;

        _downloadResult = Callback<DownloadItemResult_t>.Create(OnDownloadItemResult);
        _overlayActivated = Callback<GameOverlayActivated_t>.Create(OnGameOverlayActivated);
        _callbacksReady = true;
    }

    private static void OnDownloadItemResult(DownloadItemResult_t result)
    {
        if (result.m_unAppID.m_AppId != WorkshopUrls.AppId) return;

        ulong fileId = result.m_nPublishedFileId.m_PublishedFileId;
        Debug.Log($"[WorkshopSubscriptionService] 다운로드 완료 | fileId={fileId}, result={result.m_eResult}");

        if (result.m_eResult == EResult.k_EResultOK)
            OnItemDownloaded?.Invoke(fileId);
    }

    private static void OnGameOverlayActivated(GameOverlayActivated_t param)
    {
        // 열릴 때가 아니라 닫힐 때만 알린다. (구독하고 게임으로 돌아온 시점)
        if (param.m_bActive == 0)
            OnOverlayClosed?.Invoke();
    }

    public static List<WorkshopMapEntry> GetSubscribedMaps()
    {
        var result = new List<WorkshopMapEntry>();

        if (!GameManager.SteamInitialized)  // 수정
            return result;

        uint count = SteamUGC.GetNumSubscribedItems();
        if (count == 0)
            return result;

        var ids = new PublishedFileId_t[count];
        SteamUGC.GetSubscribedItems(ids, count);

        for (int i = 0; i < ids.Length; i++)
        {
            var state = (EItemState)SteamUGC.GetItemState(ids[i]);
            bool installed = (state & EItemState.k_EItemStateInstalled) != 0;

            string path = "";
            if (installed)
            {
                SteamUGC.GetItemInstallInfo(ids[i], out _, out path, 1024, out _);
            }

            result.Add(new WorkshopMapEntry(ids[i].m_PublishedFileId, path, installed));
        }

        return result;
    }

    public static void RequestDownload(ulong fileId)
    {
        if (!GameManager.SteamInitialized)  // 수정
            return;

        EnsureCallbacks();
        SteamUGC.DownloadItem(new PublishedFileId_t(fileId), true);
    }

    public static string GetInstallPath(ulong fileId)
    {
        if (!GameManager.SteamInitialized)  // 수정
            return string.Empty;

        var id = new PublishedFileId_t(fileId);
        var state = (EItemState)SteamUGC.GetItemState(id);

        if ((state & EItemState.k_EItemStateInstalled) == 0)
            return string.Empty;

        if (SteamUGC.GetItemInstallInfo(id, out _, out string path, 1024, out _))
            return path;

        return string.Empty;
    }

    public static bool IsSubscribed(ulong fileId)
    {
        if (!GameManager.SteamInitialized) return false;  // 수정
        var state = (EItemState)SteamUGC.GetItemState(new PublishedFileId_t(fileId));
        return (state & EItemState.k_EItemStateSubscribed) != 0;
    }

    public static bool IsInstalled(ulong fileId)
    {
        if (!GameManager.SteamInitialized) return false;  // 수정
        var state = (EItemState)SteamUGC.GetItemState(new PublishedFileId_t(fileId));
        return (state & EItemState.k_EItemStateInstalled) != 0;
    }

    public static void SubscribeItem(ulong fileId)
    {
        if (!GameManager.SteamInitialized) return;  // 수정
        SteamUGC.SubscribeItem(new PublishedFileId_t(fileId));
    }
}