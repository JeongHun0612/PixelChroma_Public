using UnityEngine;
using UnityEngine.Localization;

[CreateAssetMenu(fileName = "HUDTextData", menuName = "PixelChroma/Localization/HUDTextData")]
public class HUDTextDataSO : ScriptableObject
{
    [Header("State")]
    public LocalizedString stateWaiting;
    public LocalizedString stateHideTime;
    public LocalizedString stateSeekTime;
    public LocalizedString stateCheckTime;
    public LocalizedString stateGameEnd;

    [Header("Result")]
    public LocalizedString resultSeekerWin;
    public LocalizedString resultRunnerWin;

    [Header("Spectate")]
    public LocalizedString spectateFreeView;

    [Header("Transition")]
    public LocalizedString transitionSeekTime;    // "술래 출발!"
    public LocalizedString transitionAllCaught;   // "전원 사망!"
    public LocalizedString transitionTimeUp;      // " 시간 종료!"

    [Header("Skip Vote")]
    public LocalizedString skipVoteLabel;        // "스킵하기"
    public LocalizedString skipVoteVotedLabel;   // "투표 완료"
}