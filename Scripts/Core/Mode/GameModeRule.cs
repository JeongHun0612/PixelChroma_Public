using System.Collections.Generic;

namespace PixelChroma
{

    public abstract class GameModeRule
    {
        public abstract GameMode Mode { get; }

        // 술래에게 잡혔을 때 해당 러너를 어떻게 처리할지 결정
        public virtual void OnRunnerCaught(RunnerPlayer runner) => runner.ServerBeginDeath(Mode);

        // 현재 타임스테이트에서 러너끼리 서로의 닉네임을 볼 수 있는지 결정
        public abstract bool CanSeeRunnerNickname(TimeState currentState);

        // Runner가 다른 Runner의 몸(스프라이트)을 볼 수 있는지
        public abstract bool CanSeeRunnerBody(TimeState currentState);

        //Runner관전자 List
        public abstract IReadOnlyList<RunnerPlayer> GetSpectatableRunners(RunnerPlayer viewer, TimeState currentState);
        //팀 위치 확인용 indicator 
        public abstract bool ShouldShowTeammateIndicator(PlayerRole role);

        // 현재 타임스테이트에서 viewerRole 플레이어가 "어떤 역할"의 인디케이터를 볼지 결정한다.
        // PlayerRole.None을 반환하면 아무것도 표시하지 않는다.
        public virtual PlayerRole GetIndicatorTargetRole(PlayerRole viewerRole, TimeState currentState)
        {
            if (viewerRole == PlayerRole.None) return PlayerRole.None;

            // CheckTime(정산)에는 역할과 무관하게 Runner들의 위치를 공개한다.
            if (currentState == TimeState.CheckTime) return PlayerRole.Runner;

            // 그 외에는 기존 규칙 그대로: 같은 역할끼리 표시할지 여부만 판단.
            return ShouldShowTeammateIndicator(viewerRole) ? viewerRole : PlayerRole.None;
        }

        // 인디케이터에 거리 제한을 적용할지 결정한다.
        // CheckTime(정산)에는 맵 전체의 위치를 공개하므로 제한을 풀어준다.
        public virtual bool ShouldLimitIndicatorDistance(TimeState currentState)
            => currentState != TimeState.CheckTime;

        // Runner가 다른 Runner의 도발(노출) 사운드를 들을 수 있는지
        public virtual bool CanHearRunnerTauntSound(TimeState currentState) => true;
    }
}