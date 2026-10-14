using UnityEngine;
using UnityEngine.Localization;

[CreateAssetMenu(
    fileName = "WorkshopMapCheckTextData",
    menuName = "PixelChroma/Localization/WorkshopMapCheckTextData")]
public class WorkshopMapCheckTextDataSO : ScriptableObject
{
    [Header("Status")]
    public LocalizedString statusNeedDownload; // "맵이 설치되어 있지 않습니다. 아래 버튼을 눌러 다운로드해 주세요."
    public LocalizedString statusWaiting;       // "다른 유저의 다운로드를 기다리는 중입니다..."
    public LocalizedString statusSubscribing;   // "구독 대기 중..."
    public LocalizedString statusDownloading;   // "다운로드 중..."
    public LocalizedString statusTimeout;       // "다운로드 시간 초과. 다시 시도해주세요."
    public LocalizedString statusComplete;      // "다운로드 완료! 다른 유저를 기다리는 중..."
    public LocalizedString statusFailed;        // "다운로드 실패. 다시 시도해주세요."

    [Header("Slot")]
    public LocalizedString slotInstalled;       // "완료"
    public LocalizedString slotNotInstalled;    // "미설치"
}