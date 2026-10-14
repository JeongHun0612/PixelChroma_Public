using UnityEngine;
using UnityEngine.Localization;
using PixelChroma;

// 로비 진입/세션 실패 사유(LobbyFailureReason) → 사용자 안내 문구 매핑의 단일 원본.
[CreateAssetMenu(fileName = "LobbyFailureTextData", menuName = "PixelChroma/Localization/LobbyFailureTextData")]
public class LobbyFailureTextDataSO : ScriptableObject
{
    public LocalizedString alreadyConnected;
    public LocalizedString createFailed;
    public LocalizedString quickMatchNotFound;
    public LocalizedString codeNotFound;
    public LocalizedString roomInGame;
    public LocalizedString joinFailed;
    public LocalizedString connectFailed;
    public LocalizedString disconnected;
    public LocalizedString timeout;
    public LocalizedString generic;   // None/미분류 대비 기본 문구

    public LocalizedString Get(LobbyFailureReason reason)
    {
        return reason switch
        {
            LobbyFailureReason.AlreadyConnected => alreadyConnected,
            LobbyFailureReason.CreateFailed => createFailed,
            LobbyFailureReason.QuickMatchNotFound => quickMatchNotFound,
            LobbyFailureReason.CodeRoomNotFound => codeNotFound,
            LobbyFailureReason.RoomInGame => roomInGame,
            LobbyFailureReason.JoinFailed => joinFailed,
            LobbyFailureReason.ConnectFailed => connectFailed,
            LobbyFailureReason.Disconnected => disconnected,
            LobbyFailureReason.Timeout => timeout,
            _ => generic,
        };
    }

    public string GetLocalized(LobbyFailureReason reason)
    {
        var localized = Get(reason);
        return localized != null ? localized.GetLocalizedString() : string.Empty;
    }
}
