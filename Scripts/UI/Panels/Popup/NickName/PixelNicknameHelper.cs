using Mirror;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma
{
    /// <summary>
    /// 픽셀 닉네임(PNG byte[])을 RawImage에 적용하거나,
    /// 설정에 따라 텍스트/이미지를 전환하는 공용 헬퍼.
    /// </summary>
    public static class PixelNicknameHelper
    {
        /// <summary>
        /// PNG 바이트 배열을 텍스처로 로드해 RawImage에 적용합니다.
        /// 기존 텍스처가 있으면 재사용하고, 없으면 새로 생성합니다.
        /// </summary>
        /// <returns>로드된 (또는 재사용된) Texture2D. 호출부가 수명을 관리합니다.</returns>
        public static Texture2D ApplyPng(RawImage image, byte[] pngData, Texture2D existing)
        {
            if (image == null || pngData == null || pngData.Length == 0)
                return existing;

            if (existing == null)
            {
                existing = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
            }

            existing.LoadImage(pngData);
            existing.filterMode = FilterMode.Point;
            image.texture = existing;
            return existing;
        }

        /// <summary>
        /// netId로 현재 씬의 PlayerBase를 찾습니다.
        /// </summary>
        public static PlayerBase FindPlayer(uint netId)
        {
            if (NetworkClient.spawned.TryGetValue(netId, out var identity))
                return identity != null ? identity.GetComponent<PlayerBase>() : null;
            return null;
        }

        /// <summary>
        /// PlayerBase에서 픽셀 PNG 데이터를 가져옵니다. 없으면 null.
        /// </summary>
        public static byte[] GetPixelData(PlayerBase player)
        {
            if (player == null) return null;
            if (player.pixelNicknamePng == null || player.pixelNicknamePng.Length == 0) return null;
            return player.pixelNicknamePng;
        }

        /// <summary>
        /// GameSettings.ShowPixelNickname이 켜져 있고, 픽셀 데이터가 있으면 true.
        /// </summary>
        public static bool ShouldShowPixel()
        {
            return GameSettings.ShowPixelNickname;
        }
    }
}