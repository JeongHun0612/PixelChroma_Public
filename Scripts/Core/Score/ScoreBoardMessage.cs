using Mirror;

namespace PixelChroma
{
    public struct ScoreBoardMessage : NetworkMessage
    {
        public ScoreEntry[] entries;    // 순위순 정렬 완료된 배열
        public float refreshInterval;  // 갱신 주기(초) — 클라가 카운트다운 표시용
    }
}