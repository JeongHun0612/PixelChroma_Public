using UnityEngine;

namespace PixelChroma
{
    // Pixel nickname이 기본 정책이어도, 실험/롤백/호환을 위해 텍스트 닉네임 저장 흐름은 분리 보존한다.
    public static class TextNicknameService
    {
        public const string PrefsKey = "CustomPlayerNickname";
        public const int MaxLength = 10;

        // 접두사(6자) + 임의 번호(4자)가 MaxLength에 딱 맞는다. 셋 중 하나를 바꾸려면 함께 맞출 것.
        private const string DefaultNamePrefix = "Chroma";

        public static string GetDefaultSteamName()
        {
            try
            {
                if (Steamworks.SteamAPI.IsSteamRunning())
                    return Truncate(Steamworks.SteamFriends.GetPersonaName(), MaxLength);
            }
            catch (System.Exception) { }

            return DefaultNamePrefix + Random.Range(1000, 9999);
        }

        public static string LoadOrDefault()
        {
            string saved = PlayerPrefs.GetString(PrefsKey, "");
            return string.IsNullOrEmpty(saved) ? GetDefaultSteamName() : saved;
        }

        public static void Save(string nickname)
        {
            PlayerPrefs.SetString(PrefsKey, nickname);
            PlayerPrefs.Save();
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
                return value;

            // 스팀 이름에는 이모지가 흔하다. 서로게이트 쌍 가운데를 자르면 깨진 글자가 남으므로 한 칸 물러난다.
            if (maxLength > 0 && char.IsHighSurrogate(value[maxLength - 1]))
                maxLength--;

            return value.Substring(0, maxLength);
        }
    }
}
