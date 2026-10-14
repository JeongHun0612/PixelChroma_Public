using System.Collections.Generic;

namespace PixelChroma
{
    public class InfectionGameModeRule : GameModeRule
    {
        public override GameMode Mode => GameMode.Infection;
        public override bool CanSeeRunnerNickname(TimeState currentState)// Runner끼리 CheckTime전까지 닉네임 못봄
            => currentState != TimeState.HideTime && currentState != TimeState.SeekTime;

        public override bool CanSeeRunnerBody(TimeState currentState)//Runner끼리 Hide때 서로 몸 안보임
            => currentState != TimeState.HideTime;

        //Runner관전: HideTime/SeekTime에는 본인만, CheckTime(정산)부터는 전원 관전 가능
        public override IReadOnlyList<RunnerPlayer> GetSpectatableRunners(RunnerPlayer viewer, TimeState currentState)
        {
            if (currentState != TimeState.HideTime && currentState != TimeState.SeekTime)
            {
                return InGameManager.Instance != null
                    ? InGameManager.Instance.Runners
                    : (IReadOnlyList<RunnerPlayer>)System.Array.Empty<RunnerPlayer>();
            }

            return viewer != null ? new[] { viewer } : System.Array.Empty<RunnerPlayer>();
        }

        public override bool ShouldShowTeammateIndicator(PlayerRole role) => role == PlayerRole.Seeker;

        public override bool CanHearRunnerTauntSound(TimeState currentState)// Runner끼리 Hide때 도발 사운드 안들림
    => currentState != TimeState.HideTime;
    }
}