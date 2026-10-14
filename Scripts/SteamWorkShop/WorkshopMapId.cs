/// <summary>
/// RoomOption.mapName 에 실리는 창작마당 맵 식별자("workshop:{fileId}")를 다루는 헬퍼.
/// 접두어 문자열이 여러 곳에 흩어져 있던 것을 여기로 모은다.
/// </summary>
public static class WorkshopMapId
{
    public const string Prefix = "workshop:";

    public static bool IsWorkshop(string mapId)
    {
        return !string.IsNullOrEmpty(mapId) && mapId.StartsWith(Prefix);
    }

    public static bool TryGetFileId(string mapId, out ulong fileId)
    {
        fileId = 0;
        if (!IsWorkshop(mapId)) return false;
        return ulong.TryParse(mapId.Substring(Prefix.Length), out fileId);
    }

    public static string From(ulong fileId)
    {
        return Prefix + fileId;
    }
}
