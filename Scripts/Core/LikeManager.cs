using System.Collections.Generic;

namespace PixelChroma
{
    /// <summary>
    /// 서버측 좋아요 중복 방지 장부.
    /// (보낸이 → 받는이) 조합을 세션 내 1회만 허용한다.
    /// 누적 카운트는 각 클라이언트가 LikeService(PlayerPrefs)로 관리하므로
    /// 이 클래스는 중복 검사만 담당한다.
    /// </summary>
    public static class LikeManager
    {
        private static readonly HashSet<(int sender, int target)> _records = new();


        public static bool TryAddRecord(int senderConnId, int targetConnId)
        {
            return _records.Add((senderConnId, targetConnId));
        }

        public static void ClearRecords() => _records.Clear();

        public static void Clear() => _records.Clear();
    }
}