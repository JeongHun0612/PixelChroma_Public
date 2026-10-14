using Mirror;

namespace PixelChroma
{
    public enum TimeState { Waiting, Countdown, HideTime, SeekTime, CheckTime, GameEnd }

    public struct TimeStateMessage : NetworkMessage
    {
        public TimeState state;
        public float timer;
    }

    public struct WinnerDecidedMessage : NetworkMessage
    {
        public PlayerRole winner;
    }

    public struct RoomOptionSyncMessage : NetworkMessage
    {
        public RoomOptionData option;
    }

    // ── 창작마당 맵 보유 확인 ────────────────────────

    // 서버 → 클라: "이 맵 갖고 있어?"
    public struct WorkshopMapCheckRequest : NetworkMessage
    {
        public ulong fileId;
    }

    // 클라 → 서버: "나는 있어/없어"
    public struct WorkshopMapCheckResponse : NetworkMessage
    {
        public bool isInstalled;
        public string playerName;
    }

    // 서버 → 전체 클라: "검사 결과"
    public struct WorkshopMapCheckResult : NetworkMessage
    {
        public bool allInstalled;
        public WorkshopPlayerStatus[] playerStatuses;
        public ulong fileId;
        public string mapName;           
    }

    // 클라 → 서버: "나 다운 완료됐어"
    public struct WorkshopMapDownloadComplete : NetworkMessage
    {
        public ulong fileId;
    }
    public struct WorkshopPlayerStatus
    {
        public uint netId;          // 강퇴·슬롯 매칭에 사용
        public bool isInstalled;
        public string playerName;   // 폴백 이름
    }


    // ── 스킵 투표 ────────────────────────────────

    // 클라 → 서버: "나 스킵에 동의할게"
    public struct SkipVoteRequest : NetworkMessage { }

    // 서버 → 전체 클라: "현재 투표 현황"
    public struct SkipVoteSync : NetworkMessage
    {
        public int currentVotes;   // 현재까지 동의한 인원 수
        public int requiredVotes;  // 스킵에 필요한 총 인원 수
    }
}
