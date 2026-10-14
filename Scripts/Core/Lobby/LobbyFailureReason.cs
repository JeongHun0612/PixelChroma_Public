namespace PixelChroma
{
    // 로비 진입/세션 실패 사유의 단일 어휘.
    // 문구 매핑은 LobbyFailureTextDataSO 한 곳에서만 관리한다.
    public enum LobbyFailureReason
    {
        None,

        // ── 로딩 이전(진입 시도) 단계 : OnLobbyFailed로 각 진입 UI에 전달 ──
        AlreadyConnected,    // 이미 호스팅/참가 중이라 요청을 거부함
        CreateFailed,        // 방 생성 실패(스팀 로비 생성 또는 호스트 시작 실패)
        QuickMatchNotFound,  // 빠른참가: 조건에 맞는 공개방 없음
        CodeRoomNotFound,    // 코드입장: 해당 코드의 방 없음
        RoomInGame,          // 코드입장: 해당 방이 이미 게임 진행 중이라 입장 불가
        JoinFailed,          // 방(스팀 로비) 입장 실패
        ConnectFailed,       // Mirror 연결 시도 자체가 실패

        // ── 로딩 이후(씬 전환/스폰) 단계 : GameManager가 타이틀로 강제 복귀시키며 전달 ──
        Disconnected,      // 세션 도중 서버와의 연결이 끊어짐
        Timeout,           // 씬 로드/스폰 대기 시간 초과(워치독 포함)
    }
}
