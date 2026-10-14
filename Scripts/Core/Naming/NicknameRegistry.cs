using System.Collections.Generic;
using UnityEngine;

namespace PixelChroma
{
    // 서버측 커넥션별 닉네임 명부. 플레이어 스폰 시점에 오브젝트에 이름을 입히기 위해 보관한다.
    // 네트워크 전송/스폰과 무관한 데이터라 NetworkManager에서 분리했다. 서버 전용.
    public static class NicknameRegistry
    {
        private static readonly Dictionary<int, string> _names = new();
        private static readonly Dictionary<int, byte[]> _pixelData = new();
        public static void Register(int connId, string nickname)
        {
            _names[connId] = nickname;
            Debug.Log($"[NicknameRegistry] 닉네임 등록 완료 - ConnID: {connId}, Name: {nickname}");
        }

        public static void RegisterPixel(int connId, byte[] pngData)
    => _pixelData[connId] = pngData;

        public static bool TryGetPixel(int connId, out byte[] pngData)
    => _pixelData.TryGetValue(connId, out pngData);

        public static void Unregister(int connId)
        {
            _names.Remove(connId);
            _pixelData.Remove(connId);
        }

        public static bool TryGet(int connId, out string nickname) => _names.TryGetValue(connId, out nickname);

        // 서버 종료 시 전체 초기화. (다음 호스트 세션으로의 stale 캐리오버 방지)
        public static void Clear()
        {
            _names.Clear();
            _pixelData.Clear();
        }
    }
}
