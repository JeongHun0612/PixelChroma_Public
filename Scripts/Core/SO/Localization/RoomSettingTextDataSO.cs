using UnityEngine;
using UnityEngine.Localization;

[CreateAssetMenu(fileName = "RoomSettingTextData", menuName = "PixelChroma/Localization/RoomSettingTextData")]
public class RoomSettingTextDataSO : ScriptableObject
{
    [Header("Button")]
    public LocalizedString createButtonLabel;
    public LocalizedString editButtonLabel;

    [Header("Map")]
    public LocalizedString workshopDownloading;  // 목록 항목: 아직 내려받는 중인 창작마당 맵

    [Header("Warning")]
    public LocalizedString warningEmptyRoomName;
    public LocalizedString warningBanned;
    public LocalizedString defaultRoomName;
}
