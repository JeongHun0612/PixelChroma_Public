using PixelChroma;
using UnityEngine;

/// <summary>
/// 창작마당 웹 페이지 주소와 열기 방식을 한 곳에 모은다.
/// 스팀이 초기화돼 있으면 게임을 나가지 않는 오버레이 브라우저를 쓰고, 아니면 시스템 브라우저로 폴백한다.
/// </summary>
public static class WorkshopUrls
{
    public const uint AppId = 5023800;

    public static string Hub => $"https://steamcommunity.com/app/{AppId}/workshop/";

    public static string Item(ulong fileId)
        => $"https://steamcommunity.com/sharedfiles/filedetails/?id={fileId}";

    public static void OpenHub() => Open(Hub);

    public static void OpenItem(ulong fileId) => Open(Item(fileId));

    public static void Open(string url)
    {
        if (string.IsNullOrEmpty(url)) return;

        if (GameManager.SteamInitialized)
        {
            Steamworks.SteamFriends.ActivateGameOverlayToWebPage(url);
            return;
        }

        Application.OpenURL(url);
    }
}
