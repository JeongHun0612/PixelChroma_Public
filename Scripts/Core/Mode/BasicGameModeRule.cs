using System.Collections.Generic;

namespace PixelChroma
{
    public class BasicGameModeRule : GameModeRule
    {
        public override GameMode Mode => GameMode.Basic;
        public override bool CanSeeRunnerNickname(TimeState currentState) => true;//Runner끼리 닉네임 항상 보임
        public override bool CanSeeRunnerBody(TimeState currentState) => true;//Runner끼리 몸 항상 보임
        public override IReadOnlyList<RunnerPlayer> GetSpectatableRunners(RunnerPlayer viewer, TimeState currentState)//Runner전원 관전 가능
        {
            return InGameManager.Instance != null ?
                InGameManager.Instance.Runners : System.Array.Empty<RunnerPlayer>();
        }

        public override bool ShouldShowTeammateIndicator(PlayerRole role) => true;
    }
}