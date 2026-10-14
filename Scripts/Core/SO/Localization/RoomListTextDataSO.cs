using UnityEngine;
using UnityEngine.Localization;

[CreateAssetMenu(fileName = "RoomListTextData", menuName = "PixelChroma/Localization/RoomListTextData")]
public class RoomListTextDataSO : ScriptableObject
{
    [Header("Filter")]
    public LocalizedString allFilterLabel;

    [Header("Status")]
    public LocalizedString searchingStatus;
    public LocalizedString joiningStatus;

    [Header("Room Item")]
    public LocalizedString lobbyStatusLabel;
    public LocalizedString inGameStatusLabel;
    public LocalizedString defaultRoomNameFormat;
}